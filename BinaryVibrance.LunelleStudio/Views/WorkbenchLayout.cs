using System.ComponentModel;

using System.Numerics;

using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// The workbench page's layout (<c>WorkbenchLayout.xaml</c>): its regions, each a view handed the page's view model in
/// its XAML. <see cref="WorkbenchView"/> hosts it. A patch or kit dragged out of the library is dropped here: on the
/// board in Arrange, it adds a track that plays it.
/// </summary>
internal sealed partial class WorkbenchLayout : BinaryVibrance.Vysara.Controls.ContentControl
{
	/// <summary>A page over <paramref name="viewModel"/>.</summary>
	public WorkbenchLayout(WorkbenchViewModel viewModel)
	{
		ArgumentNullException.ThrowIfNull(viewModel);
		ViewModel = viewModel;
		InitializeComponent();
		// The built-in controls in the workbench's colours, everywhere under the page, and the rail's as markers of the mode.
		// Set before the page joins a tree, so every control's template reads them as it is built.
		(Resources ??= new ResourceDictionary()).MergedDictionaries.Add(Theme.Controls());
		RailModes.Resources = Theme.Markers();
		WireDrops();
	}

	partial void OnComponentRebuilt() => WireDrops();

	// The rail's Settings: a flyout of the workbench's settings beside it, made afresh each time, so it shows them as they are
	private void ShowSettings()
	{
		var flyout = new Flyout
		{
			Placement = BinaryVibrance.Vysara.Controls.Primitives.FlyoutPlacementMode.RightEdgeAlignedBottom,
			Content = new SettingsPanel(ViewModel.Workbench),
		};
		flyout.ShowAt(SettingsButton);
	}

	private void WireDrops()
	{
		SettingsButton.Click += (_, _) => ShowSettings();
		PinnedLibrary.InstrumentDragged += (name, isKit, at, dropped) => OnInstrumentDragged(PinnedLibrary, name, isKit, at, dropped);
		FlyoutLibrary.InstrumentDragged += (name, isKit, at, dropped) => OnInstrumentDragged(FlyoutLibrary, name, isKit, at, dropped);
		PinnedLibrary.PatternDragged += (id, at, dropped) => OnPatternDragged(PinnedLibrary, id, at, dropped);
		FlyoutLibrary.PatternDragged += (id, at, dropped) => OnPatternDragged(FlyoutLibrary, id, at, dropped);
	}

	// Whether a point (in an element's space) is over the board, past the library it came from
	private bool OverBoard(CatalogBrowser from, Func<UIElement, Vector2> at) =>
		ViewModel.IsArrange && Inside(Board, at(Board)) && !Inside(from, at(from));

	private static bool Inside(FrameworkElement element, Vector2 point) =>
		point.X >= 0f && point.Y >= 0f && point.X < element.LayoutRect.Width && point.Y < element.LayoutRect.Height;

	private void OnInstrumentDragged(CatalogBrowser from, string name, bool isKit, Func<UIElement, Vector2> at, bool dropped)
	{
		var over = OverBoard(from, at);
		if (dropped)
		{
			DragGhost.Visibility = Visibility.Collapsed;
			if (over && ViewModel.Workbench.AddTrackPlaying(name, isKit) is { } why)
				ViewModel.Error = why;
			return;
		}
		var point = at(DragLayer);
		DragText.Text = over ? $"Add track: {name}" : ViewModel.IsArrange ? name : $"{name} (drop in Arrange to add a track)";
		DragText.Foreground = over ? Theme.AccentInk : Theme.Ink;
		DragGhost.Background = over ? Theme.Accent : Theme.Raised;
		DragGhost.BorderBrush = over ? Theme.AccentOver : Theme.LineStrong;
		Canvas.SetLeft(DragGhost, point.X + 14f);
		Canvas.SetTop(DragGhost, point.Y + 10f);
		DragGhost.Visibility = Visibility.Visible;
	}

	// The lane cell a pattern's drag is over, lit
	private LaneCell? _dropCell;

	// A pattern dragged over Arrange: onto a part it plays there, onto a lane's ground a new part plays it, with Shift a
	// copy of its content; a lane it wouldn't fit is tinted, and refuses it
	private void OnPatternDragged(CatalogBrowser from, string id, Func<UIElement, Vector2> at, bool dropped)
	{
		var hit = ViewModel.IsArrange && !Inside(from, at(from)) ? LaneAt(Board, at) : null;
		var misfit = hit is { } h ? ViewModel.Workbench.PatternMisfit(id, h.Hit.Track.Name) : null;
		if (_dropCell != hit?.Cell)
			_dropCell?.ShowDrop(false, null, true);
		_dropCell = hit?.Cell;
		var copy = ViewModel.ShiftHeld;
		if (dropped)
		{
			DragGhost.Visibility = Visibility.Collapsed;
			_dropCell?.ShowDrop(false, null, true);
			_dropCell = null;
			if (hit is { Hit: var target } && (misfit ?? ViewModel.Workbench.DropPattern(id, target.Track.Name, target.Section, target.Part, copy)) is { } why)
				ViewModel.Error = why;
			return;
		}
		_dropCell?.ShowDrop(true, hit?.Hit.Part, misfit is null);
		var point = at(DragLayer);
		DragText.Text = hit is not { Hit: var over } ? ViewModel.IsArrange ? $"{id} (drop on a part, or a lane)" : $"{id} (drop in Arrange, on a part or a lane)"
			: misfit ?? (over.Part is { } part
				? $"{(copy ? "Copy" : "Play")} {id} in {part.Title}"
				: $"New part of {over.Track.Name}: {(copy ? "a copy of " : "")}{id}");
		var fits = hit is not null && misfit is null;
		DragText.Foreground = fits ? Theme.AccentInk : Theme.Ink;
		DragGhost.Background = fits ? Theme.Accent : misfit is null ? Theme.Raised : Theme.ErrorSoft;
		DragGhost.BorderBrush = fits ? Theme.AccentOver : Theme.LineStrong;
		Canvas.SetLeft(DragGhost, point.X + 14f);
		Canvas.SetTop(DragGhost, point.Y + 10f);
		DragGhost.Visibility = Visibility.Visible;
	}

	// The lane cell under a point, and what in it a drop would land on
	private static (LaneCell Cell, (TrackViewModel Track, int Section, PlacementViewModel? Part) Hit)? LaneAt(UIElement under, Func<UIElement, Vector2> at)
	{
		if (under is LaneCell cell)
			return cell.DropAt(at) is { } hit ? (cell, hit) : null;
		if (under.Visibility != Visibility.Visible)
			return null;
		for (var i = 0; i < BinaryVibrance.Vysara.Media.VisualTreeHelper.GetChildrenCount(under); i++)
			if (LaneAt(BinaryVibrance.Vysara.Media.VisualTreeHelper.GetChild(under, i), at) is { } found)
				return found;
		return null;
	}

	/// <summary>What it shows.</summary>
	public WorkbenchViewModel ViewModel { get; }

	/// <summary>The transport bar, for an app to add controls of its own to (<see cref="TransportBar.Extras"/>).</summary>
	public TransportBar Transport => TransportView;
}
