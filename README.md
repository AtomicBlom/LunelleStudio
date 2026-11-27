# LunelleStudio

The music workbench for [Lunelle](https://github.com/AtomicBlom/Lunelle): `lunelle-studio` opens a music project (a
library whose songs and patches are files Lunelle's generator makes into C#), plays it, compiles it again in-process on
every save, and swaps the new song in at the next bar. A game adds its own mix and controls through an extension. It was
split out of the [Kaldra engine](https://github.com/AtomicBlom/Kaldra), once called Loom.

| Project | What it is |
|---|---|
| `BinaryVibrance.LunelleStudio` | The workbench's model, view models and Vysara views, with no window: the API an extension is written against. |
| `BinaryVibrance.LunelleStudio.Projects` | A music project opened from its `.csproj` and compiled in-process with Roslyn, recompiled on each save. |
| `BinaryVibrance.LunelleStudio.App` | `lunelle-studio`, the app: a window on the engine, the music through Sirrow's SDL 3 output; or, with `-r browser-wasm`, the same in the browser ("In the browser"). |
| `BinaryVibrance.LunelleStudio.Tests` | The tests. |
| `samples/HelloSynth` | The project `lunelle-studio new` starts from. |
| `samples/Showcase` | Six songs of music files alone, arranged by their cues as they play, every sound Lunelle's own library's: the first project the studio opens, and a tour of what the files can say (its `README.md`). |

```
dotnet run --project BinaryVibrance.LunelleStudio.App -- <music project.csproj | folder>
dotnet run --project BinaryVibrance.LunelleStudio.App -- new <folder> [--data]
dotnet run --project BinaryVibrance.LunelleStudio.App -- <folder> --data
```

Music is Lunelle's format 2 (its `docs/spec/`): a `lunelle.meta.json` and its songs, patches, kits, patterns and cue
files, each named by its path. A folder with no `.csproj`, or any folder with `--data`, is music files alone, read with
no C#, nothing compiled and no extension (Lunelle's `MusicFolder`); `new --data` starts one. A project with code hands
the same files to Lunelle's generator, and the studio resolves them the same way.

**Editing.** Every edit goes to a document over the project's files (`ProjectDocument`, docs/spec/transactions.md):
Ctrl+Z and Ctrl+Y undo and redo across every file, **save** (Ctrl+S) writes them, and a crash loses nothing, the edits
recovered from `.lunelle-studio/journal.jsonl` when the project opens again. A project with code compiles from the
document, unsaved edits and all.

**The library** is a tree of the project's songs, patches, kits and patterns by folder, and its cues by namespace,
filtered by words and by kind. A pattern dragged onto a part in Arrange plays there, onto a lane it is a new part, and
with Shift a copy; a part's notes extract into one, and one detaches back. A cue is imported into the open song with a
tick; made from the library, the strip, or a condition naming it; and edited, renamed and deleted across every song.
**Lunelle's sounds** (`lunelle:…`, `PresetLibrary`) play on the keys with a click, and **add** makes a sound of the
project's based on one, which saves only what it changes: the designer marks each line it changes, with **revert**.
**exports** plays a song's exports live, renders them to WAV, and records new ones from the cues and track macros moved
as it plays; the automation lane under Arrange's tracks shows and edits an export's events. The UI's specs are `docs/spec/ui-*.md`.

## On the desktop

Started with nothing named, the studio opens the project it had open last, or `Documents\Lunelle Studio\Showcase`,
which it makes from the showcase the first time. A `.lunelle` file given to it (or opened from Explorer, once installed)
is unpacked into `Documents\Lunelle Studio` as a folder to edit, into the same folder each time while that folder still
holds just what the file does. **open**, **open folder** and **export** on the transport use the system's dialogs, and a
`.lunelle` file, a `.csproj` or a folder dropped on the window opens too, in a studio of its own. A project always opens
stopped, at the song and mode it was left at; how the workbench is laid out is kept with the studio's settings
(`%LOCALAPPDATA%\BinaryVibrance\LunelleStudio\settings.json`), for every project alike.

A project with code needs the .NET SDK (`dotnet`) to build it: without one, its music files open alone. It must be
built against the very Kaldra and Lunelle the studio runs, which it shares with the project; a project built against
another version is refused, naming both.

**Publishing.** `./build/publish.ps1 -Runtime win-x64` publishes the studio self-contained and ReadyToRun to
`artifacts/publish/win-x64`, and with Inno Setup installed, makes its installer (`installer/lunelle-studio.iss`, which
also opens `.lunelle` files in the studio) in `artifacts/installer`. Given a certificate, it signs both. The `Studio`
workflow does the same on every push, and makes a GitHub release from a `v*` tag. On Windows the studio prefers Vulkan
and falls back to DirectX 12 where there is no Vulkan driver; `--backend` chooses.

## In the browser

The app builds for the browser too, on WebGPU (`-r browser-wasm`; Kaldra's SDK picks the WebAssembly SDK by the runtime
identifier), into `bin/web` and `obj/web` apart from the desktop build:

```
dotnet publish BinaryVibrance.LunelleStudio.App -c Release -r browser-wasm -p:RunAOTCompilation=true -o <dir>
dotnet run --file <Kaldra's checkout>/tools/kaldra-serve.cs -- <dir> --no-isolation
```

- **Music files alone.** There is no Roslyn in the browser, so a project is always music files alone, with no extension.
  The browser keeps one project in its storage (IndexedDB, `Web/BrowserFiles.cs`). The first visit starts it with
  `samples/Showcase` (its sampled kits' takes copied from the presets', so the app holds them once). A browser that
  kept a project from an earlier visit keeps it; clearing the site's data starts again from the showcase. **export** on the transport saves the project as a `.lunelle` file (`MusicArchive`: a zip
  of its folder, without `.lunelle-studio/`), and **import** replaces it with one. A zip of a folder holding a project
  works as well.
- **One thread.** The runtime is single-threaded: the workbench never waits on a thread, and the music mixes on the
  frame's thread in `AudioEngine.Update`, through Sirrow.Web's AudioWorklet. So the page needs no cross-origin
  isolation, and any static host can serve it. GitHub Pages works too, given a `.nojekyll` file, or it drops
  `_framework/` and `_content/`.
- **AOT.** Interpreted, the studio draws about a frame a second and the music starves. With
  `RunAOTCompilation=true` (about three minutes, the `wasm-tools` workload) it runs at the display's rate, with the
  music rendered in real time.

Lunelle's [`docs/synth.md`](https://github.com/AtomicBlom/Lunelle/blob/master/docs/synth.md), sections 5 and 6, is the guide.

## Building

LunelleStudio builds on the engine (Kaldra), Sirrow, Lunelle and Vysara, each from a checkout
beside this one or from its packages (`build/Sources.props`):

| Property | Default | Is |
|---|---|---|
| `KaldraSource` | `..\Kaldra\` | the engine's checkout |
| `SirrowSource` | `..\Sirrow\` | the audio's checkout |
| `LunelleSource` | `..\Lunelle\` | the synth's checkout |
| `VysaraSource` | `..\Vysara\` | the UI library's checkout |

`none` for any of them takes its packages, at `KaldraPackageVersion`, `SirrowPackageVersion` or `LunellePackageVersion`. A machine's own choices go
in an untracked `dev.local.props` at the top level. With the checkouts beside it:

```
dotnet build BinaryVibrance.LunelleStudio.slnx
dotnet run --project BinaryVibrance.LunelleStudio.Tests
```

## Licence

MIT (`LICENSE`).
