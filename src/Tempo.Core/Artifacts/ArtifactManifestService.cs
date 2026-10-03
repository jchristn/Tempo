using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Tempo.Core.Models;
using Tempo.Core.Runtime;
using Tempo.Protocol;

namespace Tempo.Core.Artifacts;

/// <summary>Parses and validates artifact package manifests.</summary>
public static class ArtifactManifestService
{
	/// <summary>Name of the manifest file expected within an artifact package.</summary>
	public const string ManifestFileName = "tempo.step.json";

	private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = false
	};

	/// <summary>
	/// Parses an artifact manifest from its JSON representation.
	/// </summary>
	/// <param name="json">The manifest JSON; may be null or whitespace.</param>
	/// <returns>The parsed manifest, or null when the input is null, empty, or whitespace.</returns>
	public static ArtifactManifest? Parse(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}
		return JsonSerializer.Deserialize<ArtifactManifest>(json, _Json);
	}

	/// <summary>
	/// Serializes an artifact manifest to its JSON representation.
	/// </summary>
	/// <param name="manifest">The manifest to serialize.</param>
	/// <returns>The JSON representation of the manifest.</returns>
	public static string Serialize(ArtifactManifest manifest)
	{
		return JsonSerializer.Serialize(manifest, _Json);
	}

	/// <summary>
	/// Reads and parses the manifest embedded in a zipped artifact package.
	/// </summary>
	/// <param name="packageBytes">The raw bytes of the artifact package archive.</param>
	/// <returns>The parsed manifest, or null when the bytes are not a zip archive or no manifest entry is present.</returns>
	public static ArtifactManifest? ReadFromZip(byte[] packageBytes)
	{
		using MemoryStream memoryStream = new MemoryStream(packageBytes, writable: false);
		if (!LooksLikeZip(memoryStream))
		{
			return null;
		}
		memoryStream.Position = 0L;
		using ZipArchive zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);
		ZipArchiveEntry? zipArchiveEntry = zipArchive.Entries.FirstOrDefault((ZipArchiveEntry e) => string.Equals(NormalizeEntryName(e.FullName), "tempo.step.json", StringComparison.OrdinalIgnoreCase));
		if (zipArchiveEntry == null)
		{
			return null;
		}
		using Stream stream = zipArchiveEntry.Open();
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		return Parse(streamReader.ReadToEnd());
	}

	/// <summary>
	/// Validates an artifact manifest and returns any validation errors.
	/// </summary>
	/// <param name="manifest">The manifest to validate; may be null.</param>
	/// <returns>A read-only list of validation error messages; empty when the manifest is valid.</returns>
	public static IReadOnlyList<string> Validate(ArtifactManifest? manifest)
	{
		List<string> list = new List<string>();
		if (manifest == null)
		{
			list.Add("artifact manifest is required for artifact-backed runtimes.");
			return list;
		}
		if (string.IsNullOrWhiteSpace(manifest.ManifestVersion))
		{
			list.Add("manifestVersion is required.");
		}
		if (string.IsNullOrWhiteSpace(manifest.RuntimeKey))
		{
			list.Add("runtimeKey is required.");
		}
		else if (manifest.RuntimeKey != StepRuntimeKeys.ArtifactProcess.ToString() && manifest.RuntimeKey != StepRuntimeKeys.ArtifactPython.ToString() && manifest.RuntimeKey != StepRuntimeKeys.ArtifactJavaScript.ToString() && manifest.RuntimeKey != StepRuntimeKeys.ArtifactDotnetProcess.ToString())
		{
			list.Add("runtimeKey must be Artifact.Process, Artifact.Python, Artifact.JavaScript, or Artifact.DotnetProcess.");
		}
		if (manifest.SupportedProtocolVersions.Count == 0 && !string.IsNullOrWhiteSpace(manifest.ProtocolVersion))
		{
			manifest.SupportedProtocolVersions.Add(manifest.ProtocolVersion);
		}
		if (manifest.SupportedProtocolVersions.Count == 0)
		{
			list.Add("supportedProtocolVersions is required.");
		}
		else if (!((IEnumerable<string>)manifest.SupportedProtocolVersions).Any((Func<string, bool>)ProtocolVersions.IsSupported))
		{
			list.Add("supportedProtocolVersions must overlap server-supported protocol versions.");
		}
		if (manifest.Entrypoints.Count == 0)
		{
			list.Add("entrypoints is required.");
		}
		if (string.IsNullOrWhiteSpace(manifest.DefaultEntrypoint))
		{
			list.Add("defaultEntrypoint is required.");
		}
		else if (manifest.Entrypoints.Count > 0 && !manifest.Entrypoints.ContainsKey(manifest.DefaultEntrypoint))
		{
			list.Add("defaultEntrypoint must reference a manifest entrypoint.");
		}
		foreach (KeyValuePair<string, ArtifactManifestEntrypoint> entrypoint in manifest.Entrypoints)
		{
			if (string.IsNullOrWhiteSpace(entrypoint.Key))
			{
				list.Add("entrypoint names cannot be empty.");
			}
			ArtifactManifestEntrypoint artifactManifestEntrypoint = entrypoint.Value ?? new ArtifactManifestEntrypoint();
			if (manifest.RuntimeKey == StepRuntimeKeys.ArtifactProcess.ToString())
			{
				if (string.IsNullOrWhiteSpace(artifactManifestEntrypoint.Command))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' requires command.");
				}
				else if (IsUnsafePathReference(artifactManifestEntrypoint.Command))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' command must be a relative artifact path.");
				}
			}
			if (manifest.RuntimeKey == StepRuntimeKeys.ArtifactPython.ToString())
			{
				if (string.IsNullOrWhiteSpace(artifactManifestEntrypoint.Module))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' requires module.");
				}
				if (string.IsNullOrWhiteSpace(artifactManifestEntrypoint.Function))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' requires function.");
				}
			}
			if (manifest.RuntimeKey == StepRuntimeKeys.ArtifactJavaScript.ToString())
			{
				if (string.IsNullOrWhiteSpace(artifactManifestEntrypoint.Module))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' requires module.");
				}
				else if (IsUnsafePathReference(artifactManifestEntrypoint.Module))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' module must be a relative artifact path.");
				}
				if (string.IsNullOrWhiteSpace(artifactManifestEntrypoint.Function))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' requires function.");
				}
			}
			if (manifest.RuntimeKey == StepRuntimeKeys.ArtifactDotnetProcess.ToString())
			{
				if (string.IsNullOrWhiteSpace(artifactManifestEntrypoint.Command))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' requires command.");
				}
				else if (IsUnsafePathReference(artifactManifestEntrypoint.Command))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' command must be a relative artifact path.");
				}
				else if (!artifactManifestEntrypoint.Command.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' command must reference a .NET assembly dll.");
				}
				if (string.IsNullOrWhiteSpace(artifactManifestEntrypoint.HandlerType))
				{
					list.Add("entrypoint '" + entrypoint.Key + "' requires handlerType.");
				}
			}
		}
		return list;
	}

	/// <summary>
	/// Determines whether a path reference is unsafe (rooted, absolute, drive-qualified, empty, or containing parent traversal).
	/// </summary>
	/// <param name="value">The path reference to inspect.</param>
	/// <returns>True when the reference is unsafe or null/whitespace; otherwise false.</returns>
	public static bool IsUnsafePathReference(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return true;
		}
		string text = value.Replace('\\', '/');
		if (Path.IsPathRooted(value))
		{
			return true;
		}
		if (text.StartsWith("/", StringComparison.Ordinal))
		{
			return true;
		}
		if (text.Contains(":", StringComparison.Ordinal))
		{
			return true;
		}
		return text.Split('/').Any((string p) => p == "..");
	}

	private static bool LooksLikeZip(Stream stream)
	{
		if (!stream.CanSeek || stream.Length < 4)
		{
			return false;
		}
		long position = stream.Position;
		Span<byte> buffer = stackalloc byte[4];
		stream.ReadExactly(buffer);
		stream.Position = position;
		if (buffer[0] == 80 && buffer[1] == 75)
		{
			if (buffer[2] != 3 && buffer[2] != 5)
			{
				return buffer[2] == 7;
			}
			return true;
		}
		return false;
	}

	private static string NormalizeEntryName(string name)
	{
		return name.Replace('\\', '/').TrimStart('/');
	}
}
