namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal OTLP/HTTP stand-in for tests. Accepts any POST, records the request path and raw body, and replies
    /// 200 with an empty protobuf message (a valid OTLP export response). Thread-safe.
    /// </summary>
    public sealed class OtlpRecordingEndpoint : IDisposable
    {
        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();
        private readonly object _Lock = new object();
        private readonly List<KeyValuePair<string, byte[]>> _Requests = new List<KeyValuePair<string, byte[]>>();
        private readonly Task _Loop;

        /// <summary>Start listening on 127.0.0.1 at the given port.</summary>
        /// <param name="port">TCP port.</param>
        public OtlpRecordingEndpoint(int port)
        {
            BaseUrl = "http://127.0.0.1:" + port;
            _Listener.Prefixes.Add(BaseUrl + "/");
            _Listener.Start();
            _Loop = Task.Run(LoopAsync);
        }

        /// <summary>Base URL, without a trailing slash.</summary>
        public string BaseUrl { get; }

        /// <summary>Whether any recorded request whose path contains <paramref name="pathFragment"/> has a body containing <paramref name="text"/> (UTF-8).</summary>
        /// <param name="pathFragment">Path fragment such as /v1/logs.</param>
        /// <param name="text">Text to look for in the raw protobuf body.</param>
        /// <returns>True when found.</returns>
        public bool Received(string pathFragment, string text)
        {
            byte[] needle = Encoding.UTF8.GetBytes(text);
            lock (_Lock)
            {
                return _Requests.Any(r => r.Key.Contains(pathFragment, StringComparison.OrdinalIgnoreCase) && IndexOf(r.Value, needle) >= 0);
            }
        }

        /// <summary>Paths of every recorded request.</summary>
        public List<string> Paths
        {
            get { lock (_Lock) { return _Requests.Select(r => r.Key).ToList(); } }
        }

        /// <summary>Stop listening.</summary>
        public void Dispose()
        {
            try { _Cts.Cancel(); } catch { /* ignore */ }
            try { _Listener.Stop(); } catch { /* ignore */ }
            try { _Loop.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
            try { _Listener.Close(); } catch { /* ignore */ }
            _Cts.Dispose();
        }

        private async Task LoopAsync()
        {
            while (!_Cts.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                try
                {
                    using MemoryStream body = new MemoryStream();
                    await ctx.Request.InputStream.CopyToAsync(body).ConfigureAwait(false);
                    lock (_Lock)
                    {
                        _Requests.Add(new KeyValuePair<string, byte[]>(ctx.Request.Url?.AbsolutePath ?? string.Empty, body.ToArray()));
                    }

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/x-protobuf";
                    ctx.Response.ContentLength64 = 0;
                    ctx.Response.Close();
                }
                catch
                {
                    // Ignore a single failed request in the test helper.
                }
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }

            return -1;
        }
    }
}
