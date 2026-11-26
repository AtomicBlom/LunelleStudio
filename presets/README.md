# Presets

The sounds `lunelle-studio` ships for any project of music files alone to take: the browser's studio always, and the
desktop's over a folder with no C# (`--data`). They are listed in the library under **Presets**. A click plays one on
the keys (and a kit's pieces on the pads) as it is here, and **add** copies it into the project and opens it.

| Folder | What it holds |
|---|---|
| `Patches/<kind>/` | 47 patches (`lunelle-patch/1`), a folder for each kind of instrument: keys, bass, leads, synths, orchestra, guitars, mallets, percussion |
| `Kits/general/`, `Kits/rock/` | Two sampled kits, in velocity layers (`kit.json` and its Ogg Vorbis takes) |
| `Kits/*.kit.json` | Four kits made of patches, whose pieces' patches are in `Kits/patches/`: `synth`, `breaks`, `breaks-deep` and `grind` |
| `lunelle-project.json` | The names of the macros the patches turn: `bright` (0) opens a filter, `drive` (1) drives harder, `sweep` (2) moves a filter across a section |

A patch is added as `Patches/<name>.patch.json`. A sampled kit brings its whole folder (its samples, `LICENSE.txt` and
`SOURCE.md`) as `Kits/<name>/`. A kit of patches brings its `.kit.json` and each piece's patch. Any of the macro names
above that the patches turn go into the project's `lunelle-project.json`, unless the project already uses that name or
number, so a song's track can make the macro follow a cue. A preset the project already has by name opens as the project
has it. A file the preset would bring that the project has already, with other contents, stops the add, and the workbench
says which file it is.

## Where they come from

Most of the patches and the kits of patches are RubberTrack's instruments (a game built on Lunelle), without
the game's own sound effects (its impacts, menu sounds and countdown). The kits of patches were C# there,
written out here as files by `PatchSpec.From` and `PatchFile.Write`. Each patch says what
it is in its `"about"`.

The `grind` kit and ten patches were written for the showcase's `double-shot`: `glide-sub`, `fm-reese` and `swell-sub`
(bass), `fm-keys` (keys), `triple-saw-lead`, `buzz-lead` and `soft-pluck` (leads), `fm-bell` (mallets), and `haze-pad` and
`bubble-riser` (synths). They are named for how they are made, not for an instrument they would only approximate.

The sampled kits were cut from CC0 libraries by Lunelle's `tools/kit-cut.cs`. Each folder's `SOURCE.md` says how, and
its `LICENSE.txt` is the library's own:

- `general`: Virtuosity Drums v0.925, Versilian Studios with Karoryfer Samples, CC0 1.0.
- `rock`: Big Rusty Drums v1.100, Karoryfer Samples, CC0 1.0.

They add about 3.3 MB to the app, the browser's download included.
