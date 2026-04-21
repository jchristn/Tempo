namespace Tempo.Core.Services
{
    using System.Collections.Generic;

    /// <summary>References that block deletion of a resource.</summary>
    public class DeletionDependencyResult
    {
        /// <summary>Resource references that would be broken by the delete.</summary>
        public List<string> References { get; set; } = new List<string>();

        /// <summary>True when deletion must be blocked.</summary>
        public bool IsBlocked => References.Count > 0;

        /// <summary>Build a concise message describing the references.</summary>
        public string ToMessage(string resourceName)
        {
            string noun = string.IsNullOrWhiteSpace(resourceName) ? "Resource" : resourceName.Trim();
            return noun + " is referenced by: " + string.Join(", ", References);
        }
    }
}
