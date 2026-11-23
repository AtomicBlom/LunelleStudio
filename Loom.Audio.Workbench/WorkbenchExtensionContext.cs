using Vysara.Controls;

namespace Loom.Audio.Workbench;

/// <summary>What an extension is attached with: the workbench, where its controls go, and the command line it was given.</summary>
/// <param name="Workbench">The workbench: its player, cues, song and view model.</param>
/// <param name="Engine">The audio engine the music plays on.</param>
/// <param name="TransportExtras">A panel at the transport's end for the extension's own controls.</param>
/// <param name="Arguments">The command line's arguments the workbench didn't take itself (<c>--recording race.zip</c>).</param>
public sealed record WorkbenchExtensionContext(Workbench Workbench, AudioEngine Engine, StackPanel TransportExtras, IReadOnlyList<string> Arguments)
{
	/// <summary>The value after <paramref name="option"/> on the command line, or null.</summary>
	public string? Option(string option)
	{
		for (var i = 0; i + 1 < Arguments.Count; i++)
			if (Arguments[i] == option)
				return Arguments[i + 1];
		return null;
	}
}
