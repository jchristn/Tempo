namespace Tempo.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Enums;

    /// <summary>Request body for creating an artifact-backed step from pasted source code.</summary>
    public class SourceStepCreateRequest
    {
        public string? ExecutionKey { get; set; } = null;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = null;
        public string Language { get; set; } = "Python";
        public string Code { get; set; } = string.Empty;
        public string? FileName { get; set; } = null;
        public string? ArtifactName { get; set; } = null;
        public string? Version { get; set; } = null;
        public string Entrypoint { get; set; } = "main";
        public string? Module { get; set; } = null;
        public string Function { get; set; } = "run";
        public string HandlerType { get; set; } = "Tempo.UserSteps.Handler";
        public StepContractTypeEnum ContractType { get; set; } = StepContractTypeEnum.Loose;
        public string? InputSchema { get; set; } = null;
        public string? OutputSchema { get; set; } = null;
        public bool ValidateInput { get; set; } = false;
        public bool ValidateOutput { get; set; } = false;
        public int MaxRuntimeMs { get; set; } = 0;
        public bool Active { get; set; } = true;

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

        public SourceStepLanguage NormalizedLanguage => NormalizeLanguage(Language);

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
        Unknown = 0,
        Python = 1,
        JavaScript = 2,
        CSharp = 3
    }
}
