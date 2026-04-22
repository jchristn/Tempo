namespace Tempo.Sdk
{
    using System;
    using System.Threading;

    /// <summary>Ambient execution context for the currently running step handler.</summary>
    public sealed class TempoExecutionContext
    {
        private static readonly AsyncLocal<TempoExecutionContext?> _Current = new AsyncLocal<TempoExecutionContext?>();

        /// <summary>Ambient current execution context.</summary>
        public static TempoExecutionContext? Current => _Current.Value;

        /// <summary>Tenant identifier.</summary>
        public string? TenantId { get; init; }

        /// <summary>Data-flow identifier.</summary>
        public string? DataFlowId { get; init; }

        /// <summary>Flow-run identifier.</summary>
        public string? FlowRunId { get; init; }

        /// <summary>Run-assignment identifier.</summary>
        public string? RunAssignmentId { get; init; }

        /// <summary>Step identifier.</summary>
        public string? StepId { get; init; }

        /// <summary>Step-run identifier.</summary>
        public string? StepRunId { get; init; }

        /// <summary>Worker identifier.</summary>
        public string? WorkerId { get; init; }

        /// <summary>Ambient step logger.</summary>
        public ITempoStepLogger Logger { get; init; } = NullTempoStepLogger.Instance;

        internal static IDisposable Push(TempoExecutionContext? context)
        {
            TempoExecutionContext? previous = _Current.Value;
            _Current.Value = context;
            return new RestoreScope(previous);
        }

        private sealed class RestoreScope : IDisposable
        {
            private readonly TempoExecutionContext? _Previous;
            private bool _Disposed;

            public RestoreScope(TempoExecutionContext? previous)
            {
                _Previous = previous;
            }

            public void Dispose()
            {
                if (_Disposed) return;
                _Current.Value = _Previous;
                _Disposed = true;
            }
        }
    }

    internal sealed class NullTempoStepLogger : ITempoStepLogger
    {
        public static readonly NullTempoStepLogger Instance = new NullTempoStepLogger();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
