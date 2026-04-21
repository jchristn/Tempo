namespace Tempo.Protocol
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Enums;

    /// <summary>Minimal stdin/stdout host for .NET process-backed Tempo step artifacts.</summary>
    public static class TempoStepHost
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Run a handler by reading one <see cref="StepRequest"/> from stdin and writing one <see cref="StepResult"/> to stdout.</summary>
        public static async Task<int> RunAsync(ITempoStepHandler handler, CancellationToken token = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            StepRequest? request = null;
            try
            {
                string input = await Console.In.ReadToEndAsync(token).ConfigureAwait(false);
                request = JsonSerializer.Deserialize<StepRequest>(input, _Json);
                if (request == null) throw new InvalidOperationException("stdin did not contain a StepRequest.");
                StepResult result = await handler.RunAsync(request, token).ConfigureAwait(false);
                WriteResult(Correlate(result, request));
                return 0;
            }
            catch (Exception ex)
            {
                WriteResult(ExceptionResult(request, ex));
                return 0;
            }
        }

        /// <summary>Create a successful result correlated to the request.</summary>
        public static StepResult Success(StepRequest request, object? data, object? metadata = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new StepResult
            {
                ProtocolVersion = request.ProtocolVersion,
                TenantId = request.TenantId,
                DataFlowId = request.DataFlowId,
                FlowRunId = request.FlowRunId,
                StepRunId = request.StepRunId,
                RequestId = request.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = data!,
                Metadata = (metadata ?? request.Metadata)!
            };
        }

        /// <summary>Create an error result correlated to the request.</summary>
        public static StepResult Error(StepRequest request, object? data, object? metadata = null)
        {
            StepResult result = Success(request, data, metadata);
            result.Result = StepResultTypeEnum.Error;
            return result;
        }

        /// <summary>Create an exception result correlated to the request.</summary>
        public static StepResult Exception(StepRequest? request, Exception exception, object? metadata = null)
        {
            return ExceptionResult(request, exception, metadata);
        }

        private static StepResult Correlate(StepResult result, StepRequest request)
        {
            result.ProtocolVersion = request.ProtocolVersion;
            result.TenantId = request.TenantId;
            result.DataFlowId = request.DataFlowId;
            result.FlowRunId = request.FlowRunId;
            result.StepRunId = request.StepRunId;
            result.RequestId = request.RequestId;
            return result;
        }

        private static StepResult ExceptionResult(StepRequest? request, Exception exception, object? metadata = null)
        {
            return new StepResult
            {
                ProtocolVersion = request?.ProtocolVersion ?? ProtocolVersions.Current,
                TenantId = request?.TenantId,
                DataFlowId = request?.DataFlowId ?? "unknown",
                FlowRunId = request?.FlowRunId,
                StepRunId = request?.StepRunId,
                RequestId = request?.RequestId ?? TempoIds.GenerateRequestId(),
                Result = StepResultTypeEnum.Exception,
                ExceptionMessage = exception.Message,
                Metadata = (metadata ?? request?.Metadata)!
            };
        }

        private static void WriteResult(StepResult result)
        {
            Console.Out.Write(JsonSerializer.Serialize(result, _Json));
        }
    }
}
