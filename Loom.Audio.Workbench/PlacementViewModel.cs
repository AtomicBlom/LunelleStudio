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

	/// <summary>When it plays, in words: "when intensity above 0.35", "level follows forest"; empty when always.</summary>
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
			if (Set(ref _state, value))
				_workbench.Force(this, value);
		}
	}

	/// <summary>Shows it in the pattern view.</summary>
	public void Select() => _workbench.SelectPlacement(this);
}
