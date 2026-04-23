namespace Tempo.Protocol
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Base class for .NET process-backed Tempo step handlers.</summary>
    public abstract class TempoStepHandlerBase : ITempoStepHandler
    {
        /// <summary>Ambient context for the current handler invocation.</summary>
        protected TempoExecutionContext? ExecutionContext => TempoExecutionContext.Current;

        /// <summary>Handle one Tempo step request.</summary>
        public abstract Task<StepResult> RunAsync(StepRequest request, CancellationToken token);

        /// <summary>Create a successful result correlated to the request.</summary>
        protected StepResult Success(StepRequest request, object? data, object? metadata = null)
        {
            return TempoStepHost.Success(request, data, metadata);
        }

        /// <summary>Create an error result correlated to the request.</summary>
        protected StepResult Error(StepRequest request, object? data, object? metadata = null)
        {
            return TempoStepHost.Error(request, data, metadata);
        }

        /// <summary>Create an exception result correlated to the request.</summary>
        protected StepResult Exception(StepRequest? request, Exception exception, object? metadata = null)
        {
            return TempoStepHost.Exception(request, exception, metadata);
        }

        /// <summary>Write a debug log line.</summary>
        protected void LogDebug(string message)
        {
            Logger.Debug(message);
        }

        /// <summary>Write an informational log line.</summary>
        protected void LogInfo(string message)
        {
            Logger.Info(message);
        }

        /// <summary>Write a warning log line.</summary>
        protected void LogWarn(string message)
        {
            Logger.Warn(message);
        }

        /// <summary>Write a warning log line.</summary>
        protected void LogWarning(string message)
        {
            Logger.Warn(message);
        }

        /// <summary>Write an error log line.</summary>
        protected void LogError(string message)
        {
            Logger.Error(message);
        }

        private static ITempoStepLogger Logger => TempoExecutionContext.Current?.Logger ?? ConsoleTempoStepLogger.Instance;
    }
}
