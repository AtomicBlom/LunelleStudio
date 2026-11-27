using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

// The presets: a library's sounds (a PresetLibrary, Lunelle's own), listed in the library under the project's own for a
// project of files alone, each tried on the keyboard as it is and added to the project as an entry based on it
public sealed partial class Workbench
{
	/// <summary>
	/// The sounds the host ships, offered in the library to a project of music files alone (whose catalog has a
	/// <see cref="WorkbenchCatalog.Folder"/>): each tried on the keyboard before it is added, and copied into the project
	/// when it is (<see cref="AddPreset"/>). Null offers none.
	/// </summary>
	public PresetLibrary? Presets
	{
		get;
		set
		{
			field = value;
			ShowPresets();
		}
	}

	// The library's presets for the catalog: listed for a project of files alone, each marked when the project has its name
	private void ShowPresets()
	{
		var offered = Presets is not null && _catalog.Folder is not null ? Presets.Presets : [];
		if (!ViewModel.Presets.Select(p => p.Preset).SequenceEqual(offered))
		{
			ViewModel.Presets.Clear();
			foreach (var preset in offered)
				ViewModel.Presets.Add(new PresetViewModel(preset, this));
			ViewModel.FilterLibrary();
		}
		foreach (var preset in ViewModel.Presets)
			preset.InProject = InProject(preset.Preset);
		ViewModel.HasPresets = ViewModel.Presets.Count > 0;
	}

	private bool InProject(Preset preset) =>
		preset.IsKit ? Index(_catalog.Kits, preset.Name) >= 0 : Index(_catalog.Patches, preset.Name) >= 0;

	/// <summary>
	/// Puts <paramref name="preset"/> on the keyboard (and a kit's pieces on the pads) as the library has it, nothing
	/// copied: to hear it before adding it. Choosing anything else takes the keyboard back.
	/// </summary>
	public void TryPreset(Preset preset)
	{
		ArgumentNullException.ThrowIfNull(preset);
		if (Presets is not { } library)
			return;
		Patch? patch = null;
		DrumKit? kit = null;
		try
		{
			if (preset.IsKit)
				kit = library.BuildKit(preset.Name);
			else
				patch = library.BuildPatch(preset.Name);
		}
		catch (Exception ex)
		{
			ViewModel.Error = $"{preset.Name}: {ex.Message}";
			Console.WriteLine($"[workbench] the preset {preset.Name} didn't build: {ex.Message}");
			return;
		}
		var chip = NewBench();
		_benchInstrument = kit is not null ? chip.AddKit(kit) : chip.AddInstrument(patch!);
		_benchKit = kit;
		_benchPatch = -1;
		_benchKitIndex = -1;
		_dockTrack = null;
		ViewModel.ShowBench(-1, -1);
		ViewModel.ShowPads(kit is null ? [] : kit.Pieces.Select(p => p.Piece.Id));
		ViewModel.DockLabel = $"preset {preset.Name}";
		ViewModel.ShowTrying(preset);
	}

	/// <summary>
	/// Adds <paramref name="preset"/> to the project as an entry of its own based on it (overrides.md §2): a patch at
	/// <c>Patches/&lt;id&gt;.patch.json</c> or a kit at <c>Kits/&lt;id&gt;.kit.json</c>, saying only <c>"base":
	/// "lunelle:&lt;id&gt;"</c>, so it sounds as the library's until the project changes it, and follows the library where
	/// it doesn't. The project's manifest lists the library, and names the macros the sound turns where it names neither
	/// the name nor the number. It opens once the folder is read again; one the project has already opens as it is. Why it
	/// can't be added, or null.
	/// </summary>
	public string? AddPreset(Preset preset)
	{
		ArgumentNullException.ThrowIfNull(preset);
		if (Presets is not { } library)
			return "There are no presets here.";
		if (_catalog.Folder is not { } folder)
			return "Presets go into a project of music files alone, and this one's music is C#.";
		if (preset.IsKit && Index(_catalog.Kits, preset.Name) is >= 0 and var kit)
		{
			ChooseKit(kit);
			return null;
		}
		if (!preset.IsKit && Index(_catalog.Patches, preset.Name) is >= 0 and var patch)
		{
			ChoosePatch(patch);
			return null;
		}
		var kind = preset.IsKit ? EntryKind.Kit : EntryKind.Patch;
		var path = Path.Combine(folder, EntryIds.Folder(kind) + preset.Name + EntryIds.Suffix(kind));
		if (Files.Exists(path))
			return $"The project has {EntryIds.Folder(kind)}{preset.Name}{EntryIds.Suffix(kind)} already, and it doesn't read. Mend or rename it to add {preset.Name}.";
		if (EditManifest(folder, manifest => ListLibrary(manifest, library) | NameMacros(manifest, library.MacrosFor(preset))) is { } problem)
			return problem;
		var entry = new JObject()
			.Set("format", new JString(ProjectLoader.Format(kind)))
			.Set("base", new JString(preset.Reference));
		Files.WriteText(path, JWriter.Write(entry) + "\n");
		Awaiting(preset.Name, preset.IsKit ? Awaited.Kit : Awaited.Patch, $"Added {preset.Name}, based on {preset.Reference}. It opens once the project is read again.");
		return null;
	}

	// Changes the project's manifest by `change`, writing it when it says it changed; why it couldn't be, or null
	private string? EditManifest(string folder, Func<JObject, bool> change)
	{
		var path = Path.Combine(folder, EntryIds.Manifest);
		JObject manifest;
		try
		{
			manifest = Files.Exists(path)
				? JReader.Parse(Files.ReadText(path)) as JObject ?? throw new FormatError("the manifest isn't an object", 0, 0)
				: new JObject().Set("format", new JString(EntryIds.ManifestFormat)).Set("name", new JString("music"));
		}
		catch (Exception ex) when (ex is FormatError or IOException)
		{
			return $"The project's {EntryIds.Manifest} can't be read, so nothing is added: {ex.Message}";
		}
		if (change(manifest))
			Files.WriteText(path, JWriter.Write(manifest) + "\n");
		return null;
	}

	// The library listed in "libraries", at its major version, unless it is there already
	private static bool ListLibrary(JObject manifest, PresetLibrary library)
	{
		if (manifest["libraries"] is not JObject libraries)
			manifest.Set("libraries", libraries = new JObject());
		if (libraries[PresetLibrary.LibraryName] is not null)
			return false;
		var major = library.Version is { } version && SemVer.TryParse(version, out var parsed) ? parsed.Major.ToString(System.Globalization.CultureInfo.InvariantCulture) : "1";
		libraries.Set(PresetLibrary.LibraryName, new JObject().Set("version", new JString(major)));
		return true;
	}

	// The macros a preset's patches turn, named in the manifest where it names neither that name nor that number, so a
	// song can follow a cue with them
	private static bool NameMacros(JObject manifest, IReadOnlyDictionary<string, int> macros)
	{
		if (macros.Count == 0)
			return false;
		if (manifest["macros"] is { } existing && existing is not JObject)
			return false;
		if (manifest["macros"] is not JObject named)
			manifest.Set("macros", named = new JObject());
		var taken = named.Members.Select(m => (m.Value is JObject o ? o["number"] : m.Value)?.Num is { } n && int.TryParse(n, out var id) ? id : -1).ToHashSet();
		var changed = false;
		foreach (var (name, id) in macros.OrderBy(m => m.Value))
		{
			if (named[name] is not null || !taken.Add(id))
				continue;
			named.Set(name, new JNumber(id));
			changed = true;
		}
		return changed;
	}
}
