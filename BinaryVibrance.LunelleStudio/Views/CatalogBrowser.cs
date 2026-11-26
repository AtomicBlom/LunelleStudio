using System.ComponentModel;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;
using BinaryVibrance.Vysara.Input;
using BinaryVibrance.Vysara.RoutedEvents;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// The library: the catalog filtered by what is typed, as lists of songs, patches and kits. A click on a row opens it, and
/// leaves the keys with the workbench (<see cref="WorkbenchFocus"/>); a list reached with Tab takes the arrows, and Enter
/// opens the row. The lists show what is open. While its filter has the keys, the workbench's own keys wait. The + on
/// Songs and Patches makes a new file of that kind beside the project's others, which it lists once the project has built
/// again. A patch or kit dragged out of it (<see cref="InstrumentDragged"/>) is for the page to drop on the board as a new
/// track. Under them the presets (<see cref="Workbench.Presets"/>), offered to a project of files alone: a click on one
/// plays it on the keys (<see cref="Workbench.TryPreset"/>), and its button adds it (<see cref="Workbench.AddPreset"/>).
/// </summary>
internal sealed partial class CatalogBrowser : ContentControl, INotifyPropertyChanged
{
	public CatalogBrowser()
	{
		InitializeComponent();
		Wire();
		WireDragMoves();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// A patch or kit dragged out of the library: its name, whether a kit, where the pointer is (a point in an element's
	/// space), and whether it was let go there. Raised as it moves and once as it is let go; a drag lost to the window is
	/// let go nowhere (the point is NaN).
	/// </summary>
	internal event Action<string, bool, Func<UIElement, System.Numerics.Vector2>, bool>? InstrumentDragged;

	/// <summary>
	/// A pattern dragged out of the library, as <see cref="InstrumentDragged"/>: its id, where the pointer is, and whether
	/// it was let go there. For the page to drop on a lane or a part (<see cref="Workbench.DropPattern"/>).
	/// </summary>
	internal event Action<string, Func<UIElement, System.Numerics.Vector2>, bool>? PatternDragged;

	// How far the pointer moves with a row held before it is a drag rather than a click
	private const float DragDistance = 6f;

	// A row pressed: its id, whether a kit, whether a pattern, where, and on which pointer; and whether it has become a drag
	private (string Name, bool IsKit, bool IsPattern, System.Numerics.Vector2 At, uint Pointer)? _held;
	private bool _dragging;

	// The patch, kit or pattern a row shows, from an element in it
	private static LibraryRowViewModel? RowOf(object? source)
	{
		for (var element = source as UIElement; element is not null; element = BinaryVibrance.Vysara.Media.VisualTreeHelper.GetParent(element))
			if (element is FrameworkElement { Tag: LibraryRowViewModel row })
				return row;
		return null;
	}

	private void WireDrag()
	{
		foreach (var (list, isKit, isPattern) in new[] { (PatchList, false, false), (KitList, true, false), (PatternList, false, true) })
			list.AddHandler(PreviewPointerPressedEvent, (_, e) =>
			{
				if (e is PointerRoutedEventArgs pointer && (pointer.Pointer.Buttons & PointerButtons.Left) != 0 && RowOf(e.OriginalSource) is { } row)
					_held = (row.Id, isKit, isPattern, pointer.GetCurrentPoint(null).Position, pointer.PointerId);
			}, handledEventsToo: true);
	}

	// The drag itself, on the library as a whole: it holds the pointer from the first move past the distance
	private void WireDragMoves()
	{
		AddHandler(PointerMovedEvent, (_, e) =>
		{
			if (_held is not { } held || e is not PointerRoutedEventArgs pointer || pointer.PointerId != held.Pointer)
				return;
			if ((pointer.Pointer.Buttons & PointerButtons.Left) == 0)
			{
				EndDrag(null);
				return;
			}
			if (!_dragging && System.Numerics.Vector2.Distance(pointer.GetCurrentPoint(null).Position, held.At) < DragDistance)
				return;
			if (!_dragging)
				_dragging = CapturePointer(held.Pointer);
			Func<UIElement, System.Numerics.Vector2> at = element => pointer.GetCurrentPoint(element).Position;
			if (held.IsPattern)
				PatternDragged?.Invoke(held.Name, at, false);
			else
				InstrumentDragged?.Invoke(held.Name, held.IsKit, at, false);
			e.Handled = true;
		}, handledEventsToo: true);
		AddHandler(PointerReleasedEvent, (_, e) =>
		{
			if (_dragging && e is PointerRoutedEventArgs pointer)
				e.Handled = true;
			EndDrag(e as PointerRoutedEventArgs);
		}, handledEventsToo: true);
		AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(null), handledEventsToo: true);
	}

	// The row let go: dropped where the pointer is, or nowhere
	private void EndDrag(PointerRoutedEventArgs? at)
	{
		var (held, dragging) = (_held, _dragging);
		_held = null;
		_dragging = false;
		if (held is not { } h || !dragging)
			return;
		ReleasePointerCapture(h.Pointer);
		Func<UIElement, System.Numerics.Vector2> where = at is null ? _ => new System.Numerics.Vector2(float.NaN) : element => at.GetCurrentPoint(element).Position;
		if (h.IsPattern)
			PatternDragged?.Invoke(h.Name, where, true);
		else
			InstrumentDragged?.Invoke(h.Name, h.IsKit, where, true);
	}

	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field is not null)
			{
				field.PropertyChanged -= OnModelChanged;
				field.CueMenuRequested -= ShowCueMenu;
			}
			field = value;
			if (value is not null)
			{
				value.PropertyChanged += OnModelChanged;
				value.CueMenuRequested += ShowCueMenu;
			}
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
			ShowChosen();
		}
	}

	partial void OnComponentRebuilt() => Wire();

	private void Wire()
	{
		Filter.TextChanged += (_, _) =>
		{
			if (ViewModel is { } model)
				model.LibraryQuery = Filter.Text;
		};
		Filter.GotFocus += (_, _) =>
		{
			if (ViewModel is { } model)
				model.IsTyping = true;
		};
		Filter.LostFocus += (_, _) =>
		{
			if (ViewModel is { } model)
				model.IsTyping = false;
		};
		foreach (var list in new[] { SongList, PatchList, KitList })
			list.ItemClick += (_, e) =>
			{
				if (e.ClickedItem is ChoiceViewModel choice)
					choice.IsSelected = true;
			};
		PresetList.ItemClick += (_, e) =>
		{
			if (e.ClickedItem is PresetViewModel preset)
				preset.TryCommand.Execute(null);
		};
		WireDrag();
		NewSong.Click += (_, _) => AskForName(NewSong, "New song", "open-road", copy: false);
		NewPatch.Click += (_, _) => AskForName(NewPatch, "New patch", "warm-pad", copy: true);
		NewCue.Click += (_, _) => AskForCue();
	}

	// A flyout making a cue: its id (a namespace before a '/'), a level or a choice among options, and what it is for
	private void AskForCue()
	{
		if (ViewModel?.Workbench is not { } workbench)
			return;
		var id = new TextBox { Width = 240f };
		ToolTipService.SetToolTip(id, "Lowercase letters, digits and hyphens, a namespace before a '/': intensity, lava/heat");
		var choice = new ToggleSwitch { OnContent = "A choice", OffContent = "A level, 0 to 1" };
		var options = new TextBox { Width = 240f, Visibility = Visibility.Collapsed };
		ToolTipService.SetToolTip(options, "The options, by commas: Calm, Bright, Dark");
		choice.Toggled += (_, _) => options.Visibility = choice.IsOn ? Visibility.Visible : Visibility.Collapsed;
		var about = new TextBox { Width = 240f };
		ToolTipService.SetToolTip(about, "What it is for, shown as its tooltip and in the game's code");
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Width = 240f };
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = "New cue, imported into this song", FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		column.Children.Add(id);
		column.Children.Add(choice);
		column.Children.Add(options);
		column.Children.Add(new TextBlock { Text = "About", FontSize = 11f, Foreground = Theme.Faint, Margin = new Thickness(0f, 6f, 0f, 2f) });
		column.Children.Add(about);
		var make = new Button { Content = new TextBlock { Text = "make", FontSize = 12f }, Margin = new Thickness(0f, 8f, 0f, 0f) };
		column.Children.Add(make);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		make.Click += (_, _) =>
		{
			var named = choice.IsOn ? options.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : null;
			var why = workbench.NewCue(id.Text, named, about.Text);
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		};
		flyout.ShowAt(NewCue);
	}

	// A cue's menu: renamed in every song, or deleted when no song reads it
	private void ShowCueMenu(CueEntryViewModel cue)
	{
		var name = new TextBox { Width = 220f, Text = cue.Id };
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Width = 220f };
		var rename = new Button { Content = new TextBlock { Text = "rename", FontSize = 12f }, Margin = new Thickness(0f, 6f, 6f, 0f) };
		var delete = new Button { Content = new TextBlock { Text = "delete", FontSize = 12f }, Margin = new Thickness(0f, 6f, 0f, 0f) };
		var buttons = new StackPanel { Orientation = Orientation.Horizontal };
		buttons.Children.Add(rename);
		buttons.Children.Add(delete);
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = cue.Id, FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		if (cue.About.Length > 0)
			column.Children.Add(new TextBlock { Text = cue.About, FontSize = 11f, Foreground = Theme.Faint, TextWrapping = TextWrapping.Wrap, Width = 220f });
		column.Children.Add(name);
		column.Children.Add(buttons);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		void Done(string? why)
		{
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		}
		rename.Click += (_, _) => Done(cue.Rename(name.Text));
		delete.Click += (_, _) => Done(cue.Delete());
		flyout.ShowAt(CueList);
	}

	private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(WorkbenchViewModel.SelectedSong) or nameof(WorkbenchViewModel.SelectedPatch) or nameof(WorkbenchViewModel.SelectedKit)
			or nameof(WorkbenchViewModel.Songs) or nameof(WorkbenchViewModel.Patches) or nameof(WorkbenchViewModel.Kits)
			or nameof(WorkbenchViewModel.TryingPreset))
			ShowChosen();
	}

	// The lists lit at what is open
	private void ShowChosen()
	{
		if (ViewModel is not { } model)
			return;
		SongList.SelectedIndex = model.SelectedSong;
		PatchList.SelectedIndex = model.SelectedPatch;
		KitList.SelectedIndex = model.SelectedKit;
		PresetList.SelectedIndex = model.TryingPreset;
	}

	// A flyout asking for the new file's name (and, for a patch, whether to start from the one open), Enter making it
	private void AskForName(Button anchor, string title, string example, bool copy)
	{
		if (ViewModel?.Workbench is not { } workbench)
			return;
		var name = new TextBox { Width = 220f };
		ToolTipService.SetToolTip(name, $"Lowercase letters, digits and hyphens, for example {example}");
		var from = new ToggleSwitch { OnContent = "Copy of open patch", OffContent = "Blank", IsOn = copy && ViewModel.SelectedPatch >= 0 };
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Width = 220f };
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = title, FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		column.Children.Add(name);
		if (copy)
			column.Children.Add(from);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		name.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is not KeyRoutedEventArgs { Key: VirtualKey.Enter })
				return;
			var open = ViewModel?.SelectedPatch is >= 0 and var at && at < ViewModel.Patches.Count ? ViewModel.Patches[at].Name : null;
			var why = copy ? workbench.NewPatch(name.Text, from.IsOn ? open : null) : workbench.NewSong(name.Text);
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		});
		flyout.ShowAt(anchor);
	}
}
