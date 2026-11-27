using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A music project as one file to keep or hand on: a <c>.lunelle</c> file (Lunelle's format 2, docs/spec/project.md §6),
/// packed by Lunelle's <see cref="ProjectPacker"/>: the project's own files, without code, what is built or what the
/// workbench was doing, and the library sounds it uses bundled beside them, so it stands alone. A zip of a folder that
/// holds the project is taken as well, the folder dropped.
/// </summary>
public static class MusicArchive
{
	/// <summary>The extension a project's file is given.</summary>
	public const string Extension = ".lunelle";

	/// <summary>The project under <paramref name="folder"/> in <paramref name="files"/>, packed.</summary>
	public static byte[] Pack(IMusicFiles files, string folder) => PackWithNotes(files, folder).Bytes;

	/// <summary>The project under <paramref name="folder"/> packed, with notes on what was left out (its code).</summary>
	public static ProjectPacker.Packed PackWithNotes(IMusicFiles files, string folder)
	{
		ArgumentNullException.ThrowIfNull(files);
		ArgumentNullException.ThrowIfNull(folder);
		// On disk a library may sit beside the project, outside its folder; elsewhere a project has its libraries inside
		IProjectSource source = files is DiskFiles
			? new DirectorySource(folder)
			: new MemorySource(files.List(folder), path => files.ReadBytes(Path.Combine(folder, path)), folder);
		var music = MusicFolder.Load(source);
		return ProjectPacker.Pack(music.Project, music.Resolver);
	}

	/// <summary>
	/// The files in a project's file, by their paths in the project (<c>/</c> between folders): from its root, or from the
	/// one folder holding its <c>lunelle.meta.json</c>.
	/// </summary>
	/// <exception cref="InvalidDataException">It isn't a zip, or holds no music project, or names a file outside it.</exception>
	public static IReadOnlyList<(string Path, byte[] Bytes)> Unpack(byte[] archive)
	{
		ArgumentNullException.ThrowIfNull(archive);
		var source = ZipSource.Read(archive);
		return [.. source.Paths.Where(p => !EntryIds.LeftOut(p)).Select(p => (p, source.ReadBytes(p)))];
	}

	/// <summary>A file name for a project named <paramref name="name"/>: the name, with what a file name can't hold left out.</summary>
	public static string FileName(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
		var safe = new string([.. name.Where(c => !invalid.Contains(c) && !char.IsControl(c))]).Trim().Trim('.');
		return (safe.Length > 0 ? safe : "music") + Extension;
	}

	/// <summary>
	/// Unpacks <paramref name="archive"/> into a folder of its own under <paramref name="home"/>, named
	/// <paramref name="name"/>, and returns it. A folder of that name already holding just these files (unpacked from the
	/// same file before) is returned as it is; one holding anything else is left alone, and <c>name (2)</c>,
	/// <c>name (3)</c> and so on are tried.
	/// </summary>
	/// <exception cref="InvalidDataException">It isn't a project's file, and nothing is written.</exception>
	public static string UnpackBeside(IMusicFiles files, string home, string name, byte[] archive)
	{
		ArgumentNullException.ThrowIfNull(files);
		var incoming = Unpack(archive);
		for (var n = 1; ; n++)
		{
			var folder = Path.Combine(home, n == 1 ? name : $"{name} ({n})");
			if (!files.List(folder).Any())
			{
				foreach (var (file, bytes) in incoming)
					files.WriteBytes(Path.Combine(folder, file), bytes);
				return folder;
			}
			if (Holds(files, folder, incoming))
				return folder;
		}
	}

	// Whether the folder holds just what the archive does (what is never a project's aside)
	private static bool Holds(IMusicFiles files, string folder, IReadOnlyList<(string Path, byte[] Bytes)> incoming)
	{
		var there = files.List(folder).Where(p => !EntryIds.LeftOut(p)).ToHashSet(StringComparer.Ordinal);
		if (there.Count != incoming.Count)
			return false;
		foreach (var (file, bytes) in incoming)
			if (!there.Contains(file) || !files.ReadBytes(Path.Combine(folder, file)).AsSpan().SequenceEqual(bytes))
				return false;
		return true;
	}
}
