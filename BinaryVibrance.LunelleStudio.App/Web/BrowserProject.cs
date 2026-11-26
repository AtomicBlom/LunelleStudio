namespace BinaryVibrance.LunelleStudio.App.Web;

/// <summary>
/// The one music project the browser studio keeps, in <see cref="Folder"/> of its files: <c>samples/Showcase</c> the
/// first time (embedded), and whatever is imported in its place after.
/// </summary>
internal static class BrowserProject
{
	/// <summary>Where the project is, in the browser's files.</summary>
	public const string Folder = "/music";

	/// <summary>
	/// Writes the showcase's music when there is no project yet: its files as embedded, and the takes of each sampled kit
	/// (<c>Kits/&lt;name&gt;/kit.json</c>) from the presets' kit of that folder, which are the same and embedded once.
	/// </summary>
	public static void StartIfEmpty(BrowserFiles files)
	{
		if (files.List(Folder).Any())
			return;
		var assembly = typeof(BrowserProject).Assembly;
		// %(RecursiveDir) names a resource with the build machine's separator
		var resources = assembly.GetManifestResourceNames().ToDictionary(r => r.Replace('\\', '/'), StringComparer.Ordinal);
		var template = resources.Keys.Where(r => r.StartsWith("template/", StringComparison.Ordinal)).Select(r => r["template/".Length..]).ToList();
		foreach (var path in template)
			files.WriteBytes(Path.Combine(Folder, path), Read(resources[$"template/{path}"]));
		foreach (var kit in template.Where(p => p.StartsWith("Kits/", StringComparison.Ordinal) && p.EndsWith("/kit.json", StringComparison.Ordinal)))
		{
			var folder = kit[..^"kit.json".Length];
			foreach (var take in resources.Keys.Where(r => r.StartsWith($"presets/{folder}", StringComparison.Ordinal) && r.EndsWith(".ogg", StringComparison.Ordinal)))
				files.WriteBytes(Path.Combine(Folder, take["presets/".Length..]), Read(resources[take]));
		}

		byte[] Read(string resource)
		{
			using var stream = assembly.GetManifestResourceStream(resource)!;
			using var bytes = new MemoryStream();
			stream.CopyTo(bytes);
			return bytes.ToArray();
		}
	}

	/// <summary>
	/// The project replaced by a <c>.lunelle</c> file's (<see cref="MusicArchive"/>), what the workbench was doing with
	/// the old one too; returns how many files it holds.
	/// </summary>
	/// <exception cref="InvalidDataException">It isn't a project's file, and nothing is changed.</exception>
	public static int Import(BrowserFiles files, byte[] archive)
	{
		var incoming = MusicArchive.Unpack(archive);
		foreach (var path in files.List(Folder).ToList())
			files.Delete(Path.Combine(Folder, path));
		foreach (var (path, bytes) in incoming)
			files.WriteBytes(Path.Combine(Folder, path), bytes);
		return incoming.Count;
	}
}
