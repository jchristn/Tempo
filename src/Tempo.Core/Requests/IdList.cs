namespace Tempo.Core.Requests
{
    using System.Collections.Generic;

    /// <summary>A list of identifiers, used for bulk operations.</summary>
    public class IdList
    {
        /// <summary>Identifiers to operate on.</summary>
        public List<string> Ids { get; set; } = new List<string>();
    }
}
