using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kopfgeld
{
    // Tiny HTTP server on 127.0.0.1 for the OBS browser source. Plain TcpListener, so it
    // needs neither admin rights nor a URL reservation, and loopback-only listeners do
    // not trigger a firewall prompt.
    sealed class OverlayServer : IDisposable
    {
        // Fonts and pictures the overlay may load; nothing else of the embedded UI is served here.
        static readonly Regex AssetPath = new Regex("^/(fonts/[a-z0-9-]+\\.(?:woff2|css)|img/(?:thumbs/)?[a-z0-9-]+\\.jpg)$");

        readonly Func<string> statsJson;
        readonly Func<string> statusJson;
        readonly Func<string, string> overlayJson;
        TcpListener listener;
        CancellationTokenSource cts;

        public int Port { get; private set; }

        public OverlayServer(Func<string> statsJson, Func<string> statusJson, Func<string, string> overlayJson)
        {
            this.statsJson = statsJson;
            this.statusJson = statusJson;
            this.overlayJson = overlayJson;
        }

        public bool Start(int port, out string error)
        {
            error = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
            }
            catch (SocketException ex)
            {
                listener = null;
                error = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? Lang.F("Port {0} ist schon belegt – bitte in den Einstellungen einen anderen Overlay-Port wählen",
                        "Port {0} is already in use – please pick another overlay port in the settings", port)
                    : Lang.T("Overlay-Server konnte nicht starten: ", "The overlay server could not start: ") + ex.Message;
                return false;
            }
            Port = port;
            cts = new CancellationTokenSource();
            var token = cts.Token;
            var server = listener;
            Task.Run(() => AcceptLoopAsync(server, token));
            Log.Write("Overlay-Server läuft auf http://127.0.0.1:" + port + "/overlay");
            return true;
        }

        async Task AcceptLoopAsync(TcpListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await server.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
                var handling = HandleAsync(client);
            }
        }

        async Task HandleAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.NoDelay = true;
                    var stream = client.GetStream();
                    var buffer = new byte[8192];
                    int total = 0;
                    int headerEnd = -1;
                    while (total < buffer.Length && headerEnd < 0)
                    {
                        var read = stream.ReadAsync(buffer, total, buffer.Length - total);
                        if (await Task.WhenAny(read, Task.Delay(5000)).ConfigureAwait(false) != read) return;
                        int n = read.Result;
                        if (n <= 0) return;
                        total += n;
                        headerEnd = IndexOfHeaderEnd(buffer, total);
                    }
                    if (headerEnd < 0) return;

                    string head = Encoding.ASCII.GetString(buffer, 0, headerEnd);
                    string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string[] requestLine = lines[0].Split(' ');
                    if (requestLine.Length < 2) return;
                    string method = requestLine[0];
                    string path = requestLine[1];
                    string query = "";
                    int queryStart = path.IndexOf('?');
                    if (queryStart >= 0)
                    {
                        query = path.Substring(queryStart + 1);
                        path = path.Substring(0, queryStart);
                    }

                    string host = null;
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) host = lines[i].Substring(5).Trim();
                    }

                    byte[] response;
                    if (!IsLocalHost(host)) response = Build(403, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Forbidden"));
                    else if (method != "GET") response = Build(405, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Method Not Allowed"));
                    else response = Route(path, query);
                    await stream.WriteAsync(response, 0, response.Length).ConfigureAwait(false);
                }
                catch
                {
                    // Client went away; nothing to do.
                }
            }
        }

        // Rejects requests whose Host header is not ours (DNS-rebinding protection).
        bool IsLocalHost(string host)
        {
            if (host == null) return false;
            string port = Port.ToString(CultureInfo.InvariantCulture);
            return host == "127.0.0.1:" + port || host.Equals("localhost:" + port, StringComparison.OrdinalIgnoreCase);
        }

        byte[] Route(string path, string query)
        {
            switch (path)
            {
                case "/overlay":
                case "/overlay/":
                    return Resource("overlay/overlay.html");
                case "/overlay.js":
                    return Resource("overlay/overlay.js");
                case "/overlay.css":
                    return Resource("overlay/overlay.css");
                case "/api/stats":
                    return Build(200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(statsJson()));
                case "/api/frame":
                {
                    // The profile, the values and the language in one answer: the browser source polls only this.
                    string json = "{\"config\":" + overlayJson(QueryValue(query, "o")) + ",\"stats\":" + statsJson() + ",\"lang\":\"" + Lang.Id + "\"}";
                    return Build(200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
                }
                case "/status.json":
                    return Build(200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(statusJson()));
                case "/":
                    return Build(200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(IndexPage()));
                default:
                    if (AssetPath.IsMatch(path)) return Resource(path.Substring(1));
                    return Build(404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not Found"));
            }
        }

        static string QueryValue(string query, string key)
        {
            foreach (string part in query.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && part.Substring(0, eq) == key) return Uri.UnescapeDataString(part.Substring(eq + 1));
            }
            return null;
        }

        static byte[] Resource(string name)
        {
            byte[] bytes = AppInfo.Resource(name);
            return bytes == null
                ? Build(404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not Found"))
                : Build(200, AppInfo.ContentType(name), bytes);
        }

        string IndexPage()
        {
            return "<!doctype html><html lang=\"" + Lang.Id + "\"><head><meta charset=\"utf-8\"><title>" + AppInfo.Name + " – Overlay</title>" +
                "<style>body{font:15px/1.55 'Segoe UI',system-ui,sans-serif;background:#0f0e0c;color:#e9e3d8;margin:32px;max-width:680px}" +
                "a{color:#e0b25a}code{background:#1d1a17;padding:2px 6px;border-radius:4px}</style></head><body>" +
                "<h1 style=\"font:600 22px Georgia,serif;margin-bottom:4px\">" + AppInfo.Name + " – Overlay</h1>" +
                "<p style=\"margin-top:0;color:#a3998b\">by <a href=\"" + AppInfo.AuthorUrl + "\">" + AppInfo.Author + "</a></p>" +
                Lang.T("<p>In OBS eine <b>Browser</b>-Quelle mit dieser Adresse anlegen:</p>", "<p>In OBS, add a <b>Browser</b> source with this address:</p>") +
                "<p><code>http://127.0.0.1:" + Port + "/overlay</code></p>" +
                Lang.T("<p>Aussehen und Werte stellst du im Programm unter „Overlay“ ein. <a href=\"/overlay\">Vorschau öffnen</a> · <a href=\"/api/stats\">Rohdaten</a></p>",
                    "<p>Look and values are set in the program under “Overlay”. <a href=\"/overlay\">Open preview</a> · <a href=\"/api/stats\">Raw data</a></p>") +
                "</body></html>";
        }

        static byte[] Build(int status, string contentType, byte[] content)
        {
            string reason = status == 200 ? "OK" : status == 403 ? "Forbidden" : status == 404 ? "Not Found" : "Method Not Allowed";
            string header =
                "HTTP/1.1 " + status + " " + reason + "\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + content.Length + "\r\n" +
                "Cache-Control: no-store\r\n" +
                "X-Content-Type-Options: nosniff\r\n" +
                "Connection: close\r\n\r\n";
            byte[] head = Encoding.ASCII.GetBytes(header);
            var result = new byte[head.Length + content.Length];
            Buffer.BlockCopy(head, 0, result, 0, head.Length);
            Buffer.BlockCopy(content, 0, result, head.Length, content.Length);
            return result;
        }

        static int IndexOfHeaderEnd(byte[] buffer, int length)
        {
            for (int i = 3; i < length; i++)
            {
                if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n') return i - 3;
            }
            return -1;
        }

        public void Dispose()
        {
            if (cts != null) cts.Cancel();
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                }
            }
        }
    }
}
