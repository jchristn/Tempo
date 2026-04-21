namespace Tempo.Sdk
{
    /// <summary>Tempo step result states.</summary>
    public enum StepResultType
    {
        /// <summary>The step completed successfully.</summary>
        Success,

        /// <summary>The step exceeded a runtime limit.</summary>
        Timeout,

        /// <summary>The step produced a handled business error.</summary>
        Error,

        /// <summary>The step failed with an exception.</summary>
        Exception,

        /// <summary>The flow exceeded its maximum transition count.</summary>
        MaxIterationsExceeded
    }
}
