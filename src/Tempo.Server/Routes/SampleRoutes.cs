namespace Tempo.Server.Routes
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Enums;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Pre-auth sample endpoints used by startup template steps.</summary>
    public class SampleRoutes
    {
        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1.0/samples/external-rest",
                ExternalRestSampleAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Run the built-in External.Rest template endpoint", "Samples"));
        }

        private static Task ExternalRestSampleAsync(HttpContextBase ctx)
        {
            Dictionary<string, object> body = new Dictionary<string, object>
            {
                ["sample"] = "external-rest",
                ["message"] = "Hello from the Tempo REST template endpoint.",
                ["result"] = StepResultTypeEnum.Success.ToString()
            };

            return RouteHelpers.JsonAsync(ctx, 200, body);
        }
    }
}
