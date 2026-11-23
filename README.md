# SirrowStudio

The music workbench for [Sirrow](https://github.com/AtomicBlom/Sirrow): `loom-workbench` opens a music project (a
library whose songs and patches are files Sirrow's generator makes into C#), plays it, compiles it again in-process on
every save, and swaps the new song in at the next bar. A game adds its own mix and controls through an extension. It was
split out of the [Loom engine](https://github.com/AtomicBlom/Loom), and its projects keep their `Loom.Audio.Workbench`
names for now.

| Project | What it is |
|---|---|
| `Loom.Audio.Workbench` | The workbench's model, view models and Vysara views, with no window: the API an extension is written against. |
| `Loom.Audio.Workbench.Projects` | A music project opened from its `.csproj` and compiled in-process with Roslyn, recompiled on each save. |
| `Loom.Audio.Workbench.App` | `loom-workbench`, the app: a window on the engine, the music through Sirrow's SDL 3 output. |
| `Loom.Audio.Workbench.Tests` | The tests. |
| `samples/HelloSynth` | The project `loom-workbench new` starts from. |

```
dotnet run --project Loom.Audio.Workbench.App -- <music project.csproj | folder>
dotnet run --project Loom.Audio.Workbench.App -- new <folder>
```

Sirrow's `docs/synth.md`, sections 10 and 11, is the guide.

## Building

SirrowStudio builds on the engine (still Loom's repository, to become Kaldra), Sirrow and Vysara, each from a checkout
beside this one or from its packages (`build/Sources.props`):

| Property | Default | Is |
|---|---|---|
| `LoomSource` | `..\Kaldra\` | the engine's checkout |
| `SirrowSource` | `..\Sirrow\` | the audio's checkout |
| `VysaraSource` | `..\Vysara\` | the UI library's checkout |

`none` for any of them takes its packages, at `LoomPackageVersion` or `SirrowPackageVersion`. A machine's own choices go
in an untracked `dev.local.props` at the top level. With the checkouts beside it:

```
dotnet build Loom.Audio.Workbench.slnx
dotnet run --project Loom.Audio.Workbench.Tests
```

## Licence

MIT (`LICENSE`).
