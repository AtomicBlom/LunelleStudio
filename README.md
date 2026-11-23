# SirrowStudio

The music workbench for [Sirrow](https://github.com/AtomicBlom/Sirrow): `sirrow-studio` opens a music project (a
library whose songs and patches are files Sirrow's generator makes into C#), plays it, compiles it again in-process on
every save, and swaps the new song in at the next bar. A game adds its own mix and controls through an extension. It was
split out of the [Kaldra engine](https://github.com/AtomicBlom/Kaldra), once called Loom.

| Project | What it is |
|---|---|
| `BinaryVibrance.SirrowStudio` | The workbench's model, view models and Vysara views, with no window: the API an extension is written against. |
| `BinaryVibrance.SirrowStudio.Projects` | A music project opened from its `.csproj` and compiled in-process with Roslyn, recompiled on each save. |
| `BinaryVibrance.SirrowStudio.App` | `sirrow-studio`, the app: a window on the engine, the music through Sirrow's SDL 3 output. |
| `BinaryVibrance.SirrowStudio.Tests` | The tests. |
| `samples/HelloSynth` | The project `sirrow-studio new` starts from. |

```
dotnet run --project BinaryVibrance.SirrowStudio.App -- <music project.csproj | folder>
dotnet run --project BinaryVibrance.SirrowStudio.App -- new <folder>
```

Sirrow's `docs/synth.md`, sections 10 and 11, is the guide.

## Building

SirrowStudio builds on the engine (Kaldra), Sirrow and Vysara, each from a checkout
beside this one or from its packages (`build/Sources.props`):

| Property | Default | Is |
|---|---|---|
| `KaldraSource` | `..\Kaldra\` | the engine's checkout |
| `SirrowSource` | `..\Sirrow\` | the audio's checkout |
| `VysaraSource` | `..\Vysara\` | the UI library's checkout |

`none` for any of them takes its packages, at `KaldraPackageVersion` or `SirrowPackageVersion`. A machine's own choices go
in an untracked `dev.local.props` at the top level. With the checkouts beside it:

```
dotnet build BinaryVibrance.SirrowStudio.slnx
dotnet run --project BinaryVibrance.SirrowStudio.Tests
```

## Licence

MIT (`LICENSE`).
