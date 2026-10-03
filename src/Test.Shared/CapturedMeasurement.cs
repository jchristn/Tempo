namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One metric measurement observed by <see cref="TelemetryCapture"/>.
    /// </summary>
    public sealed class CapturedMeasurement
    {
        /// <summary>Instrument name (for example tempo.flow.runs).</summary>
        public string Instrument { get; }

        /// <summary>Measured value.</summary>
        public double Value { get; }

        /// <summary>Measurement tags keyed by label name. Values are rendered with invariant culture.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }

        /// <summary>Instantiate.</summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="value">Measured value.</param>
        /// <param name="tags">Measurement tags.</param>
        public CapturedMeasurement(string instrument, double value, IReadOnlyDictionary<string, string> tags)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            Value = value;
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        }

        /// <summary>Whether every <c>key=value</c> filter matches this measurement's tags.</summary>
        /// <param name="filters">Filters in <c>key=value</c> form.</param>
        /// <returns>True when all filters match.</returns>
        public bool Matches(params string[] filters)
        {
            foreach (string filter in filters)
            {
                int eq = filter.IndexOf('=');
                if (eq < 1) throw new ArgumentException("Filter must be key=value: " + filter, nameof(filters));
                string key = filter.Substring(0, eq);
                string value = filter.Substring(eq + 1);
                if (!Tags.TryGetValue(key, out string? actual) || !String.Equals(actual, value, StringComparison.Ordinal)) return false;
            }

            return true;
        }
    }
}
