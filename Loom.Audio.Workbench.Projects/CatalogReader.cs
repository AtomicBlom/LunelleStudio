using System.Reflection;

using Loom.Audio.Synth.Chip;
using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench.Projects;

/// <summary>A built project's music, read from the class its <see cref="MusicCatalogAttribute"/> names.</summary>
internal static class CatalogReader
{
	/// <summary>The catalog <paramref name="assembly"/> names, or null and why when it names none.</summary>
	public static (WorkbenchCatalog? Catalog, string? Problem) Read(Assembly assembly)
	{
		if (assembly.GetCustomAttribute<MusicCatalogAttribute>() is not { } attribute)
			return (null, $"{assembly.GetName().Name} names no catalog: add [assembly: MusicCatalog(typeof(...))] to it.");
		var type = attribute.Catalog;
		T? Get<T>(string name) where T : class =>
			type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as T;
		var macroName = type.GetMethod("MacroName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, [typeof(int)]);
		return (new WorkbenchCatalog
		{
			Songs = Get<IReadOnlyList<(string, Func<Song>)>>("Songs") ?? [],
			Patches = Get<IReadOnlyList<(string, Func<Patch>)>>("Patches") ?? [],
			Kits = Get<IReadOnlyList<(string, Func<DrumKit>)>>("Kits") ?? [],
			Cues = Get<IReadOnlyList<CueBase>>("Cues") ?? [],
			MacroName = macroName?.ReturnType == typeof(string) ? macroName.CreateDelegate<Func<int, string?>>() : null,
		}, null);
	}
}
