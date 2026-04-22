namespace Tempo.Server.Helpers
{
    using System;
    using System.Collections.Specialized;
    using System.Net;
    using WatsonWebserver.Core;

    /// <summary>
    /// Resolves the best available client IP from proxy headers or the direct socket endpoint.
    /// </summary>
    public static class ClientIpResolver
    {
        /// <summary>Resolve the client IP for the supplied request context.</summary>
        public static string? Resolve(HttpContextBase ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return Resolve(ctx.Request.Headers, ctx.Request.Source?.IpAddress);
        }

        /// <summary>Resolve the client IP from request headers and a direct remote address fallback.</summary>
        public static string? Resolve(NameValueCollection? headers, string? remoteIp)
        {
            string? forwarded = TryGetHeaderValue(headers, "Forwarded");
            string? resolved = ExtractForwarded(forwarded);
            if (!string.IsNullOrWhiteSpace(resolved)) return resolved;

            string? xForwardedFor = TryGetHeaderValue(headers, "X-Forwarded-For");
            resolved = ExtractXForwardedFor(xForwardedFor);
            if (!string.IsNullOrWhiteSpace(resolved)) return resolved;

            return NormalizeAddress(remoteIp);
        }

        private static string? ExtractForwarded(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            string[] entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string entry in entries)
            {
                string[] parameters = entry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string parameter in parameters)
                {
                    if (!parameter.StartsWith("for=", StringComparison.OrdinalIgnoreCase)) continue;
                    string candidate = parameter.Substring(4).Trim();
                    string? normalized = NormalizeAddress(candidate);
                    if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
                }
            }

            return null;
        }

        private static string? ExtractXForwardedFor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            string[] entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string entry in entries)
            {
                string? normalized = NormalizeAddress(entry);
                if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
            }

            return null;
        }

        private static string? NormalizeAddress(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            string candidate = value.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(candidate)) return null;
            if (string.Equals(candidate, "unknown", StringComparison.OrdinalIgnoreCase)) return null;
            if (candidate.StartsWith("_", StringComparison.Ordinal)) return null;

            if (candidate.StartsWith("[", StringComparison.Ordinal))
            {
                int closingBracket = candidate.IndexOf(']');
                if (closingBracket > 1)
                {
                    candidate = candidate.Substring(1, closingBracket - 1);
                }
            }

            if (IPAddress.TryParse(candidate, out IPAddress? direct))
            {
                return direct.ToString();
            }

            if (TryStripPort(candidate, out string hostOnly) && IPAddress.TryParse(hostOnly, out IPAddress? withoutPort))
            {
                return withoutPort.ToString();
            }

            return null;
        }

        private static bool TryStripPort(string value, out string host)
        {
            host = value;
            int lastColon = value.LastIndexOf(':');
            if (lastColon <= 0) return false;
            if (value.IndexOf(':') != lastColon) return false;

            string port = value.Substring(lastColon + 1);
            if (!int.TryParse(port, out _)) return false;

            host = value.Substring(0, lastColon);
            return !string.IsNullOrWhiteSpace(host);
        }

        private static string? TryGetHeaderValue(NameValueCollection? headers, string key)
        {
            if (headers == null || string.IsNullOrWhiteSpace(key)) return null;

            foreach (string? existing in headers.AllKeys)
            {
                if (string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
                {
                    return headers[existing];
                }
            }

            return null;
        }
    }
}
