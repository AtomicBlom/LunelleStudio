using System.Windows.Input;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A part a track plays in a section: what it plays through the section (every pass of it, and its fill), its condition,
/// whether it is on now, where it is (which pass, the tick in it), and whether it is forced on or off.
/// </summary>
public sealed class PlacementViewModel : ViewModel
{
	private readonly Workbench _workbench;
	private bool _isActive;
	private PlacementState _state;

	internal PlacementViewModel(Workbench workbench, Placement placement, string conditionText, PlacementState state)
	{
		_workbench = workbench;
		Placement = placement;
		Name = $"{placement.Name} ({placement.Section.Name})";
		ConditionText = conditionText;
		_state = state;
	}

	internal Placement Placement { get; }

	/// <summary>The workbench it belongs to, for a view that changes it.</summary>
	internal Workbench Workbench => _workbench;

	/// <summary>Its name, and its section's.</summary>
	public string Name { get; }

	/// <summary>When it plays, in words: "when energy above 0.35", "level follows energy"; empty when always.</summary>
	public string ConditionText { get; }

	/// <summary>Whether it is playing now.</summary>
	public bool IsActive
	{
		get => _isActive;
		internal set => Set(ref _isActive, value);
	}

	/// <summary>By its condition, or held on or off. Setting it acts.</summary>
	public PlacementState State
	{
		get => _state;
		set
		{
			if (!Set(ref _state, value))
				return;
			Changed(nameof(IsAuto));
			Changed(nameof(IsOn));
			Changed(nameof(IsOff));
			_workbench.Force(this, value);
		}
	}

	/// <summary>Whether it plays by its condition. Setting it true makes it so; false does nothing, since another state is chosen instead.</summary>
	public bool IsAuto
	{
		get => State == PlacementState.Auto;
		set
		{
			if (value)
				State = PlacementState.Auto;
		}
	}

	/// <summary>Whether it is held on. Setting it true makes it so; false does nothing.</summary>
	public bool IsOn
	{
		get => State == PlacementState.On;
		set
		{
			if (value)
				State = PlacementState.On;
		}
	}

	/// <summary>Whether it is held off. Setting it true makes it so; false does nothing.</summary>
	public bool IsOff
	{
		get => State == PlacementState.Off;
		set
		{
			if (value)
				State = PlacementState.Off;
		}
	}

	/// <summary>Whether it has a condition to show.</summary>
	public bool HasCondition => ConditionText.Length > 0;

	/// <summary>Its own name, as a clip shows it: "the tune".</summary>
	public string Title => Placement.Name;

	/// <summary>What it plays: "notes" or "hits", and "on its phrase" when it waits for one.</summary>
	public string KindText { get; internal init; } = "";

	/// <summary>Whether its condition holds for the cues as they are now; true when it has none.</summary>
	public bool ConditionMet
	{
		get;
		internal set => Set(ref field, value);
	} = true;

	/// <summary>Whether it is the part chosen, shown in the roll.</summary>
	public bool IsSelected
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>What it plays through its whole section, every pass and its fill: notes by row, over <see cref="LengthTicks"/>.</summary>
	public IReadOnlyList<PatternNote> Notes { get; internal init; } = [];

	/// <summary>How long one pass of its part is, in ticks: whole bars.</summary>
	public int LoopTicks { get; internal init; }

	/// <summary>How long its section is, in ticks.</summary>
	public int LengthTicks { get; internal init; }

	/// <summary>Where its fill starts in the section, in ticks, or -1 for none.</summary>
	public int FillTick { get; internal init; } = -1;

	/// <summary>Its track's place in the song, which gives it its hue.</summary>
	public int TrackIndex { get; internal init; }

	/// <summary>Its condition as a song file writes it ("intensity &gt; 0.5 and pass &lt; 2"), or null for none.</summary>
	public string? When { get; internal init; }

	/// <summary>Its condition as its chip shows it: as written, or "always".</summary>
	public string WhenShown => When ?? "always";

	/// <summary>Which pass of it is playing, from 0, or -1 while its section isn't.</summary>
	public int PassPlaying
	{
		get;
		internal set => Set(ref field, value);
	} = -1;

	/// <summary>The tick in its section the song is at, or -1 while its section isn't playing.</summary>
	public int SectionTick
	{
		get;
		internal set => Set(ref field, value);
	} = -1;

	/// <summary>Sets when it plays, as a song file writes a condition (null: always); the reason it can't be, or null.</summary>
	public string? SetWhen(string? when) => _workbench.SetCondition(Placement, when);

	/// <summary>Whether it is a line, which can follow the chords.</summary>
	public bool IsLine => Placement.Part is Line;

	/// <summary>Whether it is a line that follows the chords: its degree 1 each bar's root.</summary>
	public bool IsOverChords => Placement.Part is Line { IsOverChords: true };

	/// <summary>Makes a line follow the chords, or play the scale's degrees as written.</summary>
	public void SetOverChords(bool over) => _workbench.SetOverChords(Placement, over);

	/// <summary>The grid its notes are written on (quarters, eighths, sixteenths or triplets), and those its kind can be; null for a part that is code.</summary>
	public (string Grid, IReadOnlyList<string> Grids)? GridChoice => _workbench.GridOf(Placement);

	/// <summary>Writes it on another grid, its notes keeping their timing; why it can't be, or null.</summary>
	public string? SetGrid(string grid) => _workbench.SetGrid(Placement, grid);

	/// <summary>Deletes it from its section.</summary>
	public void Delete() => _workbench.DeletePart(Placement);

	/// <summary>How many rows its notes lie on: pitches or pieces.</summary>
	public int Rows { get; internal init; }

	/// <summary>It, for a view drawing the clip from all of it at once.</summary>
	public PlacementViewModel Self => this;

	/// <summary>Shows it in the pattern view, for a button.</summary>
	public ICommand SelectCommand => field ??= new ActionCommand(Select);

	/// <summary>Shows it in the pattern view.</summary>
	public void Select() => _workbench.SelectPlacement(this);

	/// <summary>Chooses it with the roll at <paramref name="bar"/> of its section (from 0): that bar of the part, or its fill where the bar is the fill's.</summary>
	public void SelectBar(int bar) => _workbench.SelectPlacement(this, bar);

	/// <summary>The bar of its section a click on its lane chose while it is the part chosen, or -1.</summary>
	public int ChosenBar
	{
		get;
		internal set => Set(ref field, value);
	} = -1;

	/// <summary>How long a bar of its section is, in ticks.</summary>
	public int TicksPerBar { get; internal init; } = Ticks.PerBeat * 4;

	/// <summary>How many bars its section is.</summary>
	public int Bars => TicksPerBar > 0 ? LengthTicks / TicksPerBar : 0;

	/// <summary>Its condition taken apart by cue, as its lane's chip and the plays-when editor show it; null where it can't be.</summary>
	internal ConditionShape? Shape { get; init; }

	/// <summary>
	/// Where the cue its chip draws a gauge for is now (its condition's first level cue), refreshed as cues move; 0 when it
	/// has none.
	/// </summary>
	public float LevelNow
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Whether its song is a file the workbench edits, so it can be changed from its lane.</summary>
	public bool CanEdit => _workbench.CanEditSong;

	/// <summary>Whether it is data in its song's file, so its notes, fill and copies are the workbench's to make.</summary>
	public bool IsData => _workbench.IsDataPart(Placement);

	/// <summary>Whether it ends its section's every pass with a fill.</summary>
	public bool HasFill => FillTick >= 0;

	/// <summary>Adds a part for its track just above it or just below it, chosen at once.</summary>
	public void AddBeside(NewPartKind kind, bool below) => _workbench.AddPartBeside(Placement, kind, below);

	/// <summary>Gives it a fill in its last bar: a copy of that bar, to change in the roll, which opens on it; why it can't, or null.</summary>
	public string? AddFill() => _workbench.AddFill(Placement);

	/// <summary>Takes its fill out of its last bar.</summary>
	public void RemoveFill() => _workbench.RemoveFill(Placement);

	/// <summary>Puts a copy of it just below it, chosen.</summary>
	public void Duplicate() => _workbench.DuplicatePart(Placement);

	/// <summary>Renames it (empty: its track's name again); why it can't be, or null.</summary>
	public string? Rename(string name) => _workbench.RenamePart(Placement, name);

	/// <summary>Converts the whole of it to C#, as the roll's button does.</summary>
	public void ConvertToCode()
	{
		Select();
		_workbench.EjectSelected(placement: true);
	}

	/// <summary>Whether it can be converted to C#: its song has a file and code to write it to.</summary>
	public bool CanConvert => _workbench.CanEject;

	/// <summary>The project's cues, which its condition can name.</summary>
	public IReadOnlyList<CueBase> Cues => _workbench.ConditionCues;

	/// <summary>Where a cue is now.</summary>
	public float CueNow(CueBase cue) => _workbench.CueNow(cue);

	/// <summary>Which pass of its section in a row is playing: 1 while its section isn't.</summary>
	public int SectionPass => _workbench.PassOf(Placement);

	/// <summary>The other parts in its section that wait for <paramref name="cue"/>: each one's name and the level it waits at.</summary>
	public IReadOnlyList<(string Name, float Level)> OthersOn(Cue cue) => _workbench.LevelsInSection(Placement, cue);
}
