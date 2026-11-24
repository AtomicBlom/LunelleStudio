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

	/// <summary>Writes the file, making the folders it is in if need be.</summary>
	/// <exception cref="IOException">It can't be written.</exception>
	void WriteText(string path, string text);
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
	public void WriteText(string path, string text)
	{
		if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder)
			Directory.CreateDirectory(folder);
		File.WriteAllText(path, text);
	}
}
