using System.Collections.ObjectModel;

namespace Loom.Audio.Workbench;

/// <summary>Everything the workbench page shows and works: the song and transport, its sections, tracks and cues, the patch bench, the pattern.</summary>
public sealed class WorkbenchViewModel : ViewModel
{
	private readonly Workbench _workbench;
	private int _selectedSong = -1;
	private bool _isPlaying;
	private bool _loopSection;
	private string _positionText = "stopped";
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

	/// <summary>The keyboard's octave: its lowest C is in it.</summary>
	public int Octave
	{
		get => _octave;
		set => Set(ref _octave, Math.Clamp(value, 0, 8));
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

	/// <summary>Copies the newest frames heard on the workbench's bus into <paramref name="into"/>; returns how many.</summary>
	public int ReadScope(Span<float> into) => _workbench.ReadScope(into);

	/// <summary>The rate the workbench's audio runs at, for a spectrum's frequencies.</summary>
	public int SampleRate => _workbench.SampleRate;

	/// <summary>Plays a note on the bench (a key of the on-screen keyboard), at <see cref="Velocity"/>; returns it, to let go.</summary>
	public Synth.Chip.NoteHandle PlayNote(int note) => _workbench.AuditionOn(note, Velocity);

	/// <summary>Lets a note from <see cref="PlayNote"/> go.</summary>
	public void ReleaseNote(Synth.Chip.NoteHandle note) => _workbench.AuditionOff(note);

	/// <summary>Hits a pad: a piece of the bench's kit, by its index in <see cref="Pads"/>.</summary>
	public void HitPad(int index)
	{
		if ((uint)index < (uint)Pads.Count)
			_workbench.AuditionHit(new Synth.Chip.DrumPiece(Pads[index].Piece), Velocity);
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

	internal void FilterLibrary()
	{
		var words = LibraryQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		foreach (var list in new[] { Songs, Patches, Kits })
			foreach (var choice in list)
				choice.IsVisible = words.All(w => choice.Name.Contains(w, StringComparison.OrdinalIgnoreCase));
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
}
