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

	internal WorkbenchViewModel(Workbench workbench)
	{
		_workbench = workbench;
		Macros = [.. Enumerable.Range(0, 8).Select(m => new MacroViewModel(m, (index, value) => _workbench.SetBenchMacro(index, value)))];
	}

	/// <summary>The catalog's songs, one lit while it is the one chosen.</summary>
	public ObservableCollection<ChoiceViewModel> Songs { get; } = [];

	internal void AddSong(string name) => Songs.Add(new ChoiceViewModel(Songs.Count, name, index => SelectedSong = index));

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
		internal set => Set(ref _error, value);
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

	internal void AddPatch(string name) => Patches.Add(new ChoiceViewModel(Patches.Count, name, index => SelectedPatch = index));

	/// <summary>The catalog's kits, one lit while the bench plays it.</summary>
	public ObservableCollection<ChoiceViewModel> Kits { get; } = [];

	internal void AddKit(string name) => Kits.Add(new ChoiceViewModel(Kits.Count, name, index => SelectedKit = index));

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
}
