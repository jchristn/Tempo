namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Result of runtime configuration validation.</summary>
    public class StepConfigValidationResult
    {
        public bool Valid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();

        public static StepConfigValidationResult Success()
        {
            return new StepConfigValidationResult();
        }

        public static StepConfigValidationResult Failure(IEnumerable<string> errors)
        {
            StepConfigValidationResult result = new StepConfigValidationResult { Valid = false };
            if (errors != null) result.Errors.AddRange(errors);
            return result;
        }
    }
}
