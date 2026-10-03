using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Tempo.Core.Settings;

namespace Tempo.Core.Artifacts;

/// <summary>Filesystem-backed artifact blob store using tenant and SHA-256 path segments.</summary>
public class LocalFilesystemArtifactBlobStore : IArtifactBlobStore
{
	private readonly ArtifactSettings _Settings;

	private readonly string _RootPath;

	/// <summary>Instantiate.</summary>
	public LocalFilesystemArtifactBlobStore(ArtifactSettings settings)
	{
		_Settings = settings ?? throw new ArgumentNullException("settings");
		_RootPath = Path.GetFullPath(_Settings.RootPath);
		Directory.CreateDirectory(_RootPath);
	}

	/// <inheritdoc />
	public string GetStorageKey(string tenantId, string sha256)
	{
		ValidateTenantId(tenantId);
		string text = NormalizeSha(sha256);
		return tenantId + "/" + text;
	}

	/// <inheritdoc />
	public async Task<ArtifactBlobWriteResult> PutAsync(string tenantId, string sha256, Stream content, long contentLength, CancellationToken token = default(CancellationToken))
	{
		if (content == null)
		{
			throw new ArgumentNullException("content");
		}
		if (contentLength < 0)
		{
			throw new ArgumentOutOfRangeException("contentLength");
		}
		if (contentLength > _Settings.MaxUploadBytes)
		{
			throw new InvalidOperationException("Artifact upload exceeds the configured maximum upload size.");
		}
		string normalizedSha = NormalizeSha(sha256);
		string path = BlobPath(tenantId, normalizedSha);
		string tenantPath = TenantPath(tenantId);
		Directory.CreateDirectory(tenantPath);
		long num = await TenantBytesAsync(tenantId, token).ConfigureAwait(continueOnCapturedContext: false);
		long num2 = (File.Exists(path) ? new FileInfo(path).Length : 0);
		if (num - num2 + contentLength > _Settings.MaxBytesPerTenant)
		{
			throw new InvalidOperationException("Artifact upload exceeds the configured tenant quota.");
		}
		string tempPath = Path.Combine(tenantPath, normalizedSha + "." + Guid.NewGuid().ToString("N") + ".tmp");
		long bytesWritten = 0L;
		try
		{
			using SHA256 sha257 = SHA256.Create();
			byte[] buffer = new byte[81920];
			using (FileStream output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true))
			{
				while (true)
				{
					int num3 = await content.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(continueOnCapturedContext: false);
					if (num3 == 0)
					{
						break;
					}
					bytesWritten += num3;
					if (bytesWritten > _Settings.MaxUploadBytes)
					{
						throw new InvalidOperationException("Artifact upload exceeds the configured maximum upload size.");
					}
					sha257.TransformBlock(buffer, 0, num3, null, 0);
					await output.WriteAsync(buffer, 0, num3, token).ConfigureAwait(continueOnCapturedContext: false);
				}
				sha257.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			}
			if (bytesWritten != contentLength)
			{
				throw new InvalidOperationException("Artifact content length did not match the request body.");
			}
			if (!string.Equals(ToHex(sha257.Hash ?? Array.Empty<byte>()), normalizedSha, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("Artifact SHA-256 did not match the uploaded content.");
			}
			bool replaced = File.Exists(path);
			File.Move(tempPath, path, overwrite: true);
			return new ArtifactBlobWriteResult
			{
				StorageKey = GetStorageKey(tenantId, normalizedSha),
				Sha256 = normalizedSha,
				ByteLength = bytesWritten,
				Replaced = replaced
			};
		}
		finally
		{
			try
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
			catch
			{
			}
		}
	}

	/// <inheritdoc />
	public Task<Stream> OpenReadAsync(string tenantId, string sha256, CancellationToken token = default(CancellationToken))
	{
		token.ThrowIfCancellationRequested();
		return Task.FromResult((Stream)new FileStream(BlobPath(tenantId, sha256), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
	}

	/// <inheritdoc />
	public Task<bool> ExistsAsync(string tenantId, string sha256, CancellationToken token = default(CancellationToken))
	{
		token.ThrowIfCancellationRequested();
		return Task.FromResult(File.Exists(BlobPath(tenantId, sha256)));
	}

	/// <inheritdoc />
	public Task<bool> DeleteAsync(string tenantId, string sha256, CancellationToken token = default(CancellationToken))
	{
		token.ThrowIfCancellationRequested();
		string path = BlobPath(tenantId, sha256);
		if (!File.Exists(path))
		{
			return Task.FromResult(result: false);
		}
		File.Delete(path);
		return Task.FromResult(result: true);
	}

	/// <inheritdoc />
	public Task<long> TenantBytesAsync(string tenantId, CancellationToken token = default(CancellationToken))
	{
		token.ThrowIfCancellationRequested();
		string path = TenantPath(tenantId);
		if (!Directory.Exists(path))
		{
			return Task.FromResult(0L);
		}
		long num = 0L;
		foreach (string item in Directory.EnumerateFiles(path, "*.blob", SearchOption.TopDirectoryOnly))
		{
			token.ThrowIfCancellationRequested();
			num += new FileInfo(item).Length;
		}
		return Task.FromResult(num);
	}

	private string BlobPath(string tenantId, string sha256)
	{
		string fullPath = Path.GetFullPath(Path.Combine(TenantPath(tenantId), NormalizeSha(sha256) + ".blob"));
		EnsureUnderRoot(fullPath);
		return fullPath;
	}

	private string TenantPath(string tenantId)
	{
		ValidateTenantId(tenantId);
		string fullPath = Path.GetFullPath(Path.Combine(_RootPath, tenantId));
		EnsureUnderRoot(fullPath);
		return fullPath;
	}

	private void EnsureUnderRoot(string path)
	{
		string value = (_RootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? _RootPath : (_RootPath + Path.DirectorySeparatorChar));
		if (!path.StartsWith(value, StringComparison.OrdinalIgnoreCase) && !string.Equals(path, _RootPath, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Artifact path escaped the configured artifact root.");
		}
	}

	private static void ValidateTenantId(string tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			throw new ArgumentNullException("tenantId");
		}
		foreach (char c in tenantId)
		{
			if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '_' && c != '-')
			{
				throw new ArgumentException("Tenant identifier contains invalid characters.", "tenantId");
			}
		}
	}

	private static string NormalizeSha(string sha256)
	{
		if (string.IsNullOrWhiteSpace(sha256))
		{
			throw new ArgumentNullException("sha256");
		}
		string text = sha256.Trim().ToLowerInvariant();
		if (text.Length != 64)
		{
			throw new ArgumentException("SHA-256 must be 64 hexadecimal characters.", "sha256");
		}
		string text2 = text;
		foreach (char c in text2)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
			{
				throw new ArgumentException("SHA-256 must be 64 hexadecimal characters.", "sha256");
			}
		}
		return text;
	}

	private static string ToHex(byte[] bytes)
	{
		char[] array = new char[bytes.Length * 2];
		for (int i = 0; i < bytes.Length; i++)
		{
			array[i * 2] = "0123456789abcdef"[bytes[i] >> 4];
			array[i * 2 + 1] = "0123456789abcdef"[bytes[i] & 0xF];
		}
		return new string(array);
	}
}
