namespace BinaryVibrance.SirrowStudio;

/// <summary>One stage of the voice after the operators, in the patch designer's chain: "Filter LP 2.6 kHz". A click opens the voice.</summary>
public sealed class StageViewModel(string name, string value, bool isOff, System.Windows.Input.ICommand choose) : ViewModel
{
	/// <summary>The stage: "Filter", "Drive", "Sends".</summary>
	public string Name { get; } = name;

	/// <summary>What it does: "LP 2.6 kHz", "off".</summary>
	public string Value { get; } = value;

	/// <summary>Whether it does nothing, drawn dim.</summary>
	public bool IsOff { get; } = isOff;

	/// <summary>Opens the voice in the inspector.</summary>
	public System.Windows.Input.ICommand Choose { get; } = choose;
}
