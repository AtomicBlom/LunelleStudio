namespace Loom.Audio.Workbench;

/// <summary>What the workbench's middle is given to: one job at a time.</summary>
public enum WorkbenchMode
{
	/// <summary>The song: its sections, its tracks and their parts, the cues, the part chosen.</summary>
	Arrange,

	/// <summary>A patch, played from the keyboard, with its macros.</summary>
	Patch,

	/// <summary>A kit, played from its pads and the keyboard.</summary>
	Kit,
}
