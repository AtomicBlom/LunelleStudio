using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A sound the library offers: a patch or a kit by its id in the library (<c>keys/electric-piano</c>), the folder it is
/// in as its category, and what it is.
/// </summary>
/// <param name="Name">Its id in the library.</param>
/// <param name="IsKit">Whether it is a kit, rather than a patch.</param>
/// <param name="Category">What kind of sound: its folder (keys, bass), or a kit's "sampled" or "synthesised".</param>
/// <param name="About">What it is, from its file.</param>
public sealed record Preset(string Name, bool IsKit, string Category, string About)
{
	/// <summary>How a project names it: <c>lunelle:keys/electric-piano</c>.</summary>
	public string Reference => $"{PresetLibrary.LibraryName}:{Name}";
}

/// <summary>
/// The sounds a project of music files alone is offered (<see cref="Workbench.Presets"/>): a library, Lunelle's own by
/// default (<see cref="BuiltInLibrary"/>), its patches by folder and its kits, each kit with the patches its pieces play,
/// which aren't offered alone. A project uses one by referencing it (<c>lunelle:keys/electric-piano</c>), and adding one
/// makes an entry of the project's own based on it, to change without copying it (overrides.md).
/// </summary>
public sealed class PresetLibrary
{
	/// <summary>The name a project lists the library by, and its references' prefix.</summary>
	public const string LibraryName = BuiltInLibrary.Name;

	private readonly MusicFolder _folder;

	private PresetLibrary(MusicFolder folder) => _folder = folder;

	/// <summary>Lunelle's own library.</summary>
	public static PresetLibrary Lunelle { get; } = Load(BuiltInLibrary.Source);

	/// <summary>The sounds offered, patches by category then kits.</summary>
	public IReadOnlyList<Preset> Presets { get; private set; } = [];

	/// <summary>What couldn't be read in the library, a line each.</summary>
	public IReadOnlyList<string> Problems => _folder.Problems;

	/// <summary>The library's version, from its manifest (<c>1.0.0</c>), or null.</summary>
	public string? Version => _folder.Project.Root.Manifest.Version;

	/// <summary>The library in <paramref name="source"/>.</summary>
	public static PresetLibrary Load(IProjectSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		var library = new PresetLibrary(MusicFolder.Load(source, host: _ => null));
		library.Index();
		return library;
	}

	/// <summary>The library in the folder at <paramref name="directory"/>.</summary>
	public static PresetLibrary FromDirectory(string directory) => Load(new DirectorySource(directory));

	/// <summary>The patch with <paramref name="name"/>, built.</summary>
	/// <exception cref="KeyNotFoundException">There is none.</exception>
	public Patch BuildPatch(string name) =>
		_folder.Patches.FirstOrDefault(p => p.Name == name).Build?.Invoke() ?? throw new KeyNotFoundException($"There is no patch preset {name}.");

	/// <summary>The kit with <paramref name="name"/>, built.</summary>
	/// <exception cref="KeyNotFoundException">There is none.</exception>
	public DrumKit BuildKit(string name) =>
		_folder.Kits.FirstOrDefault(k => k.Name == name).Build?.Invoke() ?? throw new KeyNotFoundException($"There is no kit preset {name}.");

	/// <summary>The names the library gives the macros a preset's patches turn, by number: so a project can name them too.</summary>
	public IReadOnlyDictionary<string, int> MacrosFor(Preset preset)
	{
		ArgumentNullException.ThrowIfNull(preset);
		var macros = new Dictionary<string, int>(StringComparer.Ordinal);
		var resolved = preset.IsKit
			? _folder.Resolver.Resolve(_folder.Project.Root.Entry(EntryKind.Kit, preset.Name)!)?.Patches.Values.ToList() ?? []
			: _folder.Resolver.Resolve(_folder.Project.Root.Entry(EntryKind.Patch, preset.Name)!) is { } one ? [one] : [];
		foreach (var patch in resolved)
			foreach (var macro in patch.Json["macros"]?.Items ?? [])
				if (macro["macro"]?.Num is { } number && int.TryParse(number, out var id) && _folder.MacroName(id) is { } name)
					macros[name] = id;
		return macros;
	}

	// The presets listed: each kit with the patches its pieces play, which come with it and aren't offered alone
	private void Index()
	{
		var root = _folder.Project.Root;
		var kits = new List<Preset>();
		var piecePatches = new HashSet<string>(StringComparer.Ordinal);
		foreach (var (id, entry) in root.Entries(EntryKind.Kit))
		{
			if (_folder.Resolver.Resolve(entry) is not { Ok: true } kit)
				continue;
			piecePatches.UnionWith(kit.Patches.Keys);
			var sampled = kit.Json["pieces"]?.Items.Any(p => p["layers"] is not null) == true;
			kits.Add(new Preset(id, IsKit: true, sampled ? "sampled" : "synthesised", kit.Json["about"]?.Str ?? kit.Json["source"]?.Str ?? ""));
		}
		var patches = new List<Preset>();
		foreach (var (id, entry) in root.Entries(EntryKind.Patch))
		{
			if (piecePatches.Contains(id))
				continue;
			var category = id.LastIndexOf('/') is > 0 and var cut ? id[..cut] : "patches";
			patches.Add(new Preset(id, IsKit: false, category, entry.Json["about"]?.Str ?? ""));
		}
		Presets =
		[
			.. patches.OrderBy(p => p.Category, StringComparer.Ordinal).ThenBy(p => p.Name, StringComparer.Ordinal),
			.. kits.OrderBy(k => k.Name, StringComparer.Ordinal),
		];
	}
}
