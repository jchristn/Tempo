namespace Tempo.Server.Routes
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Core.Responses;
    using Tempo.Core.Security;
    using Tempo.Server.Serialization;
    using WatsonWebserver.Core;

    /// <summary>
    /// Shared helpers for route handlers.
    /// </summary>
    public static class RouteHelpers
    {
        /// <summary>Content type for JSON responses.</summary>
        public const string ContentTypeJson = "application/json";

        /// <summary>Write a JSON response body with the supplied status code.</summary>
        public static async Task JsonAsync(HttpContextBase ctx, int statusCode, object? body)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = ContentTypeJson;
            string payload = Serializer.Serialize(body);
            await ctx.Response.Send(payload).ConfigureAwait(false);
        }

        /// <summary>Write an error response.</summary>
        public static Task ErrorAsync(HttpContextBase ctx, int statusCode, string code, string message, string? details = null)
        {
            return JsonAsync(ctx, statusCode, new ErrorResponse(code, message, details));
        }

        /// <summary>Write a 401.</summary>
        public static Task UnauthorizedAsync(HttpContextBase ctx) =>
            ErrorAsync(ctx, 401, "Unauthorized", "Authentication is required.");

        /// <summary>Write a 403.</summary>
        public static Task ForbiddenAsync(HttpContextBase ctx) =>
            ErrorAsync(ctx, 403, "Forbidden", "You do not have access to this resource.");

        /// <summary>Write a 404.</summary>
        public static Task NotFoundAsync(HttpContextBase ctx, string? detail = null) =>
            ErrorAsync(ctx, 404, "NotFound", detail ?? "Resource not found.");

        /// <summary>Write a 400.</summary>
        public static Task BadRequestAsync(HttpContextBase ctx, string message) =>
            ErrorAsync(ctx, 400, "BadRequest", message);

        /// <summary>Retrieve the <see cref="RequestContext"/> from the Watson metadata slot.</summary>
        public static RequestContext? Context(HttpContextBase ctx)
        {
            if (ctx.Metadata is RequestContext rc) return rc;
            return null;
        }

        /// <summary>Read a path parameter by key.</summary>
        public static string? Path(HttpContextBase ctx, string key)
        {
            if (ctx.Request.Url.Parameters == null) return null;
            return ctx.Request.Url.Parameters.Get(key);
        }

        /// <summary>Read a single query parameter by name.</summary>
        public static string? Query(HttpContextBase ctx, string name)
        {
            if (ctx.Request.Query == null || ctx.Request.Query.Elements == null) return null;
            return ctx.Request.Query.Elements.Get(name);
        }

        /// <summary>Read a boolean query parameter.</summary>
        public static bool QueryBool(HttpContextBase ctx, string name, bool defaultValue = false)
        {
            string? v = Query(ctx, name);
            if (string.IsNullOrEmpty(v)) return defaultValue;
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        /// <summary>Read an int query parameter.</summary>
        public static int QueryInt(HttpContextBase ctx, string name, int defaultValue = 0)
        {
            string? v = Query(ctx, name);
            if (string.IsNullOrEmpty(v)) return defaultValue;
            return int.TryParse(v, out int i) ? i : defaultValue;
        }

        /// <summary>Read a <see cref="DateTime"/> query parameter.</summary>
        public static DateTime? QueryDateTime(HttpContextBase ctx, string name)
        {
            string? v = Query(ctx, name);
            if (string.IsNullOrEmpty(v)) return null;
            return DateTime.TryParse(v, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime dt) ? dt : null;
        }

        /// <summary>Deserialize the JSON request body as <typeparamref name="T"/>.</summary>
        public static T? Body<T>(HttpContextBase ctx)
        {
            if (string.IsNullOrEmpty(ctx.Request.DataAsString)) return default;
            return Serializer.Deserialize<T>(ctx.Request.DataAsString);
        }

        /// <summary>Read the raw request body bytes.</summary>
        public static byte[] BodyBytes(HttpContextBase ctx)
        {
            if (ctx.Request.DataAsBytes != null) return ctx.Request.DataAsBytes;
            if (!string.IsNullOrEmpty(ctx.Request.DataAsString)) return Encoding.UTF8.GetBytes(ctx.Request.DataAsString);
            return Array.Empty<byte>();
        }
    }
}
