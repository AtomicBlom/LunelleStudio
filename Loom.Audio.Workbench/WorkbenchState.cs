namespace Loom.Audio.Workbench;

/// <summary>
/// What a workbench was doing, kept across restarts (a rude edit restarts the app under <c>dotnet watch</c>): the song,
/// whether it played and where, the cues, each track's mute, solo and gain, each part's forced state, the patch bench.
/// Everything is by name, so a song rebuilt since still finds what is still there.
/// </summary>
public sealed class WorkbenchState
{
	/// <summary>The song chosen.</summary>
	public string? Song { get; set; }

	/// <summary>Whether it was playing.</summary>
	public bool Playing { get; set; }

	/// <summary>Whether the section was held.</summary>
	public bool LoopSection { get; set; }

	/// <summary>The section playing.</summary>
	public string? Section { get; set; }

	/// <summary>A scale played in place of the song's own: its root's name, or none.</summary>
	public string? ScaleRoot { get; set; }

	/// <summary>That scale's name ("minor").</summary>
	public string? ScaleName { get; set; }

	/// <summary>Each cue's value, by name.</summary>
	public Dictionary<string, float> Cues { get; set; } = [];

	/// <summary>Each track's mute, solo and gain, by name.</summary>
	public Dictionary<string, TrackSetting> Tracks { get; set; } = [];

	/// <summary>Each forced part's state ("On", "Off"), by "section/part".</summary>
	public Dictionary<string, string> Forced { get; set; } = [];

	/// <summary>The bench's patch, by name.</summary>
	public string? Patch { get; set; }

	/// <summary>The bench's kit, by name.</summary>
	public string? Kit { get; set; }

	/// <summary>The keyboard's octave.</summary>
	public int Octave { get; set; } = 4;

	/// <summary>How tall Arrange's part panel is.</summary>
	public float PartPanelHeight { get; set; } = 300f;

	/// <summary>The keyboard's velocity.</summary>
	public float Velocity { get; set; } = 0.8f;

	/// <summary>What the middle was given to: "Arrange", "Patch" or "Kit".</summary>
	public string? Mode { get; set; }

	/// <summary>Whether the library was pinned open as a column.</summary>
	public bool LibraryPinned { get; set; }

	/// <summary>The part chosen, as "section/part", shown in the roll.</summary>
	public string? SelectedPart { get; set; }
}
