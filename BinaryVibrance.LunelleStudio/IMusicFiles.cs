namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// Where the workbench reads and writes a project's files (its songs, patches and what it was doing): the disk on the
/// desktop (<see cref="DiskFiles"/>), or wherever a host keeps them, such as a browser's storage. Paths are as the catalog
/// gives them.
/// </summary>
public interface IMusicFiles
{
	/// <summary>Whether <paramref name="path"/> is a file there.</summary>
	bool Exists(string path);

	/// <summary>The file's text.</summary>
	/// <exception cref="IOException">It isn't there or can't be read.</exception>
	string ReadText(string path);

	/// <summary>The file's bytes.</summary>
	/// <exception cref="IOException">It isn't there or can't be read.</exception>
	byte[] ReadBytes(string path);

	/// <summary>Writes the file, making the folders it is in if need be.</summary>
	/// <exception cref="IOException">It can't be written.</exception>
	void WriteText(string path, string text);

	/// <summary>Writes the file's bytes (a kit's samples), making the folders it is in if need be.</summary>
	/// <exception cref="IOException">It can't be written.</exception>
	void WriteBytes(string path, byte[] bytes);

	/// <summary>
	/// Every file under <paramref name="folder"/>, in its folders too, as paths relative to it with <c>/</c> between
	/// folders; none when there is no such folder.
	/// </summary>
	IEnumerable<string> List(string folder);

	/// <summary>Removes the file at <paramref name="path"/>, if it is there.</summary>
	void Delete(string path);

	/// <summary>
	/// Calls <paramref name="changed"/> with a file's full path whenever a file under <paramref name="folder"/> is written,
	/// made, removed or renamed, by the workbench or anything else, until the result is disposed. It may be called on
	/// any thread.
	/// </summary>
	IDisposable Watch(string folder, Action<string> changed);
}

/// <summary>The files on disk.</summary>
public sealed class DiskFiles : IMusicFiles
{
	/// <summary>The one there is.</summary>
	public static DiskFiles Instance { get; } = new();

	private DiskFiles()
	{
	}

	/// <inheritdoc/>
	public bool Exists(string path) => File.Exists(path);

	/// <inheritdoc/>
	public string ReadText(string path) => File.ReadAllText(path);

	/// <inheritdoc/>
	public byte[] ReadBytes(string path) => File.ReadAllBytes(path);

	/// <inheritdoc/>
	public void WriteText(string path, string text)
	{
		if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder)
			Directory.CreateDirectory(folder);
		File.WriteAllText(path, text);
	}

	/// <inheritdoc/>
	public void WriteBytes(string path, byte[] bytes)
	{
		if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder)
			Directory.CreateDirectory(folder);
		File.WriteAllBytes(path, bytes);
	}

	/// <inheritdoc/>
	public void Delete(string path)
	{
		if (File.Exists(path))
			File.Delete(path);
	}

	/// <inheritdoc/>
	public IEnumerable<string> List(string folder)
	{
		var root = Path.GetFullPath(folder);
		return Directory.Exists(root)
			? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
			: [];
	}

	/// <inheritdoc/>
	public IDisposable Watch(string folder, Action<string> changed)
	{
		var watcher = new FileSystemWatcher(Path.GetFullPath(folder))
		{
			IncludeSubdirectories = true,
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
		};
		watcher.Changed += (_, e) => changed(e.FullPath);
		watcher.Created += (_, e) => changed(e.FullPath);
		watcher.Deleted += (_, e) => changed(e.FullPath);
		watcher.Renamed += (_, e) => changed(e.FullPath);
		watcher.EnableRaisingEvents = true;
		return watcher;
	}
}
