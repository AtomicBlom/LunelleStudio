using System.Text.Json;
using System.Text.Json.Nodes;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

// The presets: sounds the host ships (a PresetLibrary), listed in the library under the project's own for a project of
// files alone, each tried on the keyboard as it is and copied into the project when added
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
	/// Copies <paramref name="preset"/> into the project, with what it needs: a patch its file in <c>Patches/</c>; a kit its
	/// manifest, its samples and licence or each piece's patch; and, in <c>lunelle-project.json</c>, the names the library
	/// gives the macros its patches turn, where the project uses neither the name nor the number. It opens once the folder
	/// is read again. One the project has by name already is opened as the project has it. Why it can't be, or null.
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

		// Every file checked before any is written: one the project has already is the same, or the preset isn't added
		var writes = new List<(string Path, byte[] Bytes)>();
		foreach (var file in library.FilesFor(preset))
		{
			var path = Path.Combine(folder, file.Path);
			// A piece's patch the project has already, wherever its file is, is taken as it is when it is the same
			var have = file.Patch is not null && _catalog.PatchFile(file.Patch) is { } own && Files.Exists(own) ? own
				: Files.Exists(path) ? path : null;
			if (have is null)
				writes.Add((path, file.Bytes));
			else if (!Same(Files.ReadBytes(have), file.Bytes))
				return $"The project has {Path.GetRelativePath(folder, have).Replace('\\', '/')} already, and it isn't the preset's. Rename it to add {preset.Name}.";
		}
		foreach (var (path, bytes) in writes)
			Files.WriteBytes(path, bytes);
		NameMacros(folder, library.MacrosFor(preset));
		Awaiting(preset.Name, preset.IsKit ? Awaited.Kit : Awaited.Patch, $"Added {preset.Name} from the presets. It opens once the project is read again.");
		return null;
	}

	// The same bytes, but for line endings: a text file the library keeps with CRLF is still the same file
	private static bool Same(byte[] a, byte[] b) =>
		a.AsSpan().SequenceEqual(b) || (Text(a) is { } x && Text(b) is { } y && x.ReplaceLineEndings() == y.ReplaceLineEndings());

	private static string? Text(byte[] bytes)
	{
		try
		{
			return new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
		}
		catch (System.Text.DecoderFallbackException)
		{
			return null;
		}
	}

	// The macros a preset's patches turn, named in the project file where it names neither that name nor that number, so
	// a song can follow a cue with them; a project file that can't be read is left for its owner to mend
	private void NameMacros(string folder, IReadOnlyDictionary<string, int> macros)
	{
		if (macros.Count == 0)
			return;
		var path = Path.Combine(folder, MusicFolder.ProjectFileName);
		JsonObject project;
		try
		{
			project = Files.Exists(path)
				? JsonNode.Parse(Files.ReadText(path), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject ?? new JsonObject()
				: new JsonObject { ["format"] = MusicFolder.ProjectFormat };
		}
		catch (Exception ex) when (ex is JsonException or IOException)
		{
			Console.WriteLine($"[workbench] {path} couldn't be read to name the preset's macros: {ex.Message}");
			return;
		}
		if (project["macros"] is { } existing && existing is not JsonObject)
			return;
		if (project["macros"] is not JsonObject named)
			project["macros"] = named = [];
		var taken = named.Select(m => m.Value is JsonValue value && value.TryGetValue<int>(out var id) ? id : -1).ToHashSet();
		var changed = false;
		foreach (var (name, id) in macros.OrderBy(m => m.Value))
		{
			if (named.ContainsKey(name) || !taken.Add(id))
				continue;
			named[name] = id;
			changed = true;
		}
		if (changed)
			Files.WriteText(path, SongFile.Write(project));
	}
}
