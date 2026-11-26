using System.Reflection;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>A built project's music, read from the class its <see cref="MusicCatalogAttribute"/> names.</summary>
internal static class CatalogReader
{
	/// <summary>
	/// The catalog <paramref name="assembly"/> names, or null and why when it names none; with the project's music files
	/// (<paramref name="files"/>, its AdditionalFiles: format 2, under its <c>lunelle.meta.json</c>) as the songs' and
	/// patches' sources. A song edited in the workbench is resolved as its file is (MusicFolder.BuildSong), its sounds the
	/// project's built ones where it has them, its cues the catalog's own, its code the song's class's members.
	/// </summary>
	public static (WorkbenchCatalog? Catalog, string? Problem) Read(Assembly assembly, IReadOnlyList<string> files, IMusicFiles? source = null)
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

		// The project's music, from its manifest's folder, resolved as its files are
		var manifest = files.FirstOrDefault(f => Path.GetFileName(f).Equals(EntryIds.Manifest, StringComparison.OrdinalIgnoreCase));
		var root = manifest is null ? null : Path.GetDirectoryName(Path.GetFullPath(manifest))!;
		MusicFolder? music = null;
		if (root is not null)
		{
			try
			{
				// The studio's document where it has one, so an edit not yet saved is what the song is resolved against
				music = source is null
					? MusicFolder.FromDirectory(root)
					: MusicFolder.Load(new MemorySource(source.List(root), path => source.ReadBytes(Path.Combine(root, path)), root));
			}
			catch (DirectoryNotFoundException)
			{
			}
		}

		// A sound the project built: a property of the generated (or the game's own) class, by its id's path
		object? Built(string kind, string key)
		{
			var path = key.Split(':').SelectMany(p => p.Split('/')).Select(PatchCode.Pascal).ToArray();
			var holder = assembly.GetTypes().FirstOrDefault(t => t.Name == kind && t.DeclaringType is null);
			for (var i = 0; holder is not null && i < path.Length - 1; i++)
				holder = holder.GetNestedType(path[i], BindingFlags.Public | BindingFlags.NonPublic);
			return holder?.GetProperty(path[^1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
		}

		var songFiles = new Dictionary<string, SongSource>(StringComparer.Ordinal);
		foreach (var (id, _) in music?.Songs ?? [])
		{
			var file = Path.Combine(root!, music!.SongPath(id)!.Replace('/', Path.DirectorySeparatorChar));
			var stem = id[(id.LastIndexOf('/') + 1)..];
			var code = assembly.GetTypes().FirstOrDefault(t => t.Name == PatchCode.Pascal(stem) && t.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)?.ReturnType == typeof(Song));
			MethodInfo? Member(string member) => code?.GetMethod(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			SongBindings Code(SongBindings files) => new()
			{
				Patch = key => (key.StartsWith('~') ? null : Built("Patches", key) as Patch) ?? files.Patch(key),
				Kit = key => (key.StartsWith('~') ? null : Built("Kits", key) as DrumKit) ?? files.Kit(key),
				// The catalog's own cue, which the workbench's cue controls steer, by the name it is known by at run time
				Cue = reference => files.Cue(reference) is { } cue ? cues.FirstOrDefault(c => c.Name == cue.Name) ?? cue : null,
				Macro = files.Macro,
				Part = n => Member(n)?.Invoke(null, null) as Part,
				Placement = (n, section, track) => Member(n)?.Invoke(null, [section, track]) as Placement,
				Hook = (n, song, sections) => Member(n)?.Invoke(null, [song, .. sections]),
			};
			var folder = music;
			songFiles[id] = new SongSource(file, folder.Bindings, code?.FullName)
			{
				Build = (text, wrap) => folder.BuildSong(id, text, files => wrap(Code(files))),
			};
		}
		return (new WorkbenchCatalog
		{
			Songs = songs,
			Patches = patches,
			Kits = kits,
			Cues = cues,
			MacroName = macroName,
			SongSource = name => songFiles.GetValueOrDefault(name),
			Root = root,
			Patterns = music is null ? [] : [.. music.Project.Root.Entries(EntryKind.Pattern).Keys.Order(StringComparer.Ordinal)],
			ResolveSong = music is null ? null : (name, text) => music.Resolve(name, text) is var (song, problem) ? (song.Json, problem?.Message) : default,
			PatchFile = name => music?.PatchPath(name) is { } path ? Path.Combine(root!, path.Replace('/', Path.DirectorySeparatorChar)) : null,
			// The class the generator writes the patches' properties into
			PatchesType = assembly.GetTypes().FirstOrDefault(t => t.Name == "Patches" && t.DeclaringType is null
				&& t.GetProperty("Files", BindingFlags.Public | BindingFlags.Static) is not null)?.FullName,
		}, null);
	}
}
