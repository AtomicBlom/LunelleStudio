using System.Reflection;

using BinaryVibrance.Sirrow.Synth.Chip;
using BinaryVibrance.Sirrow.Synth.Music;

namespace Loom.Audio.Workbench.Projects;

/// <summary>A built project's music, read from the class its <see cref="MusicCatalogAttribute"/> names.</summary>
internal static class CatalogReader
{
	/// <summary>
	/// The catalog <paramref name="assembly"/> names, or null and why when it names none; with the project's music files
	/// (<paramref name="files"/>, its AdditionalFiles) as the songs' and patches' sources.
	/// </summary>
	public static (WorkbenchCatalog? Catalog, string? Problem) Read(Assembly assembly, IReadOnlyList<string> files)
	{
		if (assembly.GetCustomAttribute<MusicCatalogAttribute>() is not { } attribute)
			return (null, $"{assembly.GetName().Name} names no catalog: add [assembly: MusicCatalog(typeof(...))] to it.");
		var type = attribute.Catalog;
		T? Get<T>(string name) where T : class =>
			type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as T;
		var macroMethod = type.GetMethod("MacroName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, [typeof(int)]);
		var macroName = macroMethod?.ReturnType == typeof(string) ? macroMethod.CreateDelegate<Func<int, string?>>() : null;
		var songs = Get<IReadOnlyList<(string, Func<Song>)>>("Songs") ?? [];
		var patches = Get<IReadOnlyList<(string Name, Func<Patch> Build)>>("Patches") ?? [];
		var kits = Get<IReadOnlyList<(string Name, Func<DrumKit> Build)>>("Kits") ?? [];
		var cues = Get<IReadOnlyList<CueBase>>("Cues") ?? [];

		var patchFiles = files.Where(f => f.EndsWith(".patch.json", StringComparison.OrdinalIgnoreCase))
			.GroupBy(Stem, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
		var songFiles = new Dictionary<string, SongSource>(StringComparer.Ordinal);
		foreach (var file in files.Where(f => f.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase)))
		{
			if (SongName(file) is not { } name)
				continue;
			var code = assembly.GetTypes().FirstOrDefault(t => t.Name == PatchCode.Pascal(Stem(file)) && t.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)?.ReturnType == typeof(Song));
			MethodInfo? Member(string member) => code?.GetMethod(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			songFiles.TryAdd(name, new SongSource(file, new SongBindings
			{
				Patch = n => patches.FirstOrDefault(p => p.Name == n).Build?.Invoke(),
				Kit = n => kits.FirstOrDefault(k => k.Name == n).Build?.Invoke(),
				Cue = n => cues.FirstOrDefault(c => c.Name == n),
				Macro = n => MacroNamed(n, macroName),
				Part = n => Member(n)?.Invoke(null, null) as Part,
				Placement = (n, section, track) => Member(n)?.Invoke(null, [section, track]) as Placement,
				Hook = (n, song, sections) => Member(n)?.Invoke(null, [song, .. sections]),
			}, code?.FullName));
		}
		return (new WorkbenchCatalog
		{
			Songs = songs,
			Patches = patches,
			Kits = kits,
			Cues = cues,
			MacroName = macroName,
			SongSource = name => songFiles.GetValueOrDefault(name),
			PatchFile = name => patchFiles.GetValueOrDefault(name),
			// The class a patch file's property is generated into: the one with a static Patch property named for the file
			PatchesType = patchFiles.Keys.Select(stem => assembly.GetTypes().FirstOrDefault(t =>
				t.GetProperty(PatchCode.Pascal(stem), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.PropertyType == typeof(Patch)))
				.FirstOrDefault(t => t is not null)?.FullName,
		}, null);
	}

	// "open-road.song.json" is "open-road"
	private static string Stem(string path)
	{
		var name = Path.GetFileName(path);
		var at = name.IndexOf('.', StringComparison.Ordinal);
		return at < 0 ? name : name[..at];
	}

	private static string? SongName(string path)
	{
		try
		{
			using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
			return document.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
		}
		catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
		{
			return null;
		}
	}

	// A macro by the name a song file gives it ("bright"): the one the catalog names "Macros.Bright"
	private static Macro? MacroNamed(string name, Func<int, string?>? macroName)
	{
		if (macroName is null)
			return null;
		for (var id = 0; id < Macro.Count; id++)
			if (macroName(id) is { } code && string.Equals(code[(code.LastIndexOf('.') + 1)..], PatchCode.Pascal(name), StringComparison.Ordinal))
				return new Macro(id);
		return null;
	}
}
