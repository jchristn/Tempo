namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Health-check route registrar.</summary>
    public class HealthRoutes
    {
        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET, "/v1.0/api/health", HealthAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Server health check", "System"));
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET, "/", RootAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Server information", "System"));
        }

        private static async Task HealthAsync(HttpContextBase ctx)
        {
            await RouteHelpers.JsonAsync(ctx, 200, new { status = "ok", time = DateTime.UtcNow });
        }

        private static async Task RootAsync(HttpContextBase ctx)
        {
            await RouteHelpers.JsonAsync(ctx, 200, new { name = "Tempo Server", version = "0.1.0" });
        }
    }
}
