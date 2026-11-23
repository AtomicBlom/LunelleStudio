namespace BinaryVibrance.LunelleStudio;

/// <summary>What the patch designer's inspector shows: the operator chosen, the voice after the operators, the macros, or the code.</summary>
public enum DesignerTab
{
	/// <summary>The operator chosen in the graph.</summary>
	Operator,

	/// <summary>The voice after the operators: filter, drive, gain, pan, sends, vibrato, tremolo, pitch drop, mono.</summary>
	Voice,

	/// <summary>The macros: what each moves, from what to what.</summary>
	Macros,

	/// <summary>The C# that builds the patch.</summary>
	Code,
}
