using System;
using System.IO;

namespace Tempo.Core.Artifacts;

/// <summary>Validates and normalizes artifact-relative file paths.</summary>
public static class ArtifactFilePath
{
	/// <summary>Normalize a user supplied artifact-relative path.</summary>
	public static string Normalize(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			throw new ArgumentException("artifact file path is required.", "path");
		}
		string text = path.Trim().Replace('\\', '/');
		while (text.StartsWith("./", StringComparison.Ordinal))
		{
			text = text.Substring(2);
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ArgumentException("artifact file path is required.", "path");
		}
		if (text.Length > 1024)
		{
			throw new ArgumentException("artifact file path cannot exceed 1024 characters.", "path");
		}
		if (Path.IsPathRooted(text) || text.StartsWith("/", StringComparison.Ordinal) || text.Contains(":", StringComparison.Ordinal))
		{
			throw new ArgumentException("artifact file path must be relative.", "path");
		}
		string[] array = text.Split('/');
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			if (string.IsNullOrWhiteSpace(text2))
			{
				throw new ArgumentException("artifact file path cannot contain empty segments.", "path");
			}
			if (text2 == "." || text2 == "..")
			{
				throw new ArgumentException("artifact file path cannot traverse directories.", "path");
			}
		}
		return string.Join("/", array);
	}
}
