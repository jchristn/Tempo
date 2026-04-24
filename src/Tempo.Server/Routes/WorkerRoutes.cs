namespace Tempo.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core;
    using Tempo.Core.Models;
    using Tempo.Core.Responses;
    using Tempo.Core.Security;
    using Tempo.Core.Workers;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Worker management REST routes and the worker WebSocket connection path.
    /// </summary>
    public class WorkerRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public WorkerRoutes(TempoServer host)
        {
            _Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1.0/workers",
                ListAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("List workers", "Workers")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.Enumeration(OpenApiSchemaCatalog.WorkerSummary())))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/v1.0/workers/{id}",
                ReadAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Read worker", "Workers")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.WorkerSummary()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST,
                "/v1.0/workers/{id}/drain",
                DrainAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Drain worker", "Workers")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.WorkerSummary()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST,
                "/v1.0/workers/{id}/resume",
                ResumeAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Resume worker", "Workers")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.WorkerSummary()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST,
                "/v1.0/workers/{id}/block",
                BlockAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Block worker", "Workers")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.WorkerSummary()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST,
                "/v1.0/workers/{id}/unblock",
                UnblockAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Unblock worker", "Workers")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.WorkerSummary()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.POST,
                "/v1.0/workers/{id}/rotate-token",
                RotateTokenAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Rotate worker token", "Workers")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.WorkerTokenIssueResult()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            server.WebSocket("/v1.0/workers/connect", HandleWebSocketAsync);
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            List<WorkerRecord> workers = await _Host.DispatchCoordinator.ListWorkersAsync().ConfigureAwait(false);
            Dictionary<string, int> assignmentCounts = await _Host.DispatchCoordinator.ReadActiveAssignmentCountsAsync().ConfigureAwait(false);

            string? state = RouteHelpers.Query(ctx, "state");
            string? search = RouteHelpers.Query(ctx, "search");
            bool? drainMode = ParseNullableBool(RouteHelpers.Query(ctx, "drainMode"));
            bool? enabled = ParseNullableBool(RouteHelpers.Query(ctx, "enabled"));

            IEnumerable<WorkerRecord> filtered = workers;
            if (!string.IsNullOrWhiteSpace(state))
            {
                filtered = filtered.Where(worker => string.Equals(worker.State, state.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (drainMode.HasValue)
            {
                filtered = filtered.Where(worker => worker.DrainMode == drainMode.Value);
            }
            if (enabled.HasValue)
            {
                filtered = filtered.Where(worker => worker.Enabled == enabled.Value);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                filtered = filtered.Where(worker =>
                    Contains(worker.Id, term) ||
                    Contains(worker.Name, term) ||
                    Contains(worker.HostName, term));
            }

            List<WorkerRecord> materialized = filtered.ToList();
            int pageNumber = Math.Max(1, RouteHelpers.QueryInt(ctx, "pageNumber", 1));
            int pageSize = Math.Max(1, RouteHelpers.QueryInt(ctx, "pageSize", 25));

            EnumerationResult<WorkerSummaryResponse> response = new EnumerationResult<WorkerSummaryResponse>
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = materialized.Count
            };

            foreach (WorkerRecord worker in materialized.Skip((pageNumber - 1) * pageSize).Take(pageSize))
            {
                response.Items.Add(await BuildWorkerSummaryAsync(worker, assignmentCounts).ConfigureAwait(false));
            }

            await RouteHelpers.JsonAsync(ctx, 200, response).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? workerId = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrWhiteSpace(workerId))
            {
                await RouteHelpers.BadRequestAsync(ctx, "id required.").ConfigureAwait(false);
                return;
            }

            WorkerRecord? worker = await _Host.DispatchCoordinator.ReadWorkerAsync(workerId).ConfigureAwait(false);
            if (worker == null)
            {
                await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false);
                return;
            }

            Dictionary<string, int> assignmentCounts = await _Host.DispatchCoordinator.ReadActiveAssignmentCountsAsync().ConfigureAwait(false);
            WorkerSummaryResponse response = await BuildWorkerSummaryAsync(worker, assignmentCounts).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, response).ConfigureAwait(false);
        }

        private async Task DrainAsync(HttpContextBase ctx)
        {
            await SetDrainModeAsync(ctx, true).ConfigureAwait(false);
        }

        private async Task ResumeAsync(HttpContextBase ctx)
        {
            await SetDrainModeAsync(ctx, false).ConfigureAwait(false);
        }

        private async Task BlockAsync(HttpContextBase ctx)
        {
            await SetEnabledAsync(ctx, false).ConfigureAwait(false);
        }

        private async Task UnblockAsync(HttpContextBase ctx)
        {
            await SetEnabledAsync(ctx, true).ConfigureAwait(false);
        }

        private async Task RotateTokenAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? workerId = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrWhiteSpace(workerId))
            {
                await RouteHelpers.BadRequestAsync(ctx, "id required.").ConfigureAwait(false);
                return;
            }

            WorkerTokenIssueResult token = await _Host.DispatchCoordinator.RotateWorkerTokenAsync(workerId).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, token).ConfigureAwait(false);
        }

        private async Task SetDrainModeAsync(HttpContextBase ctx, bool drainMode)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? workerId = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrWhiteSpace(workerId))
            {
                await RouteHelpers.BadRequestAsync(ctx, "id required.").ConfigureAwait(false);
                return;
            }

            bool updated = await _Host.DispatchCoordinator.SetWorkerDrainModeAsync(workerId, drainMode).ConfigureAwait(false);
            if (!updated)
            {
                await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false);
                return;
            }

            WorkerRecord? worker = await _Host.DispatchCoordinator.ReadWorkerAsync(workerId).ConfigureAwait(false);
            Dictionary<string, int> assignmentCounts = await _Host.DispatchCoordinator.ReadActiveAssignmentCountsAsync().ConfigureAwait(false);
            WorkerSummaryResponse response = await BuildWorkerSummaryAsync(worker!, assignmentCounts).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, response).ConfigureAwait(false);
        }

        private async Task SetEnabledAsync(HttpContextBase ctx, bool enabled)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? workerId = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrWhiteSpace(workerId))
            {
                await RouteHelpers.BadRequestAsync(ctx, "id required.").ConfigureAwait(false);
                return;
            }

            bool updated = await _Host.DispatchCoordinator.SetWorkerEnabledAsync(workerId, enabled).ConfigureAwait(false);
            if (!updated)
            {
                await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false);
                return;
            }

            WorkerRecord? worker = await _Host.DispatchCoordinator.ReadWorkerAsync(workerId).ConfigureAwait(false);
            Dictionary<string, int> assignmentCounts = await _Host.DispatchCoordinator.ReadActiveAssignmentCountsAsync().ConfigureAwait(false);
            WorkerSummaryResponse response = await BuildWorkerSummaryAsync(worker!, assignmentCounts).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, response).ConfigureAwait(false);
        }

        private async Task HandleWebSocketAsync(HttpContextBase ctx, WebSocketSession session)
        {
            string? workerSessionId = null;
            bool registered = false;

            try
            {
                WorkerRecord? worker = await AuthenticateWorkerAsync(ctx).ConfigureAwait(false);
                if (worker == null)
                {
                    await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "worker authentication failed", CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                WebSocketMessage? helloFrame = await session.ReceiveAsync(CancellationToken.None).ConfigureAwait(false);
                if (helloFrame == null || helloFrame.MessageType != WebSocketMessageType.Text)
                {
                    await session.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "expected hello frame", CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                WorkerHelloMessage? hello = TryDeserialize<WorkerHelloMessage>(helloFrame.Text);
                if (hello == null || !string.Equals(hello.Type, WorkerFrameTypes.Hello, StringComparison.Ordinal))
                {
                    await session.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "invalid hello frame", CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                WorkerHelloAckMessage ack = await _Host.DispatchCoordinator.RegisterWorkerAsync(worker, hello, session).ConfigureAwait(false);
                workerSessionId = ack.WorkerSessionId;
                registered = true;
                await session.SendTextAsync(JsonSerializer.Serialize(ack, WorkerProtocolSerialization.Options), CancellationToken.None).ConfigureAwait(false);

                await foreach (WebSocketMessage frame in session.ReadMessagesAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    if (frame == null || frame.MessageType != WebSocketMessageType.Text) continue;
                    if (!await HandleWorkerFrameAsync(frame.Text).ConfigureAwait(false))
                    {
                        await session.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "invalid worker frame", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore shutdown.
            }
            catch (Exception ex)
            {
                _Host.Logger.Warn("[WorkerRoutes] worker websocket failed: " + ex.Message);
                try
                {
                    await session.CloseAsync(WebSocketCloseStatus.InternalServerError, "worker connection failed", CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore close failures.
                }
            }
            finally
            {
                if (registered && !string.IsNullOrWhiteSpace(workerSessionId))
                {
                    string reason = session.CloseStatusDescription
                        ?? session.CloseStatus?.ToString()
                        ?? "socket_closed";
                    try
                    {
                        await _Host.DispatchCoordinator.UnregisterWorkerSessionAsync(workerSessionId, reason).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore disconnect cleanup failures.
                    }
                }
            }
        }

        private async Task<bool> HandleWorkerFrameAsync(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("type", out JsonElement typeElement)) return false;
                string? type = typeElement.GetString();

                switch (type)
                {
                    case WorkerFrameTypes.Heartbeat:
                    {
                        WorkerHeartbeatMessage? heartbeat = JsonSerializer.Deserialize<WorkerHeartbeatMessage>(json, WorkerProtocolSerialization.Options);
                        return heartbeat != null && await _Host.DispatchCoordinator.HandleWorkerHeartbeatAsync(heartbeat).ConfigureAwait(false);
                    }
                    case WorkerFrameTypes.AssignAck:
                    {
                        WorkerAssignAckMessage? ack = JsonSerializer.Deserialize<WorkerAssignAckMessage>(json, WorkerProtocolSerialization.Options);
                        return ack != null && await _Host.DispatchCoordinator.HandleWorkerAssignAckAsync(ack).ConfigureAwait(false);
                    }
                    case WorkerFrameTypes.RunCompleted:
                    {
                        WorkerRunCompletedMessage? completed = JsonSerializer.Deserialize<WorkerRunCompletedMessage>(json, WorkerProtocolSerialization.Options);
                        if (completed?.Completion == null) return false;
                        await _Host.Dispatch.HandleCompletionAsync(completed.Completion).ConfigureAwait(false);
                        return true;
                    }
                    default:
                        return false;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private async Task<bool> RequireAdminAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated)
            {
                await RouteHelpers.UnauthorizedAsync(ctx).ConfigureAwait(false);
                return false;
            }
            if (!rc.IsAdmin)
            {
                await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false);
                return false;
            }
            return true;
        }

        private async Task<WorkerRecord?> AuthenticateWorkerAsync(HttpContextBase ctx)
        {
            string? workerId = ctx.Request.Headers[Constants.HeaderWorkerId];
            string? workerToken = ctx.Request.Headers[Constants.HeaderWorkerToken];
            return await _Host.DispatchCoordinator.AuthenticateWorkerAsync(workerId ?? string.Empty, workerToken ?? string.Empty).ConfigureAwait(false);
        }

        private async Task<WorkerSummaryResponse> BuildWorkerSummaryAsync(WorkerRecord worker, IDictionary<string, int> assignmentCounts)
        {
            WorkerSessionRecord? latestSession = await _Host.DispatchCoordinator.ReadLatestWorkerSessionAsync(worker.Id).ConfigureAwait(false);
            return new WorkerSummaryResponse
            {
                Id = worker.Id,
                Name = worker.Name,
                Kind = worker.Kind,
                State = worker.State,
                Enabled = worker.Enabled,
                DrainMode = worker.DrainMode,
                Version = worker.Version,
                HostName = worker.HostName,
                Labels = WorkerDescriptorJson.DeserializeLabels(worker.LabelsJson),
                Capabilities = WorkerDescriptorJson.DeserializeCapabilities(worker.CapabilitiesJson),
                MaxConcurrentRuns = worker.MaxConcurrentRuns,
                MaxTaskTimeoutMs = worker.MaxTaskTimeoutMs,
                ActiveAssignmentCount = assignmentCounts.TryGetValue(worker.Id, out int activeCount) ? activeCount : 0,
                TokenLastRotatedUtc = worker.TokenLastRotatedUtc,
                LastHeartbeatUtc = worker.LastHeartbeatUtc,
                CreatedUtc = worker.CreatedUtc,
                LatestSession = latestSession == null
                    ? null
                    : new WorkerSessionResponse
                    {
                        Id = latestSession.Id,
                        WorkerId = latestSession.WorkerId,
                        ConnectedUtc = latestSession.ConnectedUtc,
                        DisconnectedUtc = latestSession.DisconnectedUtc,
                        DisconnectReason = latestSession.DisconnectReason,
                        ProtocolVersion = latestSession.ProtocolVersion
                    }
            };
        }

        private static T? TryDeserialize<T>(string? json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<T>(json, WorkerProtocolSerialization.Options);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool Contains(string? value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool? ParseNullableBool(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1") return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0") return false;
            return null;
        }
    }
}
