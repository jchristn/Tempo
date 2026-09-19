namespace Tempo.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Enums;

    /// <summary>Request body for creating an artifact-backed step from pasted source code.</summary>
    public class SourceStepCreateRequest
    {
        /// <summary>Optional execution key that uniquely identifies the step. Default: null.</summary>
        public string? ExecutionKey { get; set; } = null;

        /// <summary>Human-readable name of the step. Required. Default: empty string.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description of the step. Default: null.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Source language of the pasted code (Python, JavaScript, or CSharp). Default: "Python".</summary>
        public string Language { get; set; } = "Python";

        /// <summary>Pasted source code for the step. Required. Default: empty string.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Optional file name for the source code. Must be a simple file name without path separators. Default: null.</summary>
        public string? FileName { get; set; } = null;

        /// <summary>Optional name of the artifact to create for the step. Default: null.</summary>
        public string? ArtifactName { get; set; } = null;

        /// <summary>Optional version to assign to the created artifact. Default: null.</summary>
        public string? Version { get; set; } = null;

        /// <summary>Entrypoint used to invoke the step. Required. Default: "main".</summary>
        public string Entrypoint { get; set; } = "main";

        /// <summary>Optional module name containing the step function. Default: null.</summary>
        public string? Module { get; set; } = null;

        /// <summary>Name of the function to invoke within the module. Required for Python and JavaScript. Default: "run".</summary>
        public string Function { get; set; } = "run";

        /// <summary>Fully-qualified handler type for CSharp source steps. Required for CSharp. Default: "Tempo.UserSteps.Handler".</summary>
        public string HandlerType { get; set; } = "Tempo.UserSteps.Handler";

        /// <summary>Contract type governing input/output schema enforcement. Default: <see cref="StepContractTypeEnum.Loose"/>.</summary>
        public StepContractTypeEnum ContractType { get; set; } = StepContractTypeEnum.Loose;

        /// <summary>Optional JSON schema describing the step's input. Default: null.</summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>Optional JSON schema describing the step's output. Default: null.</summary>
        public string? OutputSchema { get; set; } = null;

        /// <summary>Whether to validate step input against the input schema. Default: false.</summary>
        public bool ValidateInput { get; set; } = false;

        /// <summary>Whether to validate step output against the output schema. Default: false.</summary>
        public bool ValidateOutput { get; set; } = false;

        /// <summary>Maximum runtime in milliseconds (0 for no timeout). Default: 0. Range: 0 to int.MaxValue.</summary>
        public int MaxRuntimeMs { get; set; } = 0;

        /// <summary>Whether the created step is active. Default: true.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Validates the request and returns a list of validation error messages.</summary>
        /// <returns>A read-only list of validation errors; empty when the request is valid.</returns>
        public IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Name)) errors.Add("name is required.");
            if (string.IsNullOrWhiteSpace(Code)) errors.Add("code is required.");
            if (string.IsNullOrWhiteSpace(Entrypoint)) errors.Add("entrypoint is required.");
            if (MaxRuntimeMs < 0) errors.Add("maxRuntimeMs must be 0 or greater.");
            SourceStepLanguage language = NormalizeLanguage(Language);
            if (language == SourceStepLanguage.Unknown) errors.Add("language must be Python, JavaScript, or CSharp.");
            if (language == SourceStepLanguage.Python || language == SourceStepLanguage.JavaScript)
            {
                if (string.IsNullOrWhiteSpace(Function)) errors.Add("function is required.");
            }
            if (language == SourceStepLanguage.CSharp && string.IsNullOrWhiteSpace(HandlerType))
                errors.Add("handlerType is required for CSharp source steps.");
            if (!string.IsNullOrWhiteSpace(FileName) && !IsSafeFileName(FileName!))
                errors.Add("fileName must be a simple file name without path separators.");
            return errors;
        }

        /// <summary>The normalized language resolved from <see cref="Language"/>.</summary>
        public SourceStepLanguage NormalizedLanguage => NormalizeLanguage(Language);

        /// <summary>Normalizes a language string into a <see cref="SourceStepLanguage"/> value.</summary>
        /// <param name="language">The raw language string to normalize. May be null.</param>
        /// <returns>The matching <see cref="SourceStepLanguage"/>, or <see cref="SourceStepLanguage.Unknown"/> when unrecognized.</returns>
        public static SourceStepLanguage NormalizeLanguage(string? language)
        {
            string value = (language ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "python" || value == "py") return SourceStepLanguage.Python;
            if (value == "javascript" || value == "java_script" || value == "js" || value == "node" || value == "nodejs") return SourceStepLanguage.JavaScript;
            if (value == "csharp" || value == "c#" || value == "cs" || value == "dotnet" || value == ".net") return SourceStepLanguage.CSharp;
            return SourceStepLanguage.Unknown;
        }

        private static bool IsSafeFileName(string fileName)
        {
            string justName = System.IO.Path.GetFileName(fileName);
            return string.Equals(justName, fileName, StringComparison.Ordinal) &&
                !fileName.Contains("/", StringComparison.Ordinal) &&
                !fileName.Contains("\\", StringComparison.Ordinal) &&
                !fileName.Contains(":", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(justName);
        }
    }

    /// <summary>Supported pasted-source step languages.</summary>
    public enum SourceStepLanguage
    {
        /// <summary>Unknown or unrecognized language.</summary>
        Unknown = 0,
        /// <summary>Python source language.</summary>
        Python = 1,
        /// <summary>JavaScript source language.</summary>
        JavaScript = 2,
        /// <summary>C# source language.</summary>
        CSharp = 3
    }
}
