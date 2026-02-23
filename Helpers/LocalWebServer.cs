using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vatsys;

namespace AtopPlugin.Helpers
{
    public static class LocalWebServer
    {
        private static HttpListener _listener;
        private static CancellationTokenSource _cts;
        private static string _webRoot;
        private const int Port = 8180;
        public static string Url => $"http://localhost:{Port}/";

        public static void Start()
        {
            try
            {
                _webRoot = Path.GetDirectoryName(typeof(LocalWebServer).Assembly.Location);

                _listener = new HttpListener();
                _listener.Prefixes.Add(Url);
                _listener.Start();

                _cts = new CancellationTokenSource();
                Task.Run(() => ListenLoop(_cts.Token));

                // Auto-open browser after a short delay to let the server start
                Task.Delay(500).ContinueWith(_ => OpenInBrowser());
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"LocalWebServer: Failed to start - {ex.Message}"));
            }
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }

        public static void OpenInBrowser()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"LocalWebServer: Failed to open browser - {ex.Message}"));
            }
        }

        private static async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var requestPath = context.Request.Url.AbsolutePath.TrimStart('/');
                if (string.IsNullOrEmpty(requestPath)) requestPath = "index.html";

                var filePath = Path.Combine(_webRoot, requestPath);

                if (File.Exists(filePath))
                {
                    var content = File.ReadAllBytes(filePath);
                    context.Response.ContentType = GetMimeType(filePath);
                    context.Response.ContentLength64 = content.Length;
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.OutputStream.Write(content, 0, content.Length);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    var msg = System.Text.Encoding.UTF8.GetBytes("Not Found");
                    context.Response.OutputStream.Write(msg, 0, msg.Length);
                }
            }
            catch (Exception)
            {
                // Client disconnected
            }
            finally
            {
                context.Response.Close();
            }
        }

        private static string GetMimeType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            switch (ext)
            {
                case ".html": return "text/html";
                case ".css": return "text/css";
                case ".js": return "application/javascript";
                case ".json": return "application/json";
                case ".png": return "image/png";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                default: return "application/octet-stream";
            }
        }
    }
}
