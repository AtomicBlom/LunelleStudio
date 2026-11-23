using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

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

	/// <summary>
	/// How the game names a macro in code ("Macros.Bright"), for the patch designer's code and its macros; null, or null for
	/// a macro, writes <c>new Macro(n)</c>.
	/// </summary>
	public Func<int, string?>? MacroName { get; init; }

	/// <summary>
	/// The file a song is built from (a <c>.song.json</c>) and what its names stand for, by the song's name; null for a
	/// song that is code alone. A song with a file is edited in the workbench (its parts in the roll) and built from the file
	/// as it is edited, with no compile.
	/// </summary>
	public Func<string, SongSource?> SongSource { get; init; } = _ => null;

	/// <summary>The file a patch is built from (a <c>.patch.json</c>), by the patch's name in the catalog; null for code.</summary>
	public Func<string, string?> PatchFile { get; init; } = _ => null;

	/// <summary>The class the generator writes the patches' properties into, in full, whose own half holds a patch that is code.</summary>
	public string? PatchesType { get; init; }
}
