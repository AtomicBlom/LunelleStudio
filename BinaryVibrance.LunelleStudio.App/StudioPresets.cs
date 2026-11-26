namespace BinaryVibrance.LunelleStudio.App;

// The presets the studio ships (presets/ at the repository's top, embedded under presets/), each file read from the
// assembly only when it is asked for: a kit's samples when the kit is tried or added
internal static class StudioPresets
{
	private const string Prefix = "presets/";

	public static PresetLibrary Load()
	{
		var assembly = typeof(StudioPresets).Assembly;
		// %(RecursiveDir) names a resource with the build machine's separator
		var resources = assembly.GetManifestResourceNames()
			.Where(r => r.StartsWith(Prefix, StringComparison.Ordinal))
			.ToDictionary(r => r[Prefix.Length..].Replace('\\', '/'), StringComparer.Ordinal);
		var library = PresetLibrary.Load(resources.Keys, path =>
		{
			using var stream = assembly.GetManifestResourceStream(resources[path])!;
			using var bytes = new MemoryStream();
			stream.CopyTo(bytes);
			return bytes.ToArray();
		});
		foreach (var problem in library.Problems)
			Console.Error.WriteLine($"[studio] preset {problem}");
		return library;
	}
}
