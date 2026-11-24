namespace BinaryVibrance.LunelleStudio.Tests;

// Files kept in memory, as a browser host keeps them in its storage: each write heard by whoever watches
internal sealed class MemoryFiles : IMusicFiles
{
	private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
	private readonly List<(string Folder, Action<string> Changed)> _watches = [];

	public bool Exists(string path) => _files.ContainsKey(Path.GetFullPath(path));

	public string ReadText(string path) => System.Text.Encoding.UTF8.GetString(ReadBytes(path));

	public byte[] ReadBytes(string path) =>
		_files.TryGetValue(Path.GetFullPath(path), out var bytes) ? bytes : throw new FileNotFoundException(path);

	public void WriteText(string path, string text) => WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(text));

	public void WriteBytes(string path, byte[] bytes)
	{
		var full = Path.GetFullPath(path);
		_files[full] = bytes;
		foreach (var (folder, changed) in _watches.ToList())
			if (full.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
				changed(full);
	}

	public IEnumerable<string> List(string folder)
	{
		var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
		return _files.Keys.Where(k => k.StartsWith(root, StringComparison.Ordinal)).Select(k => k[root.Length..].Replace('\\', '/')).ToList();
	}

	public IDisposable Watch(string folder, Action<string> changed)
	{
		var watch = (Path.GetFullPath(folder), changed);
		_watches.Add(watch);
		return new Unwatch(() => _watches.Remove(watch));
	}

	private sealed class Unwatch(Action undo) : IDisposable
	{
		public void Dispose() => undo();
	}
}
