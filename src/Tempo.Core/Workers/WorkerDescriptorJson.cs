namespace Tempo.Core.Workers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Tempo.Core.Runtime;

    /// <summary>
    /// JSON helpers and matching rules for worker labels and advertised capabilities.
    /// </summary>
    public static class WorkerDescriptorJson
    {
        /// <summary>Serialize label values as JSON.</summary>
        public static string SerializeLabels(IEnumerable<string>? labels)
        {
            List<string> normalized = NormalizeLabels(labels);
            return JsonSerializer.Serialize(normalized, WorkerProtocolSerialization.Options);
        }

        /// <summary>Deserialize label values from JSON.</summary>
        public static List<string> DeserializeLabels(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return NormalizeLabels(doc.RootElement.EnumerateArray().Select(v => v.GetString()));
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return NormalizeLabels(doc.RootElement.EnumerateObject().Select(p => p.Name));
                }
            }
            catch
            {
                // Fall through to an empty set for malformed persisted values.
            }

            return new List<string>();
        }

        /// <summary>Serialize capabilities as JSON.</summary>
        public static string SerializeCapabilities(IEnumerable<WorkerCapabilityDescriptor>? capabilities)
        {
            return JsonSerializer.Serialize(capabilities ?? Enumerable.Empty<WorkerCapabilityDescriptor>(), WorkerProtocolSerialization.Options);
        }

        /// <summary>Deserialize capabilities from JSON.</summary>
        public static List<WorkerCapabilityDescriptor> DeserializeCapabilities(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<WorkerCapabilityDescriptor>();

            try
            {
                List<WorkerCapabilityDescriptor>? capabilities = JsonSerializer.Deserialize<List<WorkerCapabilityDescriptor>>(json, WorkerProtocolSerialization.Options);
                if (capabilities == null) return new List<WorkerCapabilityDescriptor>();
                return capabilities
                    .Where(c => !string.IsNullOrWhiteSpace(c.SourceKind) && !string.IsNullOrWhiteSpace(c.RuntimeKey))
                    .ToList();
            }
            catch
            {
                return new List<WorkerCapabilityDescriptor>();
            }
        }

        /// <summary>Determine whether a label set contains the requested label.</summary>
        public static bool HasLabel(string? labelsJson, string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return true;
            List<string> labels = DeserializeLabels(labelsJson);
            return labels.Any(existing => string.Equals(existing, label.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Determine whether the advertised capability set supports the supplied plan.</summary>
        public static bool SupportsPlan(string? capabilitiesJson, FlowRunExecutionPlan? plan)
        {
            if (plan == null) return false;
            List<WorkerCapabilityDescriptor> capabilities = DeserializeCapabilities(capabilitiesJson);
            if (capabilities.Count < 1) return false;

            foreach (FlowRunCapabilityRequirement requirement in plan.RequiredCapabilities)
            {
                bool matched = capabilities.Any(capability => Matches(capability, requirement));
                if (!matched) return false;
            }

            return true;
        }

        /// <summary>Determine whether one advertised capability satisfies a requirement.</summary>
        public static bool Matches(WorkerCapabilityDescriptor capability, FlowRunCapabilityRequirement requirement)
        {
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            if (requirement == null) throw new ArgumentNullException(nameof(requirement));

            if (!MatchesValue(capability.SourceKind, requirement.SourceKind)) return false;
            if (!MatchesValue(capability.RuntimeKey, requirement.RuntimeKey.ToString())) return false;
            if (!MatchesTenant(capability.TenantScope, requirement.TenantScope)) return false;
            if (!MatchesValue(capability.SignatureHash, requirement.SignatureHash)) return false;
            return true;
        }

        private static bool MatchesTenant(string? advertised, string? required)
        {
            if (string.IsNullOrWhiteSpace(advertised) || advertised == "*") return true;
            if (string.IsNullOrWhiteSpace(required)) return true;
            return string.Equals(advertised.Trim(), required.Trim(), StringComparison.Ordinal);
        }

        private static bool MatchesValue(string? advertised, string? required)
        {
            if (string.IsNullOrWhiteSpace(advertised) || advertised == "*") return true;
            if (string.IsNullOrWhiteSpace(required)) return false;
            return string.Equals(advertised.Trim(), required.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> NormalizeLabels(IEnumerable<string?>? labels)
        {
            if (labels == null) return new List<string>();

            return labels
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => label!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
