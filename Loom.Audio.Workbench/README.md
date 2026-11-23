# Loom.Audio.Workbench

The music workbench for `BinaryVibrance.Sirrow.Synth`: its model, view models and Vysara views, with no window of its own.
`loom-workbench` (`Loom.Audio.Workbench.App`) is the app that opens a music project in it, compiles the project on every
save and plays what it built. This library is what that app is made of, and what a project's extension is written
against.

## A workbench extension

A game adds its own things to the workbench, such as its mix as the music's output, controls of its own, or sound under
the music, with an extension in an assembly beside its music project:

```csharp
[assembly: WorkbenchExtension(typeof(StudioExtension))]

public sealed class StudioExtension : IWorkbenchExtension
{
    public WorkbenchCatalog? Catalog => ...;                       // the music, before the project has compiled
    public AudioBus? Output(AudioEngine engine) => ...;            // the bus the music plays into: the game's own mix
    public void Attach(WorkbenchExtensionContext context) { ... }  // the workbench, its engine, a panel for controls
    public void Update(double seconds) { ... }                     // once a frame
    public void MusicBuilt(Assembly music) { ... }                 // after each build of the project
    public void Dispose() { ... }
}
```

The extension is built against this package at the version of the `loom-workbench` that loads it, since it runs in
that process.

## Hosting the workbench in an app of your own

An app can show the workbench over a catalog it makes itself and run under `dotnet watch` for hot reload, in place of
`loom-workbench`'s compiles. Loom's `docs/synth.md`, sections 10 and 11, is the guide to both.
