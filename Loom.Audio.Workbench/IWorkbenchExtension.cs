using BinaryVibrance.Sirrow;

namespace Loom.Audio.Workbench;

/// <summary>
/// A game's own tools in the workbench: what it plays the music into (the game's mix, so a song sounds here as it will in
/// the game), controls at the transport's end, and whatever runs beside the music (a recorded car under it, an engine on
/// a bench). It lives in an assembly of its own, named by <see cref="WorkbenchExtensionAttribute"/>, loaded once and kept
/// while the music it accompanies is compiled again on every save.
/// </summary>
public interface IWorkbenchExtension : IDisposable
{
	/// <summary>The bus the music plays into, made on <paramref name="engine"/>; null for the workbench's own.</summary>
	AudioBus? Output(AudioEngine engine) => null;

	/// <summary>
	/// The music to play until the project's first compile is done: the extension's own build of it, so the window plays
	/// at once; null to wait.
	/// </summary>
	WorkbenchCatalog? Catalog => null;

	/// <summary>Called once the workbench is made: its controls added, the command line read.</summary>
	void Attach(WorkbenchExtensionContext context);

	/// <summary>Once a frame, after the workbench's own update.</summary>
	void Update(double seconds)
	{
	}

	/// <summary>
	/// A new build of the music project has been taken up: <paramref name="music"/> is its assembly, for an extension that
	/// takes something of the music's own from the build rather than from the copy it was built against.
	/// </summary>
	void MusicBuilt(System.Reflection.Assembly music)
	{
	}
}
