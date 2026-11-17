using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>A song's file, and what the names in it stand for when the workbench builds the song from it.</summary>
/// <param name="Path">The <c>.song.json</c>, in full.</param>
/// <param name="Bindings">Its patches, kits, cues and macros, and its pieces that are code, by name.</param>
public sealed record SongSource(string Path, SongBindings Bindings);
