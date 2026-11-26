using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BinaryVibrance.LunelleStudio;

/// <summary>Everything the workbench page shows and works: the song and transport, its sections, tracks and cues, the patch bench, the pattern.</summary>
public sealed class WorkbenchViewModel : ViewModel
{
	private readonly Workbench _workbench;
	private int _selectedSong = -1;
	private bool _isPlaying;
	private bool _loopSection;
	private string _positionText = "stopped";
	private float _masterLevel;
	private bool _hasVuMeter;
	private string _tempoText = "";
	private string _scaleText = "";
	private string _error = "";
	private string _statsText = "";
	private int _selectedPatch = -1;
	private int _selectedKit = -1;
	private int _octave = 4;
	private float _velocity = 0.8f;
	private WorkbenchMode _mode;

	internal WorkbenchViewModel(Workbench workbench)
	{
		_workbench = workbench;
		Macros = [.. Enumerable.Range(0, 8).Select(m => new MacroViewModel(m, (index, value) => _workbench.SetBenchMacro(index, value)))];
		Roll = new RollViewModel(workbench);
	}

	/// <summary>The catalog's songs, one lit while it is the one chosen.</summary>
	public ObservableCollection<ChoiceViewModel> Songs { get; } = [];

	internal void AddSong(string name) => Songs.Add(new ChoiceViewModel(Songs.Count, name, index => _workbench.ChooseSong(index)));

	/// <summary>The song chosen, by index. Setting it builds and plays it.</summary>
	public int SelectedSong
	{
		get => _selectedSong;
		set
		{
			if (value != _selectedSong && value >= 0 && value < Songs.Count)
				_workbench.SelectSong(value);
		}
	}

	internal void ShowSelectedSong(int index)
	{
		Set(ref _selectedSong, index, nameof(SelectedSong));
		foreach (var song in Songs)
			song.Show(song.Index == index);
	}

	/// <summary>Whether the song is playing. Setting it plays or stops it.</summary>
	public bool IsPlaying
	{
		get => _isPlaying;
		set
		{
			if (value == _isPlaying)
				return;
			if (value)
				_workbench.Play();
			else
				_workbench.Stop();
		}
	}

	internal void ShowPlaying(bool playing) => Set(ref _isPlaying, playing, nameof(IsPlaying));

	/// <summary>Whether the section playing is held. Setting it acts.</summary>
	public bool LoopSection
	{
		get => _loopSection;
		set
		{
			if (Set(ref _loopSection, value))
				_workbench.SetLoop(value);
		}
	}

	internal void ShowLoop(bool loop) => Set(ref _loopSection, loop, nameof(LoopSection));

	/// <summary>Where the song is: "verse · bar 3 · beat 2".</summary>
	public string PositionText
	{
		get => _positionText;
		internal set => Set(ref _positionText, value);
	}

	/// <summary>Where the VU meter's needle is, 0 (−20 VU) to 1 (+3 VU): the master's level, eased as a VU meter's needle is.</summary>
	public float MasterLevel
	{
		get => _masterLevel;
		internal set => Set(ref _masterLevel, value);
	}

	/// <summary>Whether the theme shows the master's level on a VU meter by the transport.</summary>
	public bool HasVuMeter
	{
		get => _hasVuMeter;
		internal set => Set(ref _hasVuMeter, value);
	}

	/// <summary>Its tempo: "138 bpm".</summary>
	public string TempoText
	{
		get => _tempoText;
		internal set => Set(ref _tempoText, value);
	}

	/// <summary>The scale it plays in: "E minor".</summary>
	public string ScaleText
	{
		get => _scaleText;
		internal set => Set(ref _scaleText, value);
	}

	/// <summary>What went wrong building the song (a notation's mistake, a patch's), or empty.</summary>
	public string Error
	{
		get => _error;
		internal set
		{
			var was = _error.Length > 0;
			if (!Set(ref _error, value))
				return;
			Changed(nameof(HasError));
			if (value.Length > 0 && !was)
				ErrorOpen = true;
			else if (value.Length == 0)
				ErrorOpen = false;
		}
	}

	/// <summary>The chip's load: "12 voices · 44 operators · 3% load".</summary>
	public string StatsText
	{
		get => _statsText;
		internal set => Set(ref _statsText, value);
	}

	/// <summary>The song's sections.</summary>
	public ObservableCollection<SectionViewModel> Sections { get; } = [];

	/// <summary>The song's tracks.</summary>
	public ObservableCollection<TrackViewModel> Tracks { get; } = [];

	/// <summary>The cues: the catalog's, and any other the song names.</summary>
	public ObservableCollection<CueViewModel> Cues { get; } = [];

	/// <summary>The catalog's patches, one lit while the bench plays it.</summary>
	public ObservableCollection<ChoiceViewModel> Patches { get; } = [];

	internal void AddPatch(string name) => Patches.Add(new ChoiceViewModel(Patches.Count, name, index => _workbench.ChoosePatch(index)));

	/// <summary>The catalog's kits, one lit while the bench plays it.</summary>
	public ObservableCollection<ChoiceViewModel> Kits { get; } = [];

	internal void AddKit(string name) => Kits.Add(new ChoiceViewModel(Kits.Count, name, index => _workbench.ChooseKit(index)));

	/// <summary>The presets the host ships (<see cref="Workbench.Presets"/>), for a project of files alone; none for one with code.</summary>
	public System.Collections.ObjectModel.ObservableCollection<PresetViewModel> Presets { get; } = [];

	/// <summary>Whether there are presets to offer, for the library to show their list.</summary>
	public bool HasPresets
	{
		get;
		internal set
		{
			if (Set(ref field, value))
				Changed(nameof(SoundsShown));
		}
	}

	/// <summary>Where in <see cref="Presets"/> the one on the keyboard is, or -1.</summary>
	public int TryingPreset
	{
		get;
		private set => Set(ref field, value);
	} = -1;

	/// <summary>What the preset on the keyboard says of itself, under the presets' heading; empty when none is.</summary>
	public string PresetAbout
	{
		get;
		private set => Set(ref field, value);
	} = "";

	internal void ShowTrying(Preset? preset)
	{
		var at = preset is null ? -1 : Presets.Select(p => p.Preset).ToList().IndexOf(preset);
		TryingPreset = at;
		PresetAbout = at < 0 ? "" : $"{preset!.Name} on the keys{(preset.IsKit ? " and pads" : "")}. {preset.About}".TrimEnd();
	}

	/// <summary>The patch the bench plays, by index, or −1. Setting it acts.</summary>
	public int SelectedPatch
	{
		get => _selectedPatch;
		set
		{
			if (value != _selectedPatch && value >= 0 && value < Patches.Count)
				_workbench.SelectPatch(value);
		}
	}

	/// <summary>The kit the bench's pads play, by index, or −1. Setting it acts.</summary>
	public int SelectedKit
	{
		get => _selectedKit;
		set
		{
			if (value != _selectedKit && value >= 0 && value < Kits.Count)
				_workbench.SelectKit(value);
		}
	}

	internal void ShowBench(int patch, int kit)
	{
		Set(ref _selectedPatch, patch, nameof(SelectedPatch));
		Set(ref _selectedKit, kit, nameof(SelectedKit));
		foreach (var choice in Patches)
			choice.Show(choice.Index == patch);
		foreach (var choice in Kits)
			choice.Show(choice.Index == kit);
	}

	/// <summary>The pieces of the kit chosen, a pad each.</summary>
	public ObservableCollection<PadViewModel> Pads { get; } = [];

	internal void ShowPads(IEnumerable<string> pieces)
	{
		Pads.Clear();
		foreach (var piece in pieces)
		{
			var index = Pads.Count;
			Pads.Add(new PadViewModel(piece, () => HitPad(index)));
		}
	}

	/// <summary>Whether Shift is held on the computer's keyboard, as the last frame's keys said.</summary>
	internal bool ShiftHeld { get; set; }

	/// <summary>How tall Arrange's part panel is, in DIPs: where its splitter was left.</summary>
	public float PartPanelHeight
	{
		get;
		set => Set(ref field, Math.Clamp(value, 120f, 900f));
	} = 300f;

	/// <summary>How far Arrange's lanes are scrolled, which its headers follow to stay put.</summary>
	public ArrangeScroll ArrangeScroll { get; } = new();

	/// <summary>The workbench this models, for the views that change the song through it.</summary>
	internal Workbench Workbench => _workbench;

	/// <summary>The song itself, its tempo and scale, for the inspector; null before a song has built.</summary>
	public SongViewModel? Song
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Shows the song's own settings (tempo, key, scale) in the inspector.</summary>
	public ICommand InspectSongCommand => field ??= new ActionCommand(() => _workbench.InspectSong());

	/// <summary>What the inspector shows: a <see cref="SectionViewModel"/>, a <see cref="TrackViewModel"/>, a <see cref="PlacementViewModel"/>, or null.</summary>
	public object? Inspected
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>The keyboard's octave: its lowest C is in it.</summary>
	public int Octave
	{
		get => _octave;
		set => Set(ref _octave, Math.Clamp(value, 1, 7));
	}

	/// <summary>How hard the keyboard plays, 0 to 1.</summary>
	public float Velocity
	{
		get => _velocity;
		set => Set(ref _velocity, Math.Clamp(value, 0.05f, 1f));
	}

	/// <summary>The bench patch's eight macros.</summary>
	public IReadOnlyList<MacroViewModel> Macros { get; }

	/// <summary>Sets one of the bench patch's macros, 0 to 1.</summary>
	public void SetMacro(int index, float value)
	{
		if ((uint)index < (uint)Macros.Count)
			Macros[index].Value = value;
	}

	/// <summary>The part chosen, as a grid.</summary>
	public PatternViewModel Pattern { get; } = new();

	/// <summary>The patch designer, which Patch mode shows: the patch chosen, as data under the hands.</summary>
	public PatchDesigner Designer { get; } = new();

	/// <summary>The beat sketcher, which Kit mode shows under the pads.</summary>
	public BeatSketch Sketch { get; } = new();

	/// <summary>What the last copy did: "C# copied", or why it couldn't.</summary>
	public string CopyStatus
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>Raised at each refresh (30 a second), for views that draw what is heard: the scope and spectrum.</summary>
	public event Action? Refreshed;

	internal void RaiseRefreshed() => Refreshed?.Invoke();

	/// <summary>Raised every frame, for views that draw what is heard as it is heard: the scope and spectrum.</summary>
	public event Action? Frame;

	internal void RaiseFrame() => Frame?.Invoke();

	/// <summary>Copies the newest frames heard on the workbench's bus into <paramref name="into"/>; returns how many.</summary>
	public int ReadScope(Span<float> into) => _workbench.ReadScope(into);

	/// <summary>The rate the workbench's audio runs at, for a spectrum's frequencies.</summary>
	public int SampleRate => _workbench.SampleRate;

	/// <summary>Plays a note on the bench (a key of the on-screen keyboard), at <see cref="Velocity"/>; returns it, to let go.</summary>
	public BinaryVibrance.Lunelle.Chip.NoteHandle PlayNote(int note) => _workbench.AuditionOn(note, Velocity);

	/// <summary>Lets a note from <see cref="PlayNote"/> go.</summary>
	public void ReleaseNote(BinaryVibrance.Lunelle.Chip.NoteHandle note) => _workbench.AuditionOff(note);

	/// <summary>Hits a pad: a piece of the bench's kit, by its index in <see cref="Pads"/>.</summary>
	public void HitPad(int index)
	{
		if ((uint)index < (uint)Pads.Count)
			_workbench.AuditionHit(new BinaryVibrance.Lunelle.Chip.DrumPiece(Pads[index].Piece), Velocity);
	}

	/// <summary>What the middle of the window is given to. Setting it acts: the keyboard then plays what the mode has chosen.</summary>
	public WorkbenchMode Mode
	{
		get => _mode;
		set
		{
			if (value != _mode)
				_workbench.SetMode(value);
		}
	}

	internal void ShowMode(WorkbenchMode mode)
	{
		if (!Set(ref _mode, mode, nameof(Mode)))
			return;
		Changed(nameof(IsArrange));
		Changed(nameof(IsPatch));
		Changed(nameof(IsKit));
		Changed(nameof(IsFiles));
	}

	/// <summary>Whether Arrange has the middle.</summary>
	public bool IsArrange
	{
		get => Mode == WorkbenchMode.Arrange;
		set
		{
			if (value)
				Mode = WorkbenchMode.Arrange;
		}
	}

	/// <summary>Whether the patch bench has the middle.</summary>
	public bool IsPatch
	{
		get => Mode == WorkbenchMode.Patch;
		set
		{
			if (value)
				Mode = WorkbenchMode.Patch;
		}
	}

	/// <summary>Whether the kit's pads have the middle.</summary>
	public bool IsKit
	{
		get => Mode == WorkbenchMode.Kit;
		set
		{
			if (value)
				Mode = WorkbenchMode.Kit;
		}
	}

	/// <summary>Whether the library (the songs, patches and kits) is open: over the view, or beside it while pinned.</summary>
	public bool LibraryOpen
	{
		get;
		set
		{
			if (Set(ref field, value))
				Changed(nameof(LibraryFlyout));
		}
	}

	/// <summary>Whether the library stays open as a column of its own, rather than folding away once something is chosen.</summary>
	public bool LibraryPinned
	{
		get;
		set
		{
			if (!Set(ref field, value))
				return;
			if (value)
				LibraryOpen = true;
			Changed(nameof(LibraryFlyout));
		}
	}

	/// <summary>Whether the library shows over the view: open, and not pinned.</summary>
	public bool LibraryFlyout => LibraryOpen && !LibraryPinned;

	/// <summary>Opens or closes the library; a pinned one is unpinned and closed.</summary>
	public void ToggleLibrary()
	{
		if (LibraryPinned)
		{
			LibraryPinned = false;
			LibraryOpen = false;
		}
		else
			LibraryOpen = !LibraryOpen;
	}

	/// <summary>What the library is filtered by: words each name must contain. Empty shows everything.</summary>
	public string LibraryQuery
	{
		get;
		set
		{
			if (Set(ref field, value ?? ""))
				FilterLibrary();
		}
	} = "";

	/// <summary>
	/// Shows the library's rows as the filter and the tree have them: a row whose id has every word typed, its folder open
	/// (or the words finding it inside a folded one), the first of a folder's rows heading it.
	/// </summary>
	internal void FilterLibrary()
	{
		var words = LibraryQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		Group("song", Songs, words);
		Group("patch", Patches, words);
		Group("kit", Kits, words);
		Group("pattern", LibraryPatterns, words);
		Group("cue", ProjectCues, words);
		// A preset by its name or its kind: "bass", "sampled"
		foreach (var preset in Presets)
			preset.IsVisible = words.All(w => preset.Name.Contains(w, StringComparison.OrdinalIgnoreCase)
				|| preset.Category.Contains(w, StringComparison.OrdinalIgnoreCase));
	}

	// The folders folded away, by kind and folder: "patch:bass"
	private readonly HashSet<string> _folded = [];

	// One kind's rows as a tree: each folder's header on the first of its rows the words keep, its rows hidden while folded
	private void Group<T>(string kind, IEnumerable<T> rows, string[] words) where T : LibraryRowViewModel
	{
		string? heading = null;
		foreach (var row in rows)
		{
			row.FoldsBy(folder => Fold(kind, folder));
			var found = words.All(w => row.Id.Contains(w, StringComparison.OrdinalIgnoreCase));
			var folded = words.Length == 0 && _folded.Contains(kind + ":" + row.Folder);
			row.IsFolded = folded;
			row.ShowsFolder = found && row.Folder.Length > 0 && row.Folder != heading;
			row.IsVisible = found && !folded;
			if (found)
				heading = row.Folder;
		}
	}

	/// <summary>Folds a kind's folder away (<c>"patch"</c>, <c>"bass"</c>), or opens it again.</summary>
	internal void Fold(string kind, string folder)
	{
		if (!_folded.Remove(kind + ":" + folder))
			_folded.Add(kind + ":" + folder);
		FilterLibrary();
	}

	/// <summary>Whether the library shows songs only (with any other kinds ticked); none ticked shows every kind.</summary>
	public bool FilterSongs { get; set { if (Set(ref field, value)) KindsChanged(); } }

	/// <summary>Whether the library shows patches, among the kinds ticked.</summary>
	public bool FilterPatches { get; set { if (Set(ref field, value)) KindsChanged(); } }

	/// <summary>Whether the library shows kits, among the kinds ticked.</summary>
	public bool FilterKits { get; set { if (Set(ref field, value)) KindsChanged(); } }

	/// <summary>Whether the library shows patterns, among the kinds ticked.</summary>
	public bool FilterPatterns { get; set { if (Set(ref field, value)) KindsChanged(); } }

	/// <summary>Whether the library shows cues, among the kinds ticked.</summary>
	public bool FilterCues { get; set { if (Set(ref field, value)) KindsChanged(); } }

	/// <summary>Whether the library shows Lunelle's sounds, among the kinds ticked.</summary>
	public bool FilterSounds { get; set { if (Set(ref field, value)) KindsChanged(); } }

	private bool Shows(bool ticked) => ticked || !(FilterSongs || FilterPatches || FilterKits || FilterPatterns || FilterCues || FilterSounds);

	/// <summary>Whether the library's songs show: no kind ticked, or songs.</summary>
	public bool SongsShown => Shows(FilterSongs);

	/// <summary>Whether the library's patches show.</summary>
	public bool PatchesShown => Shows(FilterPatches);

	/// <summary>Whether the library's kits show.</summary>
	public bool KitsShown => Shows(FilterKits);

	/// <summary>Whether the library's patterns show: the project has some, and the kinds let them.</summary>
	public bool PatternsShown => HasPatterns && Shows(FilterPatterns);

	/// <summary>Whether the library's cues show: the project has cue files, and the kinds let them.</summary>
	public bool CuesShown => HasProjectCues && Shows(FilterCues);

	/// <summary>Whether Lunelle's sounds show: there are some to offer, and the kinds let them.</summary>
	public bool SoundsShown => HasPresets && Shows(FilterSounds);

	private void KindsChanged()
	{
		foreach (var name in new[] { nameof(SongsShown), nameof(PatchesShown), nameof(KitsShown), nameof(PatternsShown), nameof(CuesShown), nameof(SoundsShown) })
			Changed(name);
	}

	// The library's songs, patches or kits for a new catalog: left as they are when the names haven't changed
	internal static void ReplaceChoices(ObservableCollection<ChoiceViewModel> list, IReadOnlyList<string> names, Action<string> add)
	{
		if (list.Select(c => c.Name).SequenceEqual(names))
			return;
		list.Clear();
		foreach (var name in names)
			add(name);
	}

	/// <summary>A choice was made in the library: a flyout folds away; a pinned library stays.</summary>
	internal void Chose()
	{
		if (!LibraryPinned)
			LibraryOpen = false;
	}

	/// <summary>The song chosen's name, for the transport.</summary>
	public string SongName
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>Whether the song has any tracks; with none, Arrange offers a button to add the first.</summary>
	public bool HasTracks
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>The last build, for the status pill: "built 14:02:31", or what failed.</summary>
	public string BuildText
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>Whether the last build failed (and the last that built plays on).</summary>
	public bool HasError => Error.Length > 0;

	/// <summary>Whether the failed build's error is open under the transport. Opens by itself when a build fails; never open without one.</summary>
	public bool ErrorOpen
	{
		get;
		set => Set(ref field, value && HasError);
	}

	/// <summary>Opens or closes the failed build's error.</summary>
	public void ToggleError() => ErrorOpen = HasError && !ErrorOpen;

	/// <summary>What plays after the section playing, and why: "Next: open (conducted)".</summary>
	public string NextText
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>What the keyboard plays: "keys · electric-piano", "patch flute".</summary>
	public string DockLabel
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>Whether a text field has the keys, so the workbench's own keys (notes, Space, 1 to 9) leave them alone.</summary>
	public bool IsTyping { get; set; }

	/// <summary>Whether the command palette is open.</summary>
	public bool PaletteOpen
	{
		get;
		set
		{
			if (!Set(ref field, value))
				return;
			if (value)
			{
				PaletteQuery = "";
				FillPalette();
			}
		}
	}

	/// <summary>What the palette is filtered by.</summary>
	public string PaletteQuery
	{
		get;
		set
		{
			if (Set(ref field, value ?? ""))
				FillPalette();
		}
	} = "";

	/// <summary>What the palette offers for its query, the first selected.</summary>
	public ObservableCollection<PaletteItemViewModel> PaletteItems { get; } = [];

	private int _paletteIndex;

	private void FillPalette()
	{
		var words = PaletteQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		PaletteItems.Clear();
		foreach (var item in _workbench.PaletteCommands())
			if (item.Matches(words))
			{
				item.Chosen = () => RunPalette(PaletteItems.IndexOf(item));
				PaletteItems.Add(item);
				if (PaletteItems.Count == 40)
					break;
			}
		MovePalette(-_paletteIndex);
	}

	/// <summary>Moves the palette's selection by <paramref name="by"/>, within what it offers.</summary>
	public void MovePalette(int by)
	{
		if (PaletteItems.Count == 0)
		{
			_paletteIndex = 0;
			return;
		}
		_paletteIndex = Math.Clamp(_paletteIndex + by, 0, PaletteItems.Count - 1);
		for (var i = 0; i < PaletteItems.Count; i++)
			PaletteItems[i].IsSelected = i == _paletteIndex;
	}

	/// <summary>Runs the palette's selection and closes it.</summary>
	public void RunPalette() => RunPalette(_paletteIndex);

	/// <summary>Runs one of the palette's items and closes it.</summary>
	public void RunPalette(int index)
	{
		if ((uint)index >= (uint)PaletteItems.Count)
			return;
		var item = PaletteItems[index];
		PaletteOpen = false;
		IsTyping = false;
		item.Run();
	}

	/// <summary>The part chosen as the roll edits it, when its song has a file and the part is data.</summary>
	public RollViewModel Roll { get; }

	/// <summary>Whether the Files view has the middle.</summary>
	public bool IsFiles
	{
		get => Mode == WorkbenchMode.Files;
		set
		{
			if (value)
				Mode = WorkbenchMode.Files;
		}
	}

	/// <summary>The Files view: the music as files, to read and copy.</summary>
	public FilesViewModel Files { get; } = new();

	/// <summary>Something the host needs seen, beside the build pill until it goes: "StudioExtras changed: restart to take it up".</summary>
	public string Notice { get; set => Set(ref field, value); } = "";

	/// <summary>The project's patterns, for the library: each put into the part chosen by its use.</summary>
	public System.Collections.ObjectModel.ObservableCollection<PatternEntryViewModel> LibraryPatterns { get; } = [];

	/// <summary>Whether the project has patterns to list.</summary>
	public bool HasPatterns { get; set { if (Set(ref field, value)) Changed(nameof(PatternsShown)); } }

	/// <summary>The project's cues, for the cues panel: each with whether the open song imports it.</summary>
	public System.Collections.ObjectModel.ObservableCollection<CueEntryViewModel> ProjectCues { get; } = [];

	/// <summary>A cue's menu asked for (its "…"): the view shows it.</summary>
	public event Action<CueEntryViewModel>? CueMenuRequested;

	internal void RequestCueMenu(CueEntryViewModel cue) => CueMenuRequested?.Invoke(cue);

	/// <summary>Whether the project has cue files to list (a project of format 2).</summary>
	public bool HasProjectCues { get; set { if (Set(ref field, value)) Changed(nameof(CuesShown)); } }

	/// <summary>Whether an export is being recorded from the cues moved as the song plays.</summary>
	public bool IsRecording { get; set => Set(ref field, value); }

	/// <summary>Whether the project has edits not yet saved (the document's, docs/spec/transactions.md).</summary>
	public bool IsDirty { get; set => Set(ref field, value); }
}
