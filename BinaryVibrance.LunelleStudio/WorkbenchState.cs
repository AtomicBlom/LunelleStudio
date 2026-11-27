namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// What a project's workbench comes back to when it opens again: the song, what the middle was given to, and the
/// bench's patch or kit. Nothing about playing is kept: a project opens stopped, at the start, with its cues, tracks and
/// parts as the song has them. Everything is by name, so a song rebuilt since still finds what is still there.
/// </summary>
public sealed class WorkbenchState
{
	/// <summary>The song chosen.</summary>
	public string? Song { get; set; }

	/// <summary>What the middle was given to: "Arrange", "Patch" or "Kit".</summary>
	public string? Mode { get; set; }

	/// <summary>The bench's patch, by name.</summary>
	public string? Patch { get; set; }

	/// <summary>The bench's kit, by name.</summary>
	public string? Kit { get; set; }
}
