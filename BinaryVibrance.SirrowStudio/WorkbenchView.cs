using BinaryVibrance.SirrowStudio.Views;

using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.SirrowStudio;

/// <summary>
/// The workbench page, to put in a Vysara root's content: the transport, sections, cues, tracks and their parts, the patch
/// bench, the chosen part's pattern and the chip's load, over a <see cref="WorkbenchViewModel"/>. Its parts are Vysara
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

	/// <summary>The layout the workbench wants at least, in layout units: its lanes, cues, roll and keyboard side by side.</summary>
	public const float MinLayoutWidth = 1100f;

	/// <summary>The layout the workbench wants at least, in layout units.</summary>
	public const float MinLayoutHeight = 700f;

	/// <summary>
	/// The layout scale for a window <paramref name="surface"/> pixels big on a display of <paramref name="displayScale"/>:
	/// the display's, but never so large that the layout is smaller than <see cref="MinLayoutWidth"/> by
	/// <see cref="MinLayoutHeight"/> (a 1280 by 800 window on a 200% display is laid out at 1, not 2), nor under a half.
	/// A host sets its root's <c>LayoutScale</c> to it each frame, so maximising the window brings the display's scale back.
	/// </summary>
	public static float FitScale(System.Numerics.Vector2 surface, float displayScale) =>
		Math.Clamp(MathF.Min(surface.X / MinLayoutWidth, surface.Y / MinLayoutHeight), 0.5f, MathF.Max(1f, displayScale));
}
