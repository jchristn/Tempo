namespace Tempo.Core.Responses
{
    using Tempo.Core.Models;

    /// <summary>Response returned after creating a source-code backed step.</summary>
    public class SourceStepCreateResponse
    {
        /// <summary>
        /// The step that was created.
        /// </summary>
        public StepResponse Step { get; set; } = new StepResponse();

        /// <summary>
        /// The artifact associated with the created step.
        /// </summary>
        public ArtifactRecord Artifact { get; set; } = new ArtifactRecord();

        /// <summary>
        /// The specific artifact version associated with the created step.
        /// </summary>
        public ArtifactVersionRecord ArtifactVersion { get; set; } = new ArtifactVersionRecord();
    }
}
