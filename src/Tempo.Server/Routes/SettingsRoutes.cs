namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Security;
    using Tempo.Core.Settings;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Routes for reading and writing the server's settings (<c>tempo.json</c>).
    /// Admin-only. PUT persists to disk and hot-reloads the in-memory copy;
    /// sections requiring a reboot are returned in the response body.
    /// </summary>
    public class SettingsRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public SettingsRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/settings", GetAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Read the server settings JSON", "Settings"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/settings", PutAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Replace the server settings JSON and persist to disk", "Settings"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/settings/meta", MetaAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("List settings sections requiring a reboot", "Settings"));
        }

        private async Task<bool> RequireAdminAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return false; }
            if (!rc.IsAdmin) { await RouteHelpers.ForbiddenAsync(ctx); return false; }
            return true;
        }

        private async Task GetAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            await RouteHelpers.JsonAsync(ctx, 200, new
            {
                path = _Host.SettingsStore.Path,
                rebootRequiredSections = Tempo.Server.Services.SettingsStore.RebootRequiredSections,
                settings = _Host.SettingsStore.Current
            });
        }

        private async Task MetaAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            await RouteHelpers.JsonAsync(ctx, 200, new
            {
                rebootRequiredSections = Tempo.Server.Services.SettingsStore.RebootRequiredSections
            });
        }

        private async Task PutAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            Settings? body = RouteHelpers.Body<Settings>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            try
            {
                string[] rebootRequired = _Host.SettingsStore.Save(body);
                _Host.Logger.Info("[SettingsRoutes] settings replaced via PUT (reboot required: " + (rebootRequired.Length > 0 ? string.Join(",", rebootRequired) : "none") + ")");
                await RouteHelpers.JsonAsync(ctx, 200, new
                {
                    saved = true,
                    rebootRequired
                });
            }
            catch (Exception ex)
            {
                await RouteHelpers.ErrorAsync(ctx, 500, "SaveFailed", ex.Message);
            }
        }
    }
}
