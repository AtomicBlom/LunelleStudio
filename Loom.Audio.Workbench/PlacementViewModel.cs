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

	/// <summary>Shows it in the pattern view, for a button.</summary>
	public ICommand SelectCommand => field ??= new ActionCommand(Select);

	/// <summary>Shows it in the pattern view.</summary>
	public void Select() => _workbench.SelectPlacement(this);
}
