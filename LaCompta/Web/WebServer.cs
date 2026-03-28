using StardewModdingAPI;
using System;
using System.Net;
using System.Threading;

namespace LaCompta.Web
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly ApiController _api;
        private readonly IMonitor _monitor;
        private readonly int _port;
        private Thread _thread = null!;
        private bool _running;

        public WebServer(ApiController api, IMonitor monitor, int port = 5555)
        {
            _api = api;
            _monitor = monitor;
            _port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            // Also listen on 127.0.0.1 for compatibility
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        }

        public void Start()
        {
            if (_running) return;

            try
            {
                _listener.Start();
                _running = true;
                _thread = new Thread(Listen)
                {
                    IsBackground = true,
                    Name = "LaCompta-WebServer"
                };
                _thread.Start();
                _monitor.Log($"LaCompta dashboard available at http://localhost:{_port}", LogLevel.Info);
            }
            catch (HttpListenerException ex)
            {
                _monitor.Log($"Failed to start web server on port {_port}: {ex.Message}", LogLevel.Error);
                _monitor.Log("Try a different port or check if another process is using it.", LogLevel.Error);
            }
        }

        public void Stop()
        {
            if (!_running) return;

            _running = false;
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception ex)
            {
                _monitor.Log($"Error stopping web server: {ex.Message}", LogLevel.Debug);
            }
            _monitor.Log("LaCompta web server stopped.", LogLevel.Info);
        }

        private void Listen()
        {
            while (_running)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Expected when listener is stopped
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // CORS headers for local browser access
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                // Handle preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // Route request
                var path = request.Url?.AbsolutePath ?? "/";
                var query = request.Url?.Query ?? "";
                _api.HandleRequest(path, query, response);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Error handling request: {ex.Message}", LogLevel.Debug);
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }
    }
}
