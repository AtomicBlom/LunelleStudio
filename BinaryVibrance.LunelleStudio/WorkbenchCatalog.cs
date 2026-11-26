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

	/// <summary>
	/// A derived patch (a file with a <c>"base"</c>, overrides.md) by its id: the patch it resolves to, as a whole file's
	/// text, and the whole patch below it that its file changes; null for a patch whose file is whole. The designer edits
	/// the whole and saves only what differs from the base (Lunelle's OverrideDiff).
	/// </summary>
	public Func<string, (string Whole, BinaryVibrance.Lunelle.Format.JObject Below)?> DerivedPatch { get; init; } = _ => null;

	/// <summary>The class the generator writes the patches' properties into, in full, whose own half holds a patch that is code.</summary>
	public string? PatchesType { get; init; }

	/// <summary>
	/// The project's folder, in full, when it is music files alone (<see cref="FolderCatalog"/>), where a preset added from
	/// <see cref="Workbench.Presets"/> is copied; null for a project with code, which takes no presets.
	/// </summary>
	public string? Folder { get; init; }

	/// <summary>
	/// The project's folder, in full, whether its music is files alone or has code: where its cue files are (Cues/), and
	/// its songs, for edits across them (renaming a cue). Null for a catalog with no files.
	/// </summary>
	public string? Root { get; init; }
}
