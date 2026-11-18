using Loom.Audio.Workbench.Views;

using SUIsei.Controls;

namespace Loom.Audio.Workbench;

/// <summary>
/// The workbench page, to put in a SUIsei root's content: the transport, sections, cues, tracks and their parts, the patch
/// bench, the chosen part's pattern and the chip's load, over a <see cref="WorkbenchViewModel"/>. Its parts are SUIsei
/// XAML, and reload under <c>dotnet watch</c>.
/// <code>
/// var workbench = new Workbench(Audio, musicBus, catalog);
/// ui.CreateRoot(UILayer.Default).Root.Content = new WorkbenchView(workbench.ViewModel);
/// </code>
/// </summary>
public sealed class WorkbenchView : ContentControl
{
	private readonly WorkbenchLayout _layout;

	/// <summary>A page over <paramref name="viewModel"/>.</summary>
	public WorkbenchView(WorkbenchViewModel viewModel)
	{
		ArgumentNullException.ThrowIfNull(viewModel);
		ViewModel = viewModel;
		IsTabStop = false;
		_layout = new WorkbenchLayout(viewModel);
		Content = _layout;
	}

	/// <summary>What it shows.</summary>
	public WorkbenchViewModel ViewModel { get; }

	/// <summary>A panel at the end of the transport bar, for an app's own controls (a game's toggle for its engine under the music).</summary>
	public StackPanel TransportExtras => _layout.Transport.Extras;
}
