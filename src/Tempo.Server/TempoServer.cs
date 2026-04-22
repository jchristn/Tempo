namespace Tempo.Server
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Tempo.Core.Runtime;
    using Tempo.Server.Helpers;
    using Tempo.Server.Routes;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using TempoRunners = Tempo.Runners;
    using TempoStepManager = Tempo.StepManager;

    /// <summary>
    /// Tempo server host. Owns the Watson webserver, service stack, and run dispatch coordinator.
    /// </summary>
    public class TempoServer : IDisposable
    {
        private readonly Settings _Settings;
        private readonly LoggingModule _Logging;
        private readonly DatabaseDriverBase _Database;
        private readonly TokenService _Tokens;
        private readonly AuthenticationService _Auth;
        private readonly AuthorizationService _Authz;
        private readonly RequestHistoryCaptureService _Capture;
        private readonly IRunDispatchCoordinator _Dispatch;
        private readonly DeletionDependencyService _DeleteGuard;
        private readonly StepRuntimeRegistry _RuntimeRegistry;
        private readonly ExternalRuntimeCapacityManager _ExternalCapacity;
        private readonly IArtifactBlobStore _ArtifactBlobStore;
        private readonly ArtifactRetentionService _ArtifactRetention;
        private readonly TempoStepManager _StepManager;
        private readonly Tempo.Server.Services.LogFileService _LogFiles;
        private readonly RunLogService _RunLogs;
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();
        private readonly string _Header = "[TempoServer] ";
        private readonly Tempo.Server.Services.SettingsStore _SettingsStore;
        private Webserver? _Server = null;
        private Task? _PruneTask = null;
        private Task? _ArtifactGcTask = null;
        private Task? _RunLogPruneTask = null;
        private bool _Disposed = false;

        /// <summary>Instantiate.</summary>
        public TempoServer(
            Settings settings,
            LoggingModule logging,
            DatabaseDriverBase database,
            TempoStepManager stepManager,
            Tempo.Server.Services.SettingsStore? settingsStore = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _StepManager = stepManager ?? throw new ArgumentNullException(nameof(stepManager));
            _SettingsStore = settingsStore ?? new Tempo.Server.Services.SettingsStore(_Settings, Tempo.Core.Constants.DefaultSettingsFile);

            _Tokens = new TokenService(_Settings.Auth);
            _Auth = new AuthenticationService(_Database, _Tokens, _Settings.Auth);
            _Authz = new AuthorizationService(_Database);
            _Capture = new RequestHistoryCaptureService(_Database, _Settings.RequestHistory, _Logging);
            _DeleteGuard = new DeletionDependencyService(_Database);
            _ExternalCapacity = new ExternalRuntimeCapacityManager(_Settings.Runtimes.ExternalExecution);
            _ArtifactBlobStore = new LocalFilesystemArtifactBlobStore(_Settings.Artifacts);
            _RuntimeRegistry = StepRuntimeRegistry.CreateDefault(_StepManager, runtimes: _Settings.Runtimes, database: _Database, artifactBlobStore: _ArtifactBlobStore, externalCapacity: _ExternalCapacity);
            _ArtifactRetention = new ArtifactRetentionService(_Database, _ArtifactBlobStore, _Settings.Artifacts, _Settings.Runtimes.ExternalExecution);
            _RunLogs = new RunLogService(_Settings.RunLogs);
            _Dispatch = new Tempo.Server.Services.RunDispatchCoordinator(_Database, _StepManager, _Settings.Engine, _Logging, _RuntimeRegistry, runLogSettings: _Settings.RunLogs);
            _LogFiles = new Tempo.Server.Services.LogFileService(_SettingsStore, (Tempo.Server.Services.RunDispatchCoordinator)_Dispatch);
        }

        /// <summary>Settings store for live edit.</summary>
        public Tempo.Server.Services.SettingsStore SettingsStore => _SettingsStore;

        /// <summary>Expose the authentication service for route registrars.</summary>
        public AuthenticationService Authentication => _Auth;

        /// <summary>Expose the authorization service for route registrars.</summary>
        public AuthorizationService Authorization => _Authz;

        /// <summary>Expose the token service for route registrars.</summary>
        public TokenService Tokens => _Tokens;

        /// <summary>Expose the run dispatch coordinator for route registrars.</summary>
        public IRunDispatchCoordinator Dispatch => _Dispatch;

        /// <summary>Expose the concrete dispatch coordinator for worker-management routes.</summary>
        public Tempo.Server.Services.RunDispatchCoordinator DispatchCoordinator => (Tempo.Server.Services.RunDispatchCoordinator)_Dispatch;

        /// <summary>Expose delete dependency checks for route registrars.</summary>
        public DeletionDependencyService DeleteGuard => _DeleteGuard;

        /// <summary>Expose the runtime registry for route registrars.</summary>
        public StepRuntimeRegistry RuntimeRegistry => _RuntimeRegistry;

        /// <summary>Expose external runtime capacity counters for route registrars.</summary>
        public ExternalRuntimeCapacityManager ExternalCapacity => _ExternalCapacity;

        /// <summary>Expose artifact blob storage for route registrars.</summary>
        public IArtifactBlobStore ArtifactBlobStore => _ArtifactBlobStore;

        /// <summary>Expose artifact retention/GC operations for route registrars.</summary>
        public ArtifactRetentionService ArtifactRetention => _ArtifactRetention;

        /// <summary>Expose the database for route registrars.</summary>
        public DatabaseDriverBase Database => _Database;

        /// <summary>Expose the logger.</summary>
        public LoggingModule Logger => _Logging;

        /// <summary>Expose the step manager.</summary>
        public TempoStepManager StepManager => _StepManager;

        /// <summary>Expose the settings.</summary>
        public Settings Settings => _Settings;

        /// <summary>Expose the log catalog service.</summary>
        public Tempo.Server.Services.LogFileService LogFiles => _LogFiles;

        /// <summary>Expose the tenant-scoped run-log service.</summary>
        public RunLogService RunLogs => _RunLogs;

        /// <summary>Start the server.</summary>
        public Task StartAsync()
        {
            WebserverSettings ws = new WebserverSettings();
            ws.Hostname = _Settings.Rest.Hostname;
            ws.Port = _Settings.Rest.Port;
            ws.Ssl.Enable = _Settings.Rest.Ssl;
            ws.WebSockets.Enable = true;

            _Server = new Webserver(ws, DefaultRouteAsync);

            _Server.Routes.AuthenticateRequest = AuthenticateRequestAsync;
            _Server.Routes.Preflight = PreflightAsync;
            _Server.Routes.PostRouting = PostRoutingAsync;
            _Server.Routes.Exception = ExceptionRouteAsync;

            _Server.UseOpenApi(oa =>
            {
                oa.Info.Title = "Tempo API";
                oa.Info.Version = "v1.0";
                oa.Info.Description = "Tempo data flow orchestration platform.";
                OpenApiSchemaCatalog.RegisterSchemas(oa);
            });

            RegisterRoutes();

            _Server.Start();
            _Logging.Info(_Header + "listening on http" + (_Settings.Rest.Ssl ? "s" : "") + "://" + _Settings.Rest.Hostname + ":" + _Settings.Rest.Port);
            _Logging.Warn(LogMessages.WithoutTerminalPeriod(_Header + "process-backed artifact execution is available; enforce tenant isolation, quotas, and runtime allowlists before accepting untrusted code"));

            _Dispatch.Start();
            _PruneTask = Task.Run(() => PruneLoopAsync(_Cts.Token));
            _ArtifactGcTask = Task.Run(() => ArtifactGcLoopAsync(_Cts.Token));
            _RunLogPruneTask = Task.Run(() => RunLogPruneLoopAsync(_Cts.Token));

            return Task.CompletedTask;
        }

        /// <summary>Stop the server.</summary>
        public void Stop()
        {
            try { _Cts.Cancel(); } catch { /* ignore */ }
            try { _Server?.Stop(); } catch { /* ignore */ }
            _Dispatch.Stop();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_Disposed) return;
            Stop();
            try { _PruneTask?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
            try { _ArtifactGcTask?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
            try { _RunLogPruneTask?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
            _Dispatch.Dispose();
            try { _Server?.Dispose(); } catch { /* ignore */ }
            _Cts.Dispose();
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        private void RegisterRoutes()
        {
            if (_Server == null) return;

            new HealthRoutes().Register(_Server);
            new SampleRoutes().Register(_Server);
            new AuthRoutes(this).Register(_Server);
            new AccountRoutes(this).Register(_Server);
            new AdminRoutes(this).Register(_Server);
            new TenantRoutes(this).Register(_Server);
            new UserRoutes(this).Register(_Server);
            new CredentialRoutes(this).Register(_Server);
            new RoleRoutes(this).Register(_Server);
            new PermissionRoutes(this).Register(_Server);
            new DataFlowRoutes(this).Register(_Server);
            new StepRoutes(this).Register(_Server);
            new RuntimeRoutes(this).Register(_Server);
            new ArtifactRoutes(this).Register(_Server);
            new WorkerRoutes(this).Register(_Server);
            new TriggerRoutes(this).Register(_Server);
            new FlowRunRoutes(this).Register(_Server);
            new RequestHistoryRoutes(this).Register(_Server);
            new MigrationRoutes(this).Register(_Server);
            new SettingsRoutes(this).Register(_Server);
            new LogRoutes(this).Register(_Server);
        }

        private async Task AuthenticateRequestAsync(HttpContextBase ctx)
        {
            string? tokenHeader = ctx.Request.Headers[Constants.HeaderToken];
            string? apiKey = ctx.Request.Headers[Constants.HeaderApiKey];
            string? accessKey = ctx.Request.Headers[Constants.HeaderAccessKey];
            string? secretKey = ctx.Request.Headers[Constants.HeaderSecretKey];
            string? tenantIdHeader = ctx.Request.Headers[Constants.HeaderTenantId];
            string? email = ctx.Request.Headers[Constants.HeaderEmail];
            string? password = ctx.Request.Headers[Constants.HeaderPassword];
            string? authz = ctx.Request.Headers["Authorization"];

            string? bearerToken = null;
            if (!string.IsNullOrEmpty(authz) && authz.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                bearerToken = authz.Substring(7).Trim();
            }

            RequestContext rc = await _Auth.AuthenticateAsync(
                tokenHeader, bearerToken, apiKey, accessKey, secretKey, tenantIdHeader, email, password).ConfigureAwait(false);

            ctx.Metadata = rc;
        }

        private async Task PreflightAsync(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ApplyCors(ctx);
            ctx.Response.Headers.Add("Access-Control-Max-Age", "86400");
            await ctx.Response.Send().ConfigureAwait(false);
        }

        private Task PostRoutingAsync(HttpContextBase ctx)
        {
            ctx.Timestamp.End = DateTime.UtcNow;
            ApplyCors(ctx);

            _Logging.Debug(_Header +
                ctx.Request.Method + " " +
                ctx.Request.Url.RawWithQuery + " " +
                ctx.Response.StatusCode + " (" +
                (ctx.Timestamp.TotalMs.HasValue ? ctx.Timestamp.TotalMs.Value.ToString("F2") : "?") + "ms)");

            if (_Settings.RequestHistory.Enabled)
            {
                RequestHistoryEntry entry = BuildHistoryEntry(ctx);
                _Capture.Capture(entry);
            }

            return Task.CompletedTask;
        }

        private Task DefaultRouteAsync(HttpContextBase ctx)
        {
            return RouteHelpers.NotFoundAsync(ctx, "No route matched '" + ctx.Request.Method + " " + ctx.Request.Url.RawWithoutQuery + "'.");
        }

        private async Task ExceptionRouteAsync(HttpContextBase ctx, Exception ex)
        {
            _Logging.Warn(LogMessages.WithoutTerminalPeriod(_Header + "unhandled exception: " + ex.Message));
            await RouteHelpers.ErrorAsync(ctx, 500, "InternalError", ex.Message).ConfigureAwait(false);
        }

        private static void ApplyCors(HttpContextBase ctx)
        {
            SetHeaderIfMissing(ctx, "Access-Control-Allow-Origin", "*");
            SetHeaderIfMissing(ctx, "Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            SetHeaderIfMissing(ctx, "Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key, X-Token, X-Tenant-Id, X-Worker-Id, X-Worker-Token, X-Access-Key, X-Secret-Key, X-Email, X-Password, X-Request");
            SetHeaderIfMissing(ctx, "Access-Control-Expose-Headers", Constants.HeaderRunMetadataExposeList);
        }

        private static void SetHeaderIfMissing(HttpContextBase ctx, string key, string value)
        {
            System.Collections.Specialized.NameValueCollection headers = ctx.Response.Headers;
            foreach (string? existing in headers.AllKeys)
            {
                if (string.Equals(existing, key, System.StringComparison.OrdinalIgnoreCase)) return;
            }
            headers.Add(key, value);
        }

        private RequestHistoryEntry BuildHistoryEntry(HttpContextBase ctx)
        {
            RequestContext? rc = ctx.Metadata as RequestContext;

            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                TenantId = rc?.TenantId,
                UserId = rc?.UserId,
                PrincipalName = rc?.PrincipalName,
                Method = ctx.Request.Method.ToString(),
                Path = ctx.Request.Url.RawWithoutQuery,
                Url = ctx.Request.Url.RawWithQuery,
                StatusCode = ctx.Response.StatusCode,
                DurationMs = ctx.Timestamp.TotalMs ?? 0,
                SourceIp = ClientIpResolver.Resolve(ctx),
                CreatedUtc = ctx.Timestamp.Start,
                CompletedUtc = ctx.Timestamp.End
            };

            foreach (string? key in ctx.Request.Headers.AllKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                entry.RequestHeaders[key] = ctx.Request.Headers[key] ?? string.Empty;
            }
            foreach (string? key in ctx.Response.Headers.AllKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                entry.ResponseHeaders[key] = ctx.Response.Headers[key] ?? string.Empty;
            }

            string? responseContentType = GetResponseContentType(ctx);
            if (!string.IsNullOrWhiteSpace(responseContentType) && !entry.ResponseHeaders.ContainsKey("Content-Type"))
            {
                entry.ResponseHeaders["Content-Type"] = responseContentType;
            }

            if (ctx.Response.ContentLength > 0 && !entry.ResponseHeaders.ContainsKey("Content-Length"))
            {
                entry.ResponseHeaders["Content-Length"] = ctx.Response.ContentLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrEmpty(ctx.Request.DataAsString)) entry.RequestBody = ctx.Request.DataAsString;
            entry.ResponseBody = ReadResponseBody(ctx, responseContentType);

            return entry;
        }

        private static string? ReadResponseBody(HttpContextBase ctx, string? responseContentType)
        {
            if (!ShouldCaptureResponseBody(ctx, responseContentType)) return null;

            byte[]? responseBytes = ctx.Response.DataAsBytes;
            if (responseBytes == null || responseBytes.Length < 1) return null;
            return ctx.Response.DataAsString;
        }

        private static bool ShouldCaptureResponseBody(HttpContextBase ctx, string? responseContentType)
        {
            if (ctx.Response.StatusCode == 204 || ctx.Response.StatusCode == 304) return false;
            if (string.Equals(ctx.Request.Method.ToString(), "HEAD", StringComparison.OrdinalIgnoreCase)) return false;
            return IsTextLikeContentType(responseContentType);
        }

        private static string? GetResponseContentType(HttpContextBase ctx)
        {
            if (!string.IsNullOrWhiteSpace(ctx.Response.ContentType)) return ctx.Response.ContentType;
            return TryGetHeaderValue(ctx.Response.Headers, "Content-Type");
        }

        private static string? TryGetHeaderValue(System.Collections.Specialized.NameValueCollection headers, string key)
        {
            foreach (string? existing in headers.AllKeys)
            {
                if (string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
                {
                    return headers[existing];
                }
            }

            return null;
        }

        private static bool IsTextLikeContentType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return false;

            string normalized = contentType.Split(';')[0].Trim();
            if (normalized.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.EndsWith("+json", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.Equals("application/xml", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.Equals("text/xml", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private async Task PruneLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_Settings.RequestHistory.PruneIntervalMinutes), token).ConfigureAwait(false);
                    DateTime cutoff = DateTime.UtcNow.AddDays(-_Settings.RequestHistory.RetentionDays);
                    int deleted = await _Database.RequestHistory.PruneAsync(cutoff, token).ConfigureAwait(false);
                    if (deleted > 0) _Logging.Debug(_Header + "pruned " + deleted + " request history rows older than " + cutoff.ToString("o"));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _Logging.Warn(LogMessages.WithoutTerminalPeriod(_Header + "prune loop error: " + ex.Message)); }
            }
        }

        private async Task ArtifactGcLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_Settings.Artifacts.GcIntervalMinutes), token).ConfigureAwait(false);
                    ArtifactGcResult result = await _ArtifactRetention.RunOnceAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                    if (result.VersionsMarked > 0 || result.VersionsDeleted > 0 || result.BlobsDeleted > 0 || result.Errors.Count > 0)
                    {
                        _Logging.Debug(_Header + "artifact GC scanned " + result.VersionsScanned + " version(s), marked " + result.VersionsMarked +
                            ", deleted " + result.VersionsDeleted + ", blobs " + result.BlobsDeleted + ", bytes " + result.BytesDeleted +
                            ", errors " + result.Errors.Count);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _Logging.Warn(LogMessages.WithoutTerminalPeriod(_Header + "artifact GC loop error: " + ex.Message)); }
            }
        }

        private async Task RunLogPruneLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_Settings.RunLogs.PruneIntervalMinutes), token).ConfigureAwait(false);
                    int deleted = await PruneRunLogsOnceAsync(token).ConfigureAwait(false);
                    if (deleted > 0)
                    {
                        DateTime cutoff = DateTime.UtcNow.AddDays(-_Settings.RunLogs.RetentionDays);
                        _Logging.Debug(_Header + "pruned " + deleted + " run-log director" + (deleted == 1 ? "y" : "ies") + " older than " + cutoff.ToString("o"));
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _Logging.Warn(LogMessages.WithoutTerminalPeriod(_Header + "run-log prune loop error: " + ex.Message)); }
            }
        }

        /// <summary>Prune completed run-log directories older than the configured retention window.</summary>
        public async Task<int> PruneRunLogsOnceAsync(CancellationToken token = default)
        {
            if (!_Settings.RunLogs.Enabled) return 0;

            DateTime cutoff = DateTime.UtcNow.AddDays(-_Settings.RunLogs.RetentionDays);
            int deleted = 0;

            foreach (string runId in _RunLogs.EnumerateRunIds())
            {
                token.ThrowIfCancellationRequested();

                FlowRun? run = await _Database.FlowRuns.ReadGlobalAsync(runId, token).ConfigureAwait(false);
                if (run != null)
                {
                    if (!run.CompletedUtc.HasValue) continue;
                    if (run.State == FlowRunStateEnum.Queued || run.State == FlowRunStateEnum.Running) continue;
                    if (run.CompletedUtc.Value > cutoff) continue;
                }
                else
                {
                    string runRoot = _RunLogs.ResolveRunRoot(runId);
                    if (Directory.Exists(runRoot) && Directory.GetLastWriteTimeUtc(runRoot) > cutoff) continue;
                }

                await _RunLogs.DeleteRunDirectoryAsync(runId, token).ConfigureAwait(false);
                deleted++;
            }

            return deleted;
        }
    }
}
