namespace Tempo.Sdk
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>stdin/stdout host and result helpers for Tempo step handlers.</summary>
    public static class TempoStepHost
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Deserialize a request envelope from JSON.</summary>
        public static StepRequest DeserializeRequest(string json)
        {
            StepRequest? request = JsonSerializer.Deserialize<StepRequest>(json, _Json);
            return request ?? throw new InvalidOperationException("JSON did not contain a StepRequest.");
        }

        /// <summary>Serialize a result envelope to JSON.</summary>
        public static string SerializeResult(StepResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return JsonSerializer.Serialize(result, _Json);
        }

        /// <summary>Copy host-owned correlation fields from the request to the result.</summary>
        public static StepResult Correlate(StepResult result, StepRequest request)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (request == null) throw new ArgumentNullException(nameof(request));
            result.ProtocolVersion = request.ProtocolVersion;
            result.TenantId = request.TenantId;
            result.DataFlowId = request.DataFlowId;
            result.FlowRunId = request.FlowRunId;
            result.StepRunId = request.StepRunId;
            result.RequestId = request.RequestId;
            return result;
        }

        /// <summary>Create a successful result correlated to the request.</summary>
        public static StepResult Success(StepRequest request, object? data, object? metadata = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Correlate(new StepResult
            {
                Result = StepResultType.Success,
                Data = data,
                Metadata = metadata ?? request.Metadata
            }, request);
        }

        /// <summary>Create an error result correlated to the request.</summary>
        public static StepResult Error(StepRequest request, object? data, object? metadata = null)
        {
            StepResult result = Success(request, data, metadata);
            result.Result = StepResultType.Error;
            return result;
        }

        /// <summary>Create an exception result correlated to the request when available.</summary>
        public static StepResult Exception(StepRequest? request, Exception exception, object? metadata = null)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            StepResult result = new StepResult
            {
                ProtocolVersion = request?.ProtocolVersion ?? ProtocolVersions.Current,
                TenantId = request?.TenantId,
                DataFlowId = request?.DataFlowId ?? "unknown",
                FlowRunId = request?.FlowRunId,
                StepRunId = request?.StepRunId,
                RequestId = request?.RequestId ?? Ids.RequestId(),
                Result = StepResultType.Exception,
                ExceptionMessage = exception.Message,
                Metadata = metadata ?? request?.Metadata
            };
            return result;
        }

        /// <summary>Run a handler by reading one request from stdin and writing one result to stdout.</summary>
        public static async Task<int> RunAsync(ITempoStepHandler handler, TextReader? input = null, TextWriter? output = null, CancellationToken token = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            input ??= Console.In;
            output ??= Console.Out;
            StepRequest? request = null;

            try
            {
                string text = await input.ReadToEndAsync(token).ConfigureAwait(false);
                request = DeserializeRequest(text);
                StepResult result = await handler.RunAsync(request, token).ConfigureAwait(false);
                if (result == null) throw new InvalidOperationException("Handler returned null StepResult.");
                await output.WriteAsync(SerializeResult(Correlate(result, request))).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                await output.WriteAsync(SerializeResult(Exception(request, ex))).ConfigureAwait(false);
                return 0;
            }
        }
    }
}
