using Loom.Audio.Synth.Chip;
using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>
/// What a workbench offers: a game's songs, patches and kits, each a factory (hot reload changes what a factory
/// returns, so the workbench calls it again rather than keeping what it made), and the game's cues.
/// </summary>
public sealed record WorkbenchCatalog
{
	/// <summary>The songs, each built anew by its factory.</summary>
	public IReadOnlyList<(string Name, Func<Song> Build)> Songs { get; init; } = [];

	/// <summary>The patches, for the patch bench.</summary>
	public IReadOnlyList<(string Name, Func<Patch> Build)> Patches { get; init; } = [];

	/// <summary>The drum kits, for the patch bench's pads.</summary>
	public IReadOnlyList<(string Name, Func<DrumKit> Build)> Kits { get; init; } = [];

	/// <summary>The game's cues, shown whether or not the song playing uses them.</summary>
	public IReadOnlyList<CueBase> Cues { get; init; } = [];
}
