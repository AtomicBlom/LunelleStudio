# LunelleStudio

The music workbench for [Lunelle](https://github.com/AtomicBlom/Lunelle): `lunelle-studio` opens a music project (a
library whose songs and patches are files Lunelle's generator makes into C#), plays it, compiles it again in-process on
every save, and swaps the new song in at the next bar. A game adds its own mix and controls through an extension. It was
split out of the [Kaldra engine](https://github.com/AtomicBlom/Kaldra), once called Loom.

| Project | What it is |
|---|---|
| `BinaryVibrance.LunelleStudio` | The workbench's model, view models and Vysara views, with no window: the API an extension is written against. |
| `BinaryVibrance.LunelleStudio.Projects` | A music project opened from its `.csproj` and compiled in-process with Roslyn, recompiled on each save. |
| `BinaryVibrance.LunelleStudio.App` | `lunelle-studio`, the app: a window on the engine, the music through Sirrow's SDL 3 output. |
| `BinaryVibrance.LunelleStudio.Tests` | The tests. |
| `samples/HelloSynth` | The project `lunelle-studio new` starts from. |

```
dotnet run --project BinaryVibrance.LunelleStudio.App -- <music project.csproj | folder>
dotnet run --project BinaryVibrance.LunelleStudio.App -- new <folder> [--data]
dotnet run --project BinaryVibrance.LunelleStudio.App -- <folder> --data
```

A folder with no `.csproj`, or any folder with `--data`, is music files alone: its songs, patches, kits and a
`lunelle-project.json` (its cues and macros) read with no C#, nothing compiled and no extension (Lunelle's `MusicFolder`); `new --data` starts one. Whatever the project, the workbench reads and writes its
songs, patches and state through `Workbench.Files` (`IMusicFiles`): the disk, unless a host keeps them elsewhere.

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
