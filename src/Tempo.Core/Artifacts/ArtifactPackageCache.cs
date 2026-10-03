using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Tempo.Core.Models;
using Tempo.Core.Settings;

namespace Tempo.Core.Artifacts;

/// <summary>Extracts artifact ZIP packages into a tenant-scoped runtime cache.</summary>
public class ArtifactPackageCache
{
	private readonly IArtifactBlobStore _BlobStore;

	private readonly ExternalExecutionSettings _Settings;

	private readonly string _CacheRoot;

	/// <summary>
	/// Initializes a new instance of the <see cref="T:Tempo.Core.Artifacts.ArtifactPackageCache" /> class.
	/// </summary>
	/// <param name="blobStore">The blob store used to read artifact packages. Cannot be null.</param>
	/// <param name="settings">The external execution settings that supply the cache root. Cannot be null.</param>
	/// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="blobStore" /> or <paramref name="settings" /> is null.</exception>
	public ArtifactPackageCache(IArtifactBlobStore blobStore, ExternalExecutionSettings settings)
	{
		_BlobStore = blobStore ?? throw new ArgumentNullException("blobStore");
		_Settings = settings ?? throw new ArgumentNullException("settings");
		_CacheRoot = Path.GetFullPath(_Settings.CacheRoot);
		Directory.CreateDirectory(_CacheRoot);
	}

	/// <summary>
	/// Extracts the artifact version package into the cache if not already present and returns the cache directory.
	/// </summary>
	/// <param name="version">The artifact version to prepare. Cannot be null.</param>
	/// <param name="token">A token to observe for cancellation.</param>
	/// <returns>The full path to the extracted cache directory for the artifact version.</returns>
	/// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="version" /> is null.</exception>
	public async Task<string> PrepareAsync(ArtifactVersionRecord version, CancellationToken token = default(CancellationToken))
	{
		if (version == null)
		{
			throw new ArgumentNullException("version");
		}
		string root = CachePath(version.TenantId, version.Sha256);
		if (File.Exists(Path.Combine(root, ".tempo-cache-ready")))
		{
			return root;
		}
		string temp = root + "." + Guid.NewGuid().ToString("N") + ".tmp";
		if (Directory.Exists(temp))
		{
			Directory.Delete(temp, recursive: true);
		}
		Directory.CreateDirectory(temp);
		try
		{
			using Stream stream = await _BlobStore.OpenReadAsync(version.TenantId, version.Sha256, token).ConfigureAwait(continueOnCapturedContext: false);
			using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
			foreach (ZipArchiveEntry entry in zipArchive.Entries)
			{
				token.ThrowIfCancellationRequested();
				ExtractEntry(entry, temp, token);
			}
			File.WriteAllText(Path.Combine(temp, ".tempo-sha256"), version.Sha256);
			File.WriteAllText(Path.Combine(temp, ".tempo-cache-ready"), DateTime.UtcNow.ToString("o"));
			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
			Directory.Move(temp, root);
			return root;
		}
		catch
		{
			TryDelete(temp);
			throw;
		}
	}

	/// <summary>
	/// Computes the tenant-scoped cache directory path for an artifact content hash.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="sha256">The SHA-256 content hash of the artifact package.</param>
	/// <returns>The full cache directory path, guaranteed to be under the configured cache root.</returns>
	/// <exception cref="T:System.InvalidOperationException">Thrown when the resolved path escapes the cache root.</exception>
	public string CachePath(string tenantId, string sha256)
	{
		string fullPath = Path.GetFullPath(Path.Combine(_CacheRoot, SafeSegment(tenantId), SafeSegment(sha256)));
		EnsureUnderRoot(fullPath);
		return fullPath;
	}

	/// <summary>
	/// Deletes the cached package directory for the specified tenant and content hash.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="sha256">The SHA-256 content hash of the artifact package.</param>
	public void DeleteCache(string tenantId, string sha256)
	{
		TryDelete(CachePath(tenantId, sha256));
	}

	private static void ExtractEntry(ZipArchiveEntry entry, string root, CancellationToken token)
	{
		string text = entry.FullName.Replace('\\', '/');
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (text.EndsWith("/", StringComparison.Ordinal))
		{
			Directory.CreateDirectory(ResolveEntryPath(root, text));
			return;
		}
		if (IsUnixSymlink(entry))
		{
			throw new InvalidOperationException("Artifact archives may not contain symlinks: " + entry.FullName);
		}
		string path = ResolveEntryPath(root, text);
		Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
		using Stream stream = entry.Open();
		using FileStream destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
		stream.CopyToAsync(destination, 81920, token).GetAwaiter().GetResult();
	}

	private static string ResolveEntryPath(string root, string entryName)
	{
		if (Path.IsPathRooted(entryName) || entryName.Contains(":", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("Artifact archive entry must be relative: " + entryName);
		}
		string[] array = entryName.Split('/');
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == "..")
			{
				throw new InvalidOperationException("Artifact archive entry may not traverse parent directories: " + entryName);
			}
		}
		string fullPath = Path.GetFullPath(Path.Combine(root, entryName.Replace('/', Path.DirectorySeparatorChar)));
		string fullPath2 = Path.GetFullPath(root);
		string value = (fullPath2.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? fullPath2 : (fullPath2 + Path.DirectorySeparatorChar));
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Artifact archive entry escaped extraction root: " + entryName);
		}
		return fullPath;
	}

	private void EnsureUnderRoot(string path)
	{
		string value = (_CacheRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? _CacheRoot : (_CacheRoot + Path.DirectorySeparatorChar));
		if (!path.StartsWith(value, StringComparison.OrdinalIgnoreCase) && !string.Equals(path, _CacheRoot, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Artifact cache path escaped the configured cache root.");
		}
	}

	private static bool IsUnixSymlink(ZipArchiveEntry entry)
	{
		return ((entry.ExternalAttributes >> 16) & 0xF000) == 40960;
	}

	private static string SafeSegment(string value)
	{
		char[] array = value.ToCharArray();
		for (int i = 0; i < array.Length; i++)
		{
			char c = array[i];
			if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '_' && c != '-' && c != '.')
			{
				array[i] = '_';
			}
		}
		return new string(array);
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
		}
	}
}
