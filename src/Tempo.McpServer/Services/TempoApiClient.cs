namespace Tempo.McpServer.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.McpServer.Settings;

    /// <summary>
    /// Lightweight REST client used by MCP tools to call Tempo.Server.
    /// </summary>
    public sealed class TempoApiClient : IDisposable
    {
        private readonly HttpClient _HttpClient;
        private readonly TempoEndpointSettings _Settings;

        /// <summary>Default tenant identifier.</summary>
        public string? DefaultTenantId => _Settings.DefaultTenantId;

        /// <summary>Tempo API endpoint.</summary>
        public string Endpoint => _Settings.Endpoint;

        /// <summary>Instantiate.</summary>
        /// <param name="settings">Tempo endpoint settings.</param>
        public TempoApiClient(TempoEndpointSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _HttpClient = new HttpClient();
            _HttpClient.BaseAddress = new Uri(NormalizeEndpoint(settings.Endpoint), UriKind.Absolute);
            _HttpClient.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, settings.TimeoutMs));
        }

        /// <summary>Run a GET request.</summary>
        /// <param name="path">Relative API path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API response.</returns>
        public async Task<TempoApiResponse> GetAsync(string path, CancellationToken token)
        {
            return await SendAsync(HttpMethod.Get, path, null, token).ConfigureAwait(false);
        }

        /// <summary>Run a POST request.</summary>
        /// <param name="path">Relative API path.</param>
        /// <param name="body">Optional JSON body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API response.</returns>
        public async Task<TempoApiResponse> PostAsync(string path, JsonNode? body, CancellationToken token)
        {
            return await SendAsync(HttpMethod.Post, path, body, token).ConfigureAwait(false);
        }

        /// <summary>Run a PUT request.</summary>
        /// <param name="path">Relative API path.</param>
        /// <param name="body">Optional JSON body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API response.</returns>
        public async Task<TempoApiResponse> PutAsync(string path, JsonNode? body, CancellationToken token)
        {
            return await SendAsync(HttpMethod.Put, path, body, token).ConfigureAwait(false);
        }

        /// <summary>Run a DELETE request.</summary>
        /// <param name="path">Relative API path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API response.</returns>
        public async Task<TempoApiResponse> DeleteAsync(string path, CancellationToken token)
        {
            return await SendAsync(HttpMethod.Delete, path, null, token).ConfigureAwait(false);
        }

        /// <summary>Run an arbitrary supported REST request.</summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Relative API path.</param>
        /// <param name="body">Optional JSON body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API response.</returns>
        public async Task<TempoApiResponse> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken token)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            string normalizedPath = NormalizeApiPath(path);

            using HttpRequestMessage request = new HttpRequestMessage(method, normalizedPath);
            ApplyAuthentication(request);
            if (body != null)
            {
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false);
            string content = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            TempoApiResponse apiResponse = new TempoApiResponse
            {
                StatusCode = (int)response.StatusCode,
                Success = response.IsSuccessStatusCode,
                ContentType = response.Content?.Headers.ContentType?.MediaType
            };

            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                apiResponse.Headers[header.Key] = string.Join(", ", header.Value);
            }

            if (response.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                {
                    apiResponse.Headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            ApplyBody(apiResponse, content);
            return apiResponse;
        }

        /// <summary>Add query-string values to a path.</summary>
        /// <param name="path">Base path.</param>
        /// <param name="query">Query values.</param>
        /// <returns>Path with query string.</returns>
        public static string AddQuery(string path, IDictionary<string, string?> query)
        {
            if (query == null || query.Count == 0) return path;

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string?> pair in query)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
                parts.Add(Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value));
            }

            if (parts.Count == 0) return path;
            string separator = path.Contains("?", StringComparison.Ordinal) ? "&" : "?";
            return path + separator + string.Join("&", parts);
        }

        /// <summary>Escape a single URL path segment.</summary>
        /// <param name="value">Path segment.</param>
        /// <returns>Escaped segment.</returns>
        public static string EscapeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Path segment is required", nameof(value));
            return Uri.EscapeDataString(value);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _HttpClient.Dispose();
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) endpoint = Tempo.McpServer.Constants.DefaultTempoEndpoint;
            return endpoint.TrimEnd('/');
        }

        private static void ApplyBody(TempoApiResponse response, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            try
            {
                response.Body = JsonNode.Parse(content);
            }
            catch
            {
                response.Text = content;
            }
        }

        private static string NormalizeApiPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("API path is required", nameof(path));
            if (Uri.TryCreate(path, UriKind.Absolute, out Uri? absoluteUri) && absoluteUri != null)
                throw new ArgumentException("API path must be relative", nameof(path));
            if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;
            if (path.Contains("\\", StringComparison.Ordinal)) throw new ArgumentException("API path cannot contain backslashes", nameof(path));
            if (path.Contains("..", StringComparison.Ordinal)) throw new ArgumentException("API path cannot contain parent traversal", nameof(path));
            if (!path.Equals("/", StringComparison.Ordinal) && !path.StartsWith("/v1.0/", StringComparison.Ordinal))
                throw new ArgumentException("API path must be / or start with /v1.0/", nameof(path));
            return path;
        }

        private void ApplyAuthentication(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(_Settings.Token))
                request.Headers.TryAddWithoutValidation(Tempo.Core.Constants.HeaderToken, _Settings.Token);
            if (!string.IsNullOrWhiteSpace(_Settings.ApiKey))
                request.Headers.TryAddWithoutValidation(Tempo.Core.Constants.HeaderApiKey, _Settings.ApiKey);
            if (!string.IsNullOrWhiteSpace(_Settings.AccessKey))
                request.Headers.TryAddWithoutValidation(Tempo.Core.Constants.HeaderAccessKey, _Settings.AccessKey);
            if (!string.IsNullOrWhiteSpace(_Settings.SecretKey))
                request.Headers.TryAddWithoutValidation(Tempo.Core.Constants.HeaderSecretKey, _Settings.SecretKey);
            if (!string.IsNullOrWhiteSpace(_Settings.DefaultTenantId))
                request.Headers.TryAddWithoutValidation(Tempo.Core.Constants.HeaderTenantId, _Settings.DefaultTenantId);
        }
    }
}
