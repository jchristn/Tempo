using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Tempo.Core.Artifacts;

/// <summary>Tenant-scoped artifact blob storage.</summary>
public interface IArtifactBlobStore
{
	/// <summary>Build the stable tenant-scoped storage key for a SHA.</summary>
	string GetStorageKey(string tenantId, string sha256);

	/// <summary>Write a blob, validating size, quota, tenant path, and SHA-256.</summary>
	Task<ArtifactBlobWriteResult> PutAsync(string tenantId, string sha256, Stream content, long contentLength, CancellationToken token = default(CancellationToken));

	/// <summary>Open a stored blob for reading.</summary>
	Task<Stream> OpenReadAsync(string tenantId, string sha256, CancellationToken token = default(CancellationToken));

	/// <summary>Return whether a blob exists.</summary>
	Task<bool> ExistsAsync(string tenantId, string sha256, CancellationToken token = default(CancellationToken));

	/// <summary>Delete a blob if it exists.</summary>
	Task<bool> DeleteAsync(string tenantId, string sha256, CancellationToken token = default(CancellationToken));

	/// <summary>Calculate stored bytes for a tenant.</summary>
	Task<long> TenantBytesAsync(string tenantId, CancellationToken token = default(CancellationToken));
}
