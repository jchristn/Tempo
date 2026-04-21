namespace Tempo.Core.Responses
{
    using Tempo.Core.Models;

    /// <summary>Response returned after creating a source-code backed step.</summary>
    public class SourceStepCreateResponse
    {
        public StepResponse Step { get; set; } = new StepResponse();
        public ArtifactRecord Artifact { get; set; } = new ArtifactRecord();
        public ArtifactVersionRecord ArtifactVersion { get; set; } = new ArtifactVersionRecord();
    }
}
