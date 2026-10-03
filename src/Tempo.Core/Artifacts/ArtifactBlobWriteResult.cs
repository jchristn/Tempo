namespace Tempo.Core.Artifacts;

/// <summary>Result of writing an artifact blob.</summary>
public class ArtifactBlobWriteResult
{
	/// <summary>Tenant-scoped storage key.</summary>
	public string StorageKey { get; set; } = string.Empty;

	/// <summary>SHA-256 digest of the written content.</summary>
	public string Sha256 { get; set; } = string.Empty;

	/// <summary>Number of bytes written.</summary>
	public long ByteLength { get; set; }

	/// <summary>True when an existing blob was replaced.</summary>
	public bool Replaced { get; set; }
}
