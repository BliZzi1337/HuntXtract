using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kopfgeld
{
    class ObsException : Exception
    {
        public ObsException(string message) : base(message) { }
    }

    sealed class ObsAuthException : ObsException
    {
        public ObsAuthException(string message) : base(message) { }
    }

    sealed class ObsClosedException : ObsException
    {
        public readonly int Code;
        public ObsClosedException(int code, string message) : base(message) { Code = code; }
    }

    sealed class ObsRequestException : ObsException
    {
        public readonly int Code;
        public ObsRequestException(int code, string message) : base(message) { Code = code; }
    }

    sealed class ObsResponse
    {
        public Dictionary<string, object> Data;
        // GetSourceScreenshot's base64 data URL, cut out before JSON parsing (it can be megabytes).
        public string ImageData;
    }

    // Minimal obs-websocket 5.x client (the server built into OBS 28 and newer).
    sealed class ObsClient : IDisposable
    {
        const int AuthenticationFailed = 4009;
        const int UnsupportedRpcVersion = 4010;
        const string ImageDataKey = "\"imageData\":\"";

        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly ConcurrentDictionary<string, TaskCompletionSource<ObsResponse>> pending =
            new ConcurrentDictionary<string, TaskCompletionSource<ObsResponse>>();
        readonly byte[] receiveBuffer = new byte[64 * 1024];
        readonly MemoryStream message = new MemoryStream();
        int nextRequestId;
        int disposed;

        public event Action<string> Disconnected;

        public bool IsConnected
        {
            get { return socket.State == WebSocketState.Open && !cts.IsCancellationRequested; }
        }

        public async Task ConnectAsync(string host, int port, string password, CancellationToken token)
        {
            socket.Options.AddSubProtocol("obswebsocket.json");
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            var uri = new Uri("ws://" + host + ":" + port.ToString(CultureInfo.InvariantCulture));

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token))
            {
                timeout.CancelAfter(6000);
                await socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);

                var hello = Json.Parse(await ReceiveTextAsync(timeout.Token).ConfigureAwait(false));
                if (Json.Long(hello, "op", -1) != 0)
                    throw new ObsException(Lang.T("Unerwartete Antwort – ist das wirklich der OBS-WebSocket-Server?", "Unexpected answer – is this really the OBS WebSocket server?"));
                var helloData = Json.Obj(hello["d"]);

                var identify = new Dictionary<string, object>();
                identify["rpcVersion"] = 1;
                identify["eventSubscriptions"] = 0;
                object authObject;
                if (helloData.TryGetValue("authentication", out authObject) && authObject != null)
                {
                    if (string.IsNullOrEmpty(password))
                        throw new ObsAuthException(Lang.T("OBS verlangt ein Passwort – bitte in der Einrichtung eintragen", "OBS asks for a password – please enter it in the setup"));
                    var auth = Json.Obj(authObject);
                    identify["authentication"] = ComputeAuth(password, Json.Str(auth, "salt"), Json.Str(auth, "challenge"));
                }
                await SendAsync(Envelope(1, identify), timeout.Token).ConfigureAwait(false);

                Dictionary<string, object> identified;
                try
                {
                    identified = Json.Parse(await ReceiveTextAsync(timeout.Token).ConfigureAwait(false));
                }
                catch (ObsClosedException ex)
                {
                    if (ex.Code == AuthenticationFailed) throw new ObsAuthException(Lang.T("Falsches OBS-WebSocket-Passwort", "Wrong OBS WebSocket password"));
                    if (ex.Code == UnsupportedRpcVersion)
                        throw new ObsException(Lang.T("Diese OBS-Version wird nicht unterstützt (OBS 28 oder neuer nötig)", "This OBS version is not supported (OBS 28 or newer needed)"));
                    throw;
                }
                if (Json.Long(identified, "op", -1) != 2) throw new ObsException(Lang.T("OBS hat die Anmeldung nicht bestätigt", "OBS did not confirm the login"));
            }

            var receiving = Task.Run(() => ReceiveLoopAsync());
        }

        public async Task<ObsResponse> RequestAsync(string requestType, Dictionary<string, object> requestData, int timeoutMs)
        {
            string id = "b" + Interlocked.Increment(ref nextRequestId).ToString(CultureInfo.InvariantCulture);
            var tcs = new TaskCompletionSource<ObsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = tcs;

            var d = new Dictionary<string, object>();
            d["requestType"] = requestType;
            d["requestId"] = id;
            if (requestData != null) d["requestData"] = requestData;

            TaskCompletionSource<ObsResponse> removed;
            try
            {
                await SendAsync(Envelope(6, d), cts.Token).ConfigureAwait(false);
            }
            catch
            {
                pending.TryRemove(id, out removed);
                throw;
            }

            var finished = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (finished != tcs.Task)
            {
                pending.TryRemove(id, out removed);
                throw new ObsException(Lang.F("OBS antwortet nicht ({0})", "OBS does not answer ({0})", requestType));
            }
            return await tcs.Task.ConfigureAwait(false);
        }

        static Dictionary<string, object> Envelope(int op, Dictionary<string, object> data)
        {
            var envelope = new Dictionary<string, object>();
            envelope["op"] = op;
            envelope["d"] = data;
            return envelope;
        }

        static string ComputeAuth(string password, string salt, string challenge)
        {
            using (var sha = SHA256.Create())
            {
                string secret = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password + salt)));
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(secret + challenge)));
            }
        }

        async Task SendAsync(Dictionary<string, object> payload, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Json.Serialize(payload));
            await sendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }

        // Only ever called by one reader at a time (handshake first, then the receive loop).
        async Task<string> ReceiveTextAsync(CancellationToken token)
        {
            message.SetLength(0);
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    int code = socket.CloseStatus.HasValue ? (int)socket.CloseStatus.Value : 0;
                    string reason = socket.CloseStatusDescription;
                    throw new ObsClosedException(code, Lang.T("OBS hat die Verbindung beendet", "OBS closed the connection") +
                        (string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")"));
                }
                message.Write(receiveBuffer, 0, result.Count);
                if (result.EndOfMessage) break;
            }
            return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
        }

        async Task ReceiveLoopAsync()
        {
            string reason = Lang.T("Verbindung zu OBS getrennt", "Connection to OBS lost");
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    HandleMessage(await ReceiveTextAsync(cts.Token).ConfigureAwait(false));
                }
            }
            catch (ObsClosedException ex)
            {
                reason = ex.Message;
            }
            catch (OperationCanceledException)
            {
                reason = Lang.T("Verbindung beendet", "Connection closed");
            }
            catch (Exception ex)
            {
                reason = Lang.T("Verbindung zu OBS getrennt (", "Connection to OBS lost (") + ex.Message + ")";
            }
            FailPending(reason);
            var handler = Disconnected;
            if (handler != null && !cts.IsCancellationRequested) handler(reason);
        }

        void HandleMessage(string text)
        {
            string imageData = null;
            int keyIndex = text.IndexOf(ImageDataKey, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                // Base64 never contains quotes or escapes, so the value ends at the next quote.
                int start = keyIndex + ImageDataKey.Length;
                int end = text.IndexOf('"', start);
                if (end > start)
                {
                    imageData = text.Substring(start, end - start);
                    text = text.Substring(0, start) + text.Substring(end);
                }
            }

            var msg = Json.Parse(text);
            if (Json.Long(msg, "op", -1) != 7) return; // only request responses matter here
            var d = Json.Obj(msg["d"]);
            string id = Json.Str(d, "requestId");
            TaskCompletionSource<ObsResponse> tcs;
            if (id == null || !pending.TryRemove(id, out tcs)) return;

            var status = Json.Obj(d["requestStatus"]);
            if (!Json.Bool(status, "result"))
            {
                int code = (int)Json.Long(status, "code", 0);
                string comment = Json.Str(status, "comment");
                tcs.TrySetException(new ObsRequestException(code, string.IsNullOrEmpty(comment) ? Lang.T("OBS-Fehler ", "OBS error ") + code : comment));
                return;
            }
            object responseData;
            d.TryGetValue("responseData", out responseData);
            tcs.TrySetResult(new ObsResponse { Data = Json.Obj(responseData), ImageData = imageData });
        }

        void FailPending(string reason)
        {
            foreach (var entry in pending)
            {
                TaskCompletionSource<ObsResponse> tcs;
                if (pending.TryRemove(entry.Key, out tcs)) tcs.TrySetException(new ObsException(reason));
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 1) return;
            cts.Cancel();
            try
            {
                socket.Abort();
            }
            catch
            {
            }
            socket.Dispose();
            FailPending("Verbindung beendet");
        }
    }
}
