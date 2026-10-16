using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Test double for the obs-websocket 5 server built into OBS: handshake with password
// auth, GetVersion, GetInputList and GetSourceScreenshot. It renders a busy "gameplay"
// scene, or – while mock-summary.flag exists – a fake end-of-match screen whose numbers
// count up for two seconds (values from the flag file as JSON, if any).
// Other flag files in the working directory simulate problems:
//   mock-nosource.flag -> 600 ResourceNotFound, mock-noimage.flag -> 702 render failed
static class MockObs
{
    const string SourceName = "Hunt Spielaufnahme";
    static string password = "test123";
    static long screenshots;
    static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    static void Main(string[] args)
    {
        int port = args.Length > 0 ? int.Parse(args[0]) : 4460;
        if (args.Length > 1) password = args[1];
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Console.WriteLine("MockObs auf Port " + port + ", Passwort '" + password + "'");
        var timer = new Timer(_ => Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " screenshots=" + Interlocked.Read(ref screenshots)), null, 5000, 5000);
        while (true)
        {
            var client = listener.AcceptTcpClient();
            Task.Run(() => Handle(client));
        }
    }

    static void Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                var s = client.GetStream();
                if (!Handshake(s)) return;
                string salt = RandomB64(), challenge = RandomB64();
                Send(s, Env(0, new Dictionary<string, object>
                {
                    { "obsWebSocketVersion", "5.6.2" }, { "rpcVersion", 1 },
                    { "authentication", new Dictionary<string, object> { { "challenge", challenge }, { "salt", salt } } },
                }));

                int op;
                var identify = Ser.Deserialize<Dictionary<string, object>>(ReadMessage(s, out op));
                var d = (Dictionary<string, object>)identify["d"];
                object auth;
                d.TryGetValue("authentication", out auth);
                if (auth as string != Auth(password, salt, challenge))
                {
                    Console.WriteLine("Anmeldung abgelehnt (falsches Passwort)");
                    SendClose(s, 4009, "Authentication failed.");
                    Thread.Sleep(200);
                    return;
                }
                Send(s, Env(2, new Dictionary<string, object> { { "negotiatedRpcVersion", 1 } }));
                Console.WriteLine("Client angemeldet");

                while (true)
                {
                    string text = ReadMessage(s, out op);
                    if (text == null)
                    {
                        Console.WriteLine("Client hat geschlossen");
                        return;
                    }
                    var msg = Ser.Deserialize<Dictionary<string, object>>(text);
                    if (Convert.ToInt32(msg["op"]) != 6) continue;
                    d = (Dictionary<string, object>)msg["d"];
                    string type = (string)d["requestType"];
                    object rdObj;
                    d.TryGetValue("requestData", out rdObj);
                    var rd = rdObj as Dictionary<string, object> ?? new Dictionary<string, object>();

                    object response = null;
                    int code = 100;
                    string comment = null;
                    switch (type)
                    {
                        case "GetVersion":
                            response = new Dictionary<string, object> { { "obsVersion", "32.2.2" }, { "obsWebSocketVersion", "5.6.2" }, { "rpcVersion", 1 } };
                            break;
                        case "GetInputList":
                            response = new Dictionary<string, object>
                            {
                                { "inputs", new object[]
                                    {
                                        Input("Webcam", "dshow_input"),
                                        Input("Desktop-Audio", "wasapi_output_capture"),
                                        Input(SourceName, "game_capture"),
                                        Input("Bildschirm", "monitor_capture"),
                                    }
                                },
                            };
                            break;
                        case "GetSourceScreenshot":
                            string name = rd.ContainsKey("sourceName") ? (string)rd["sourceName"] : "";
                            if (name != SourceName || File.Exists("mock-nosource.flag"))
                            {
                                code = 600;
                                comment = "No source was found by the name of `" + name + "`.";
                            }
                            else if (File.Exists("mock-noimage.flag"))
                            {
                                code = 702;
                                comment = "Failed to render screenshot.";
                            }
                            else
                            {
                                response = new Dictionary<string, object> { { "imageData", Render(rd) } };
                                Interlocked.Increment(ref screenshots);
                            }
                            break;
                        default:
                            code = 204;
                            comment = "Your request type is not valid.";
                            break;
                    }
                    var status = new Dictionary<string, object> { { "result", code == 100 }, { "code", code } };
                    if (comment != null) status["comment"] = comment;
                    var body = new Dictionary<string, object> { { "requestType", type }, { "requestId", d["requestId"] }, { "requestStatus", status } };
                    if (response != null) body["responseData"] = response;
                    Send(s, Env(7, body));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Verbindung beendet: " + ex.GetType().Name + " " + ex.Message);
            }
        }
    }

    static Dictionary<string, object> Input(string name, string kind)
    {
        return new Dictionary<string, object> { { "inputName", name }, { "inputKind", kind }, { "unversionedInputKind", kind } };
    }

    static Dictionary<string, object> Env(int op, Dictionary<string, object> d)
    {
        return new Dictionary<string, object> { { "op", op }, { "d", d } };
    }

    // Layout of the fake end screen (1920×1080). The test calibration uses the same numbers.
    public static readonly string[] RowLabels = { "STATUS", "HUNTER GETÖTET", "MONSTER GETÖTET", "KOPFGELD", "HUNT DOLLARS", "ERFAHRUNG" };
    public static readonly string[] RowKeys = { "status", "hunterKills", "monsterKills", "bounty", "huntDollars", "xp" };
    const int RowTop = 300, RowStep = 95, LabelX = 600, ValueRight = 1320;

    static string Render(Dictionary<string, object> rd)
    {
        string format = rd.ContainsKey("imageFormat") ? (string)rd["imageFormat"] : "png";
        using (var full = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb))
        {
            using (var g = Graphics.FromImage(full))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                if (File.Exists("mock-team.flag")) DrawTeam(g);
                else if (File.Exists("mock-summary.flag")) DrawSummary(g);
                else DrawGameplay(g);
            }
            Bitmap output = full;
            if (rd.ContainsKey("imageWidth"))
            {
                int w = Convert.ToInt32(rd["imageWidth"]);
                int h = (int)Math.Round(1080 * (w / 1920.0));
                output = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(output))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(full, 0, 0, w, h);
                }
            }
            using (var ms = new MemoryStream())
            {
                if (format == "png")
                {
                    output.Save(ms, ImageFormat.Png);
                }
                else
                {
                    var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                    var p = new EncoderParameters(1);
                    long q = rd.ContainsKey("imageCompressionQuality") ? Convert.ToInt64(rd["imageCompressionQuality"]) : 90;
                    p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, q < 0 ? 90 : q);
                    output.Save(ms, jpeg, p);
                }
                if (output != full) output.Dispose();
                return "data:image/" + format + ";base64," + Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    static void DrawGameplay(Graphics g)
    {
        long n = Interlocked.Read(ref screenshots);
        using (var sky = new LinearGradientBrush(new Rectangle(0, 0, 1920, 1080), Color.FromArgb(58, 70, 62), Color.FromArgb(24, 30, 22), 90f))
            g.FillRectangle(sky, 0, 0, 1920, 1080);
        var rng = new Random((int)(n % 1000));
        for (int i = 0; i < 70; i++)
        {
            int shade = rng.Next(20, 110);
            using (var b = new SolidBrush(Color.FromArgb(170, shade, Math.Min(255, shade + 12), Math.Max(0, shade - 8))))
                g.FillEllipse(b, (int)((rng.Next(0, 1920) + n * 13) % 1920), rng.Next(0, 1080), rng.Next(40, 280), rng.Next(30, 190));
        }
        using (var font = new Font("Segoe UI", 26, FontStyle.Bold, GraphicsUnit.Pixel))
            g.DrawString("SPIELSZENE #" + n, font, Brushes.White, 60, 1000);
    }

    // Second screen: a real screenshot of the Team Details tab, scaled to the frame width.
    static Bitmap teamImage;

    static void DrawTeam(Graphics g)
    {
        g.Clear(Color.FromArgb(14, 12, 10));
        if (teamImage == null && File.Exists("team-details.png")) teamImage = new Bitmap("team-details.png");
        if (teamImage == null) return;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(teamImage, 0, 0, 1920, 1920f * teamImage.Height / teamImage.Width);
    }

    static void DrawSummary(Graphics g)
    {
        DateTime shownAt;
        var values = ReadSummaryValues(out shownAt);
        double t = (DateTime.Now - shownAt).TotalSeconds;
        double progress = Math.Min(1.0, t / 2.0); // the numbers count up for two seconds
        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, 1920, 1080), Color.FromArgb(34, 28, 22), Color.FromArgb(12, 10, 8), 90f))
            g.FillRectangle(bg, 0, 0, 1920, 1080);
        using (var panel = new SolidBrush(Color.FromArgb(22, 18, 14)))
            g.FillRectangle(panel, 520, 250, 880, 620);
        var gold = Color.FromArgb(226, 190, 120);
        using (var title = new Font("Georgia", 60, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(gold))
        {
            var size = g.MeasureString("MATCH-ZUSAMMENFASSUNG", title);
            g.DrawString("MATCH-ZUSAMMENFASSUNG", title, brush, 960 - size.Width / 2, 90);
        }
        using (var pen = new Pen(gold, 3)) g.DrawLine(pen, 620, 190, 1300, 190);
        using (var labelFont = new Font("Segoe UI", 30, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var valueFont = new Font("Georgia", 46, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var labelBrush = new SolidBrush(Color.FromArgb(180, 170, 150)))
        using (var valueBrush = new SolidBrush(Color.FromArgb(240, 232, 214)))
        using (var redBrush = new SolidBrush(Color.FromArgb(214, 90, 74)))
        {
            for (int i = 0; i < RowLabels.Length; i++)
            {
                int y = RowTop + i * RowStep;
                g.DrawString(RowLabels[i], labelFont, labelBrush, LabelX, y + 10);
                string text;
                if (RowKeys[i] == "status")
                {
                    text = t < 1.0 ? "…" : Convert.ToString(values["status"]);
                }
                else
                {
                    long target = Convert.ToInt64(values[RowKeys[i]]);
                    long shown = (long)Math.Floor(target * progress);
                    text = RowKeys[i] == "huntDollars" ? shown.ToString("#,0", CultureInfo.InvariantCulture) : shown.ToString(CultureInfo.InvariantCulture);
                }
                var size = g.MeasureString(text, valueFont);
                g.DrawString(text, valueFont, text == "GESTORBEN" ? redBrush : valueBrush, ValueRight - size.Width, y);
            }
        }
    }

    static Dictionary<string, object> ReadSummaryValues(out DateTime shownAt)
    {
        shownAt = File.GetLastWriteTime("mock-summary.flag");
        var values = new Dictionary<string, object>
        {
            { "status", "EXTRAHIERT" }, { "hunterKills", 3 }, { "monsterKills", 12 }, { "bounty", 1 }, { "huntDollars", 1250 }, { "xp", 3480 },
        };
        try
        {
            string text = File.ReadAllText("mock-summary.flag");
            if (text.Trim().Length > 0)
                foreach (var kv in Ser.Deserialize<Dictionary<string, object>>(text)) values[kv.Key] = kv.Value;
        }
        catch
        {
        }
        return values;
    }

    static Color FromHue(long hue)
    {
        double h = hue / 60.0, x = 1 - Math.Abs(h % 2 - 1);
        double r = 0, g = 0, b = 0;
        if (h < 1) { r = 1; g = x; } else if (h < 2) { r = x; g = 1; } else if (h < 3) { g = 1; b = x; }
        else if (h < 4) { g = x; b = 1; } else if (h < 5) { r = x; b = 1; } else { r = 1; b = x; }
        return Color.FromArgb((int)(40 + r * 150), (int)(40 + g * 150), (int)(40 + b * 150));
    }

    static string Auth(string pw, string salt, string challenge)
    {
        using (var sha = SHA256.Create())
        {
            string secret = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(pw + salt)));
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(secret + challenge)));
        }
    }

    static string RandomB64()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    static bool Handshake(NetworkStream s)
    {
        var head = new StringBuilder();
        while (!head.ToString().EndsWith("\r\n\r\n"))
        {
            int b = s.ReadByte();
            if (b < 0) return false;
            head.Append((char)b);
        }
        string key = null;
        foreach (string line in head.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) key = line.Substring(18).Trim();
        }
        if (key == null) return false;
        string accept;
        using (var sha = SHA1.Create())
            accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        byte[] resp = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
            "Sec-WebSocket-Accept: " + accept + "\r\nSec-WebSocket-Protocol: obswebsocket.json\r\n\r\n");
        s.Write(resp, 0, resp.Length);
        return true;
    }

    static string ReadMessage(Stream s, out int opcode)
    {
        var payload = new MemoryStream();
        int first = 0;
        while (true)
        {
            int b0 = s.ReadByte(), b1 = s.ReadByte();
            if (b0 < 0 || b1 < 0) throw new EndOfStreamException();
            bool fin = (b0 & 0x80) != 0;
            int op = b0 & 0x0F;
            bool masked = (b1 & 0x80) != 0;
            long len = b1 & 0x7F;
            if (len == 126) len = (s.ReadByte() << 8) | s.ReadByte();
            else if (len == 127) { len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | (long)s.ReadByte(); }
            byte[] mask = masked ? ReadExact(s, 4) : null;
            byte[] data = ReadExact(s, (int)len);
            if (masked) for (int i = 0; i < data.Length; i++) data[i] ^= mask[i % 4];
            if (op == 0x9) { WriteFrame(s, 0xA, data); continue; }
            if (op == 0xA) continue;
            if (op == 0x8) { opcode = 8; return null; }
            if (op != 0) first = op;
            payload.Write(data, 0, data.Length);
            if (fin)
            {
                opcode = first;
                return Encoding.UTF8.GetString(payload.ToArray());
            }
        }
    }

    static byte[] ReadExact(Stream s, int count)
    {
        var buf = new byte[count];
        int got = 0;
        while (got < count)
        {
            int n = s.Read(buf, got, count - got);
            if (n <= 0) throw new EndOfStreamException();
            got += n;
        }
        return buf;
    }

    static void Send(Stream s, object payload)
    {
        WriteFrame(s, 0x1, Encoding.UTF8.GetBytes(Ser.Serialize(payload)));
    }

    static void SendClose(Stream s, int code, string reason)
    {
        byte[] r = Encoding.UTF8.GetBytes(reason);
        var data = new byte[2 + r.Length];
        data[0] = (byte)(code >> 8);
        data[1] = (byte)code;
        Buffer.BlockCopy(r, 0, data, 2, r.Length);
        WriteFrame(s, 0x8, data);
    }

    static void WriteFrame(Stream s, int opcode, byte[] data)
    {
        lock (s)
        {
            var header = new List<byte> { (byte)(0x80 | opcode) };
            if (data.Length < 126) header.Add((byte)data.Length);
            else if (data.Length <= 65535) { header.Add(126); header.Add((byte)(data.Length >> 8)); header.Add((byte)data.Length); }
            else { header.Add(127); for (int i = 7; i >= 0; i--) header.Add((byte)((long)data.Length >> (8 * i))); }
            s.Write(header.ToArray(), 0, header.Count);
            s.Write(data, 0, data.Length);
            s.Flush();
        }
    }
}
