using System.IO.Compression;

using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A music project of files alone as one file to keep or hand on: a <c>.lunelle</c> file, a zip of the project's folder
/// (its <c>lunelle-project.json</c>, songs, patches, kits and samples), without what is built or what the workbench was
/// doing. A zip of a folder that holds the project is taken as well, the folder dropped.
/// </summary>
public static class MusicArchive
{
	/// <summary>The extension a project's file is given.</summary>
	public const string Extension = ".lunelle";

	/// <summary>The project under <paramref name="folder"/> in <paramref name="files"/>, zipped.</summary>
	public static byte[] Pack(IMusicFiles files, string folder)
	{
		ArgumentNullException.ThrowIfNull(files);
		ArgumentNullException.ThrowIfNull(folder);
		using var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (var path in files.List(folder).Where(p => !LeftOut(p)).Order(StringComparer.Ordinal))
			{
				var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
				using var into = entry.Open();
				into.Write(files.ReadBytes(Path.Combine(folder, path)));
			}
		}
		return stream.ToArray();
	}

	/// <summary>
	/// The files in a project's zip, by their paths in the project (<c>/</c> between folders): from its root, or from the
	/// one folder holding its <c>lunelle-project.json</c>.
	/// </summary>
	/// <exception cref="InvalidDataException">It isn't a zip, or holds no music project, or names a file outside it.</exception>
	public static IReadOnlyList<(string Path, byte[] Bytes)> Unpack(byte[] archive)
	{
		ArgumentNullException.ThrowIfNull(archive);
		var files = new List<(string Path, byte[] Bytes)>();
		using (var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read))
		{
			foreach (var entry in zip.Entries)
			{
				var path = entry.FullName.Replace('\\', '/');
				if (path.EndsWith('/') || path.StartsWith("__MACOSX/", StringComparison.Ordinal))
					continue;
				if (path.StartsWith('/') || path.Contains(':') || path.Split('/').Any(part => part is "" or "." or ".."))
					throw new InvalidDataException($"It names a file outside the project: {entry.FullName}");
				using var from = entry.Open();
				using var bytes = new MemoryStream();
				from.CopyTo(bytes);
				files.Add((path, bytes.ToArray()));
			}
		}

		// The project at the root, or in one folder at the root
		var projects = files.Where(f => f.Path == MusicFolder.ProjectFileName || f.Path.EndsWith("/" + MusicFolder.ProjectFileName, StringComparison.Ordinal))
			.Select(f => f.Path[..^MusicFolder.ProjectFileName.Length]).Distinct().ToList();
		var root = projects switch
		{
			[] => throw new InvalidDataException($"It holds no music project: there is no {MusicFolder.ProjectFileName} in it."),
			_ when projects.Contains("") => "",
			[var only] when only.Count(c => c == '/') == 1 => only,
			_ => throw new InvalidDataException($"It holds no music project at its root: {MusicFolder.ProjectFileName} is at {string.Join(", ", projects.Select(p => p + MusicFolder.ProjectFileName))}."),
		};
		return [.. files.Where(f => f.Path.StartsWith(root, StringComparison.Ordinal))
			.Select(f => (Path: f.Path[root.Length..], f.Bytes))
			.Where(f => !LeftOut(f.Path))];
	}

	/// <summary>A file name for a project named <paramref name="name"/>: the name, with what a file name can't hold left out.</summary>
	public static string FileName(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
		var safe = new string([.. name.Where(c => !invalid.Contains(c) && !char.IsControl(c))]).Trim().Trim('.');
		return (safe.Length > 0 ? safe : "music") + Extension;
	}

	// What builds, what the workbench was doing and version control's own: none of it the music
	private static bool LeftOut(string path) =>
		path.StartsWith("bin/", StringComparison.Ordinal) || path.StartsWith("obj/", StringComparison.Ordinal)
		|| path.StartsWith(".lunelle-studio/", StringComparison.Ordinal) || path.StartsWith(".git/", StringComparison.Ordinal);
}
