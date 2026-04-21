namespace Tempo.Core.Protocol
{
    using System;
    using System.Text.Json;
    using Tempo.Enums;
    using Tempo.Protocol;

    /// <summary>Validates Tempo protocol v1 JSON envelopes.</summary>
    public class ProtocolConformanceRunner
    {
        /// <summary>Validate a step request envelope.</summary>
        public ProtocolConformanceResult ValidateStepRequestJson(string json)
        {
            return Validate(json, requireResult: false);
        }

        /// <summary>Validate a step result envelope.</summary>
        public ProtocolConformanceResult ValidateStepResultJson(string json)
        {
            return Validate(json, requireResult: true);
        }

        private static ProtocolConformanceResult Validate(string json, bool requireResult)
        {
            ProtocolConformanceResult result = new ProtocolConformanceResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Errors.Add("Envelope JSON is empty.");
                return result;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                result.Errors.Add("Envelope JSON is invalid: " + ex.Message);
                return result;
            }

            using (doc)
            {
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    result.Errors.Add("Envelope root must be a JSON object.");
                    return result;
                }

                result.ProtocolVersion = GetString(root, "protocolVersion");
                result.DataFlowId = GetString(root, "dataFlowId");
                result.RequestId = GetString(root, "requestId");

                RequireSupportedProtocol(result);
                RequireNonEmpty(result, result.DataFlowId, "dataFlowId");
                RequireNonEmpty(result, result.RequestId, "requestId");

                if (requireResult)
                {
                    result.Result = GetString(root, "result");
                    RequireNonEmpty(result, result.Result, "result");
                    if (!string.IsNullOrWhiteSpace(result.Result) &&
                        !Enum.TryParse(result.Result, ignoreCase: false, out StepResultTypeEnum parsed))
                    {
                        result.Errors.Add("result is not a valid Tempo step result.");
                    }
                    else if (result.Result == StepResultTypeEnum.Exception.ToString() &&
                             (!root.TryGetProperty("exception", out JsonElement ex) || ex.ValueKind == JsonValueKind.Null))
                    {
                        result.Errors.Add("exception results must include exception.");
                    }
                }
            }

            return result;
        }

        private static string? GetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        private static void RequireSupportedProtocol(ProtocolConformanceResult result)
        {
            if (string.IsNullOrWhiteSpace(result.ProtocolVersion))
            {
                result.Errors.Add("protocolVersion is required.");
                return;
            }

            if (!ProtocolVersions.IsSupported(result.ProtocolVersion))
                result.Errors.Add("protocolVersion is not supported.");
        }

        private static void RequireNonEmpty(ProtocolConformanceResult result, string? value, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(value)) result.Errors.Add(propertyName + " is required.");
        }
    }
}
