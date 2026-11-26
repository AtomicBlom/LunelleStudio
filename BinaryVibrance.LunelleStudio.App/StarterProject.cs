namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// The music a studio starts from when it has none: <c>samples/Showcase</c>, embedded in the app. The browser's one
/// project begins as it, and so does the desktop's default project in the user's documents. Its sounds are Lunelle's own
/// library's, which every project can reference, so only its songs and cues are written.
/// </summary>
internal static class StarterProject
{
	/// <summary>Writes the showcase's music into <paramref name="folder"/> when it holds nothing yet, and says whether it did.</summary>
	public static bool SeedIfEmpty(IMusicFiles files, string folder)
	{
		ArgumentNullException.ThrowIfNull(files);
		if (files.List(folder).Any())
			return false;
		var assembly = typeof(StarterProject).Assembly;
		// %(RecursiveDir) names a resource with the build machine's separator
		foreach (var resource in assembly.GetManifestResourceNames())
		{
			var name = resource.Replace('\\', '/');
			if (!name.StartsWith("template/", StringComparison.Ordinal))
				continue;
			using var stream = assembly.GetManifestResourceStream(resource)!;
			using var bytes = new MemoryStream();
			stream.CopyTo(bytes);
			files.WriteBytes(Path.Combine(folder, name["template/".Length..]), bytes.ToArray());
		}
		return true;
	}
}
