using System.Collections.Generic;

namespace Tempo.Core.Artifacts;

/// <summary>Metrics from one artifact retention/GC pass.</summary>
public class ArtifactGcResult
{
	/// <summary>Number of tenants scanned during the pass. Default: 0.</summary>
	public int TenantsScanned { get; set; }

	/// <summary>Number of artifacts scanned during the pass. Default: 0.</summary>
	public int ArtifactsScanned { get; set; }

	/// <summary>Number of artifact versions scanned during the pass. Default: 0.</summary>
	public int VersionsScanned { get; set; }

	/// <summary>Number of artifact versions protected from deletion by retention rules. Default: 0.</summary>
	public int VersionsProtected { get; set; }

	/// <summary>Number of artifact versions marked for deletion. Default: 0.</summary>
	public int VersionsMarked { get; set; }

	/// <summary>Number of artifact versions deleted during the pass. Default: 0.</summary>
	public int VersionsDeleted { get; set; }

	/// <summary>Number of blobs deleted during the pass. Default: 0.</summary>
	public int BlobsDeleted { get; set; }

	/// <summary>Total number of bytes reclaimed by deletions during the pass. Default: 0.</summary>
	public long BytesDeleted { get; set; }

	/// <summary>Errors encountered during the pass. Default: empty list.</summary>
	public List<string> Errors { get; set; } = new List<string>();
}
