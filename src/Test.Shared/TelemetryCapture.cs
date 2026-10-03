namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Telemetry;

    /// <summary>
    /// In-memory telemetry listener for tests: subscribes to the Tempo (and Watson) meters and activity sources
    /// with plain BCL listeners, so assertions need no collector, exporter, or SDK. Thread-safe.
    /// </summary>
    public sealed class TelemetryCapture : IDisposable
    {
        private readonly object _Lock = new object();
        private readonly List<Activity> _Activities = new List<Activity>();
        private readonly List<CapturedMeasurement> _Measurements = new List<CapturedMeasurement>();
        private readonly ActivityListener _ActivityListener;
        private readonly MeterListener _MeterListener;

        /// <summary>Start capturing.</summary>
        public TelemetryCapture()
        {
            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == TelemetryConstants.ActivitySourceName || source.Name == TelemetryConstants.WatsonSourceName || source.Name == TelemetryConstants.HttpClientSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => { lock (_Lock) { _Activities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(_ActivityListener);

            _MeterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == TelemetryConstants.MeterName) listener.EnableMeasurementEvents(instrument);
                }
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Record(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => Record(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Record(instrument, value, tags));
            _MeterListener.Start();
        }

        /// <summary>Snapshot of every stopped span so far.</summary>
        public List<Activity> Activities
        {
            get { lock (_Lock) { return _Activities.ToList(); } }
        }

        /// <summary>Snapshot of every measurement so far.</summary>
        public List<CapturedMeasurement> Measurements
        {
            get { lock (_Lock) { return _Measurements.ToList(); } }
        }

        /// <summary>Collect observable gauges now.</summary>
        public void RecordObservables()
        {
            _MeterListener.RecordObservableInstruments();
        }

        /// <summary>Measurements of one instrument matching every <c>key=value</c> filter.</summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="filters">Tag filters.</param>
        /// <returns>Matching measurements.</returns>
        public List<CapturedMeasurement> Find(string instrument, params string[] filters)
        {
            return Measurements.Where(m => m.Instrument == instrument && m.Matches(filters)).ToList();
        }

        /// <summary>Sum of matching measurement values (for counters and up/down counters).</summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="filters">Tag filters.</param>
        /// <returns>The sum.</returns>
        public double Sum(string instrument, params string[] filters)
        {
            return Find(instrument, filters).Sum(m => m.Value);
        }

        /// <summary>Count of matching measurements (for histograms, the number of recordings).</summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="filters">Tag filters.</param>
        /// <returns>The count.</returns>
        public int Count(string instrument, params string[] filters)
        {
            return Find(instrument, filters).Count;
        }

        /// <summary>Stopped spans with the given operation name.</summary>
        /// <param name="name">Span name.</param>
        /// <returns>Matching spans.</returns>
        public List<Activity> Spans(string name)
        {
            return Activities.Where(a => a.OperationName == name).ToList();
        }

        /// <summary>Wait until a condition over captured telemetry holds, or the timeout elapses.</summary>
        /// <param name="condition">Condition to poll.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="timeoutMs">Timeout in milliseconds. Default: 10000.</param>
        /// <returns>True when the condition held before the timeout.</returns>
        public async Task<bool> WaitForAsync(Func<TelemetryCapture, bool> condition, CancellationToken token, int timeoutMs = 10000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition(this)) return true;
                await Task.Delay(25, token).ConfigureAwait(false);
            }

            return condition(this);
        }

        /// <summary>Stop capturing.</summary>
        public void Dispose()
        {
            _ActivityListener.Dispose();
            _MeterListener.Dispose();
        }

        private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags) where T : struct
        {
            Dictionary<string, string> rendered = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                rendered[tag.Key] = Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            double numeric = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            lock (_Lock)
            {
                _Measurements.Add(new CapturedMeasurement(instrument.Name, numeric, rendered));
            }
        }
    }
}
