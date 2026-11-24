namespace BinaryVibrance.LunelleStudio.App.Web;

/// <summary>
/// The browser's files: every one in memory, read from there, and each change written behind it to IndexedDB, which
/// keeps them from one visit to the next (wwwroot/lunelle-studio.js). Paths are full paths in the runtime's own file
/// system (<c>/music/Songs/tune.song.json</c>), kept as they are. One thread runs the browser studio, so no lock is needed.
/// </summary>
internal sealed class BrowserFiles : IMusicFiles
{
	private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
	private readonly List<(string Folder, Action<string> Changed)> _watches = [];

	private BrowserFiles()
	{
	}

	/// <summary>Whether the browser keeps the files; when it won't (a private window), they last until the page closes.</summary>
	public bool Kept { get; private init; }

	/// <summary>The files the browser has kept, loaded.</summary>
	public static async Task<BrowserFiles> OpenAsync()
	{
		var files = new BrowserFiles { Kept = await StudioPage.FilesOpen() };
		foreach (var path in StudioPage.FilesLoaded().Split('\n', StringSplitOptions.RemoveEmptyEntries))
			files._files[path] = StudioPage.FilesTake(path);
		return files;
	}

	/// <inheritdoc/>
	public bool Exists(string path) => _files.ContainsKey(Path.GetFullPath(path));

	/// <inheritdoc/>
	public string ReadText(string path) => System.Text.Encoding.UTF8.GetString(ReadBytes(path));

	/// <inheritdoc/>
	public byte[] ReadBytes(string path) =>
		_files.TryGetValue(Path.GetFullPath(path), out var bytes) ? bytes : throw new FileNotFoundException($"There is no file {path}.", path);

	/// <inheritdoc/>
	public void WriteText(string path, string text) => WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(text));

	/// <summary>Writes the file, unless it already holds just these bytes.</summary>
	public void WriteBytes(string path, byte[] bytes)
	{
		var full = Path.GetFullPath(path);
		if (_files.TryGetValue(full, out var was) && was.AsSpan().SequenceEqual(bytes))
			return;
		_files[full] = bytes;
		StudioPage.FilesPut(full, bytes);
		Heard(full);
	}

	/// <summary>Removes the file, if it is there.</summary>
	public void Delete(string path)
	{
		var full = Path.GetFullPath(path);
		if (!_files.Remove(full))
			return;
		StudioPage.FilesDelete(full);
		Heard(full);
	}

	/// <inheritdoc/>
	public IEnumerable<string> List(string folder)
	{
		var root = Under(folder);
		return [.. _files.Keys.Where(k => k.StartsWith(root, StringComparison.Ordinal)).Select(k => k[root.Length..])];
	}

	/// <inheritdoc/>
	public IDisposable Watch(string folder, Action<string> changed)
	{
		var watch = (Under(folder), changed);
		_watches.Add(watch);
		return new Unwatch(() => _watches.Remove(watch));
	}

	private void Heard(string full)
	{
		foreach (var (folder, changed) in _watches.ToList())
			if (full.StartsWith(folder, StringComparison.Ordinal))
				changed(full);
	}

	private static string Under(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + "/";

	private sealed class Unwatch(Action undo) : IDisposable
	{
		public void Dispose() => undo();
	}
}
