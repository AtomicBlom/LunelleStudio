namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// The music a studio starts from when it has none: <c>samples/Showcase</c>, embedded in the app. The browser's one
/// project begins as it, and so does the desktop's default project in the user's documents.
/// </summary>
internal static class StarterProject
{
	/// <summary>
	/// Writes the showcase's music into <paramref name="folder"/> when it holds nothing yet, and says whether it did: its
	/// files as embedded, and the takes of each sampled kit (<c>Kits/&lt;name&gt;/kit.json</c>) from the presets' kit of
	/// that folder, which are the same and embedded once.
	/// </summary>
	public static bool SeedIfEmpty(IMusicFiles files, string folder)
	{
		ArgumentNullException.ThrowIfNull(files);
		if (files.List(folder).Any())
			return false;
		var assembly = typeof(StarterProject).Assembly;
		// %(RecursiveDir) names a resource with the build machine's separator
		var resources = assembly.GetManifestResourceNames().ToDictionary(r => r.Replace('\\', '/'), StringComparer.Ordinal);
		var template = resources.Keys.Where(r => r.StartsWith("template/", StringComparison.Ordinal)).Select(r => r["template/".Length..]).ToList();
		foreach (var path in template)
			files.WriteBytes(Path.Combine(folder, path), Read(resources[$"template/{path}"]));
		foreach (var kit in template.Where(p => p.StartsWith("Kits/", StringComparison.Ordinal) && p.EndsWith("/kit.json", StringComparison.Ordinal)))
		{
			var kitFolder = kit[..^"kit.json".Length];
			foreach (var take in resources.Keys.Where(r => r.StartsWith($"presets/{kitFolder}", StringComparison.Ordinal) && r.EndsWith(".ogg", StringComparison.Ordinal)))
				files.WriteBytes(Path.Combine(folder, take["presets/".Length..]), Read(resources[take]));
		}
		return true;

		byte[] Read(string resource)
		{
			using var stream = assembly.GetManifestResourceStream(resource)!;
			using var bytes = new MemoryStream();
			stream.CopyTo(bytes);
			return bytes.ToArray();
		}
	}
}
