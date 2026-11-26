using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>A song's file, what the names in it stand for when the workbench builds the song from it, and its class.</summary>
/// <param name="Path">The <c>.song.json</c>, in full.</param>
/// <param name="Bindings">Its patches, kits, cues and macros, and its pieces that are code, by name.</param>
/// <param name="Type">The class the generator makes of it, in full ("Game.Songs.OpenRoad"), whose own half holds what is code.</param>
public sealed record SongSource(string Path, SongBindings Bindings, string? Type = null)
{
	/// <summary>
	/// Builds the song from its file's text as edited, its bindings passed through the given function: in format 2 the
	/// text is resolved first, as its project resolves the file (MusicFolder.BuildSong). Null builds the text with
	/// <see cref="Bindings"/> as it is.
	/// </summary>
	public Func<string, Func<SongBindings, SongBindings>, Song>? Build { get; init; }
}
