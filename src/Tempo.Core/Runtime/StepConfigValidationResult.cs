namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Result of runtime configuration validation.</summary>
    public class StepConfigValidationResult
    {
        /// <summary>
        /// Indicates whether the runtime configuration is valid.
        /// Default: true.
        /// </summary>
        public bool Valid { get; set; } = true;

        /// <summary>
        /// The validation error messages. Empty when the configuration is valid. Never null.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Creates a successful validation result with no errors.
        /// </summary>
        /// <returns>A validation result marked as valid.</returns>
        public static StepConfigValidationResult Success()
        {
            return new StepConfigValidationResult();
        }

        /// <summary>
        /// Creates a failed validation result containing the supplied errors.
        /// </summary>
        /// <param name="errors">The validation errors to include. May be null.</param>
        /// <returns>A validation result marked as invalid.</returns>
        public static StepConfigValidationResult Failure(IEnumerable<string> errors)
        {
            StepConfigValidationResult result = new StepConfigValidationResult { Valid = false };
            if (errors != null) result.Errors.AddRange(errors);
            return result;
        }
    }
}
