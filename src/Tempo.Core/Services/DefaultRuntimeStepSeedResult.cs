namespace Tempo.Core.Services
{
    using System.Collections.Generic;

    /// <summary>Summary of runtime sample step seeding work.</summary>
    public class DefaultRuntimeStepSeedResult
    {
        /// <summary>Step execution keys created during the seed pass.</summary>
        public List<string> StepsCreated { get; set; } = new List<string>();

        /// <summary>Artifact identifiers created during the seed pass.</summary>
        public List<string> ArtifactsCreated { get; set; } = new List<string>();

        /// <summary>Artifact version identifiers created during the seed pass.</summary>
        public List<string> ArtifactVersionsCreated { get; set; } = new List<string>();

        /// <summary>Human-readable notes for runtime types that could not be seeded as executable examples.</summary>
        public List<string> Notes { get; set; } = new List<string>();
    }
}
