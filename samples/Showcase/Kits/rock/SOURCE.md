# Big Rusty Drums, cut down

These files are a small kit cut from **Big Rusty Drums v1.100** by Karoryfer Samples.

- **Downloaded from:** https://github.com/sfzinstruments/karoryfer.big-rusty-drums (release v1.100, `Big_Rusty_Drums_1100.zip`), on 2026-11-15.
- **Licence:** CC0 1.0 Universal (public domain dedication); the library's own licence file is `LICENSE.txt` beside this one.
- **How they were made:** Lunelle's `tools/kit-cut.cs` read `map.json` (whose paths are relative to the library's root folder),
  took each take from the overhead microphones (Samples/<piece>/<articulation>/oh), converted it to mono 44.1 kHz, trimmed it to its onset, cut it where it falls under -60 dB
  (or at the piece's longest), faded its end, scaled each piece as one to -1 dBFS, and encoded it as Ogg Vorbis (quality 5).
  `kit.json` is the manifest it wrote, which `DrumKit.Load` reads.

To make them again: download the library, copy `map.json` into its root folder (kit-cut reads a relative path from the
map's own folder), then from the repository's top folder
`dotnet run --file <Lunelle's checkout>/tools/kit-cut.cs -- <library folder>/map.json presets/Kits/rock`.
