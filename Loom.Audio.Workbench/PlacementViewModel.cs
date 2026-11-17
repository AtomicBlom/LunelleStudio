using System.Windows.Input;
using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>A part a track plays in a section: its condition, whether it is on now, and whether it is forced on or off.</summary>
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

	/// <summary>One pass of its part, drawn small on its clip: notes by row, over <see cref="LoopTicks"/>.</summary>
	public IReadOnlyList<PatternNote> Notes { get; internal init; } = [];

	/// <summary>How long one pass of its part is, in ticks.</summary>
	public int LoopTicks { get; internal init; }

	/// <summary>How many rows its notes lie on: pitches or pieces.</summary>
	public int Rows { get; internal init; }

	/// <summary>It, for a view drawing the clip from all of it at once.</summary>
	public PlacementViewModel Self => this;

	/// <summary>Shows it in the pattern view, for a button.</summary>
	public ICommand SelectCommand => field ??= new ActionCommand(Select);

	/// <summary>Shows it in the pattern view.</summary>
	public void Select() => _workbench.SelectPlacement(this);
}
