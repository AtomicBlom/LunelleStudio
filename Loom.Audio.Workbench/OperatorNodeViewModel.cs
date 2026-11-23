namespace Loom.Audio.Workbench;

/// <summary>An operator as a node of the patch designer's graph: where it sits, what it says, whether it is chosen.</summary>
public sealed class OperatorNodeViewModel : ViewModel
{
	internal OperatorNodeViewModel(int index) => Index = index;

	/// <summary>Its place in the patch: what it renders after, what the code numbers it.</summary>
	public int Index { get; }

	/// <summary>Its variable in the code: "modulator", "sine1".</summary>
	public string Name
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>What it is and plays: "SINE ×14", "MODES 3", "NOISE pink".</summary>
	public string Summary
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>What it routes from or plays at: "level 0.35 · vel 0.3", "driven by 2".</summary>
	public string Detail
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>Its kind, for its colour.</summary>
	public BinaryVibrance.Sirrow.Synth.Chip.OperatorKind Kind { get; internal set; }

	/// <summary>Its column: one right of the deepest operator it reads.</summary>
	public int Column { get; internal set; }

	/// <summary>Its row in its column.</summary>
	public int Row { get; internal set; }

	/// <summary>Whether it is heard (in the patch's Out).</summary>
	public bool IsHeard { get; internal set; }

	/// <summary>Whether it is the one the inspector shows.</summary>
	public bool IsSelected
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Whether the keyboard hears it alone.</summary>
	public bool IsAlone
	{
		get;
		internal set => Set(ref field, value);
	}
}
