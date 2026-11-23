# BinaryVibrance.LunelleStudio.Projects

A music project opened from its `.csproj` and compiled in-process with Roslyn: its compiler command line taken from a
design-time build, a compilation that each save updates, the result loaded into a collectible context that shares
Kaldra with the host, and its catalog found by `[assembly: MusicCatalog]`. It is how `lunelle-studio` reloads music in
place of `dotnet watch`, with no window, so a game's tests can open its music project the same way.

```csharp
using var project = await MusicProject.OpenAsync("MyGame.Audio/MyGame.Audio.csproj",
    new Dictionary<string, string> { ["Configuration"] = "Release" });
```

Lunelle's [`docs/synth.md`](https://github.com/AtomicBlom/Lunelle/blob/master/docs/synth.md), section 5, is the guide.
