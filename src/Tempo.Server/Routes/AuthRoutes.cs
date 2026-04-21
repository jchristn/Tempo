namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Authentication-related route registrar.</summary>
    public class AuthRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public AuthRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/v1.0/token", IssueTokenAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Issue an authentication token", "Auth"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/token", ValidateTokenAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Validate the current token", "Auth"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/token/details", TokenDetailsAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Decode the current token", "Auth"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/me", MeAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Describe the authenticated principal", "Auth"));
        }

        private async Task IssueTokenAsync(HttpContextBase ctx)
        {
            LoginRequest? body = RouteHelpers.Body<LoginRequest>(ctx);
            if (body == null || string.IsNullOrEmpty(body.Email) || string.IsNullOrEmpty(body.Password))
            {
                await RouteHelpers.BadRequestAsync(ctx, "email and password are required.");
                return;
            }

            RequestContext ctxAuth = await _Host.Authentication.AuthenticateAsync(
                tokenHeader: null,
                bearerToken: null,
                apiKey: null,
                accessKey: null,
                secretKey: null,
                tenantIdHeader: body.TenantId,
                emailHeader: body.Email,
                passwordHeader: body.Password).ConfigureAwait(false);

            if (!ctxAuth.IsAuthenticated)
            {
                await RouteHelpers.UnauthorizedAsync(ctx);
                return;
            }

            string token;
            TokenResponse resp = new TokenResponse();
            if (!string.IsNullOrEmpty(ctxAuth.AdministratorId))
            {
                token = _Host.Tokens.IssueAdminToken(ctxAuth.AdministratorId!, ctxAuth.AccountId);
                resp.AdministratorId = ctxAuth.AdministratorId;
            }
            else if (!string.IsNullOrEmpty(ctxAuth.UserId) && !string.IsNullOrEmpty(ctxAuth.TenantId))
            {
                token = _Host.Tokens.IssueUserToken(ctxAuth.TenantId!, ctxAuth.UserId!, ctxAuth.AccountId);
                resp.TenantId = ctxAuth.TenantId;
                resp.UserId = ctxAuth.UserId;
            }
            else
            {
                await RouteHelpers.UnauthorizedAsync(ctx);
                return;
            }

            resp.Token = token;
            resp.ExpiresUtc = DateTime.UtcNow.AddMinutes(_Host.Settings.Auth.TokenExpirationMinutes);
            await RouteHelpers.JsonAsync(ctx, 200, resp);
        }

        private async Task ValidateTokenAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated)
            {
                await RouteHelpers.UnauthorizedAsync(ctx);
                return;
            }
            await RouteHelpers.JsonAsync(ctx, 200, new { valid = true, authenticationResult = rc.AuthenticationResult.ToString() });
        }

        private async Task TokenDetailsAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated)
            {
                await RouteHelpers.UnauthorizedAsync(ctx);
                return;
            }
            await RouteHelpers.JsonAsync(ctx, 200, new
            {
                tenantId = rc.TenantId,
                userId = rc.UserId,
                administratorId = rc.AdministratorId,
                isAdmin = rc.IsAdmin,
                isTenantAdmin = rc.IsTenantAdmin,
                email = rc.Email,
                principalName = rc.PrincipalName
            });
        }

        private async Task MeAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated)
            {
                await RouteHelpers.UnauthorizedAsync(ctx);
                return;
            }
            object? principal = null;
            if (rc.User != null) principal = new { type = "user", id = rc.User.Id, email = rc.User.Email, tenantId = rc.User.TenantId, isAdmin = rc.IsAdmin, isTenantAdmin = rc.IsTenantAdmin };
            else if (rc.Administrator != null) principal = new { type = "administrator", id = rc.Administrator.Id, email = rc.Administrator.Email };
            else principal = new { type = "anonymous" };
            await RouteHelpers.JsonAsync(ctx, 200, principal);
        }
    }
}
