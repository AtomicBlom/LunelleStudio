namespace BinaryVibrance.SirrowStudio;

/// <summary>What the workbench's middle is given to: one job at a time.</summary>
public enum WorkbenchMode
{
	/// <summary>The song: its sections, its tracks and their parts, the cues, the part chosen.</summary>
	Arrange,

	/// <summary>A patch, played from the keyboard, with its macros.</summary>
	Patch,

	/// <summary>A kit, played from its pads and the keyboard.</summary>
	Kit,

	/// <summary>The project's music as files: each song's and patch's data and the C# that is theirs, copied whole or a piece at a time.</summary>
	Files,
}
