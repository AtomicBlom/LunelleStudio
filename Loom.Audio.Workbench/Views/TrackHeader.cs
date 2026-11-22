using System.ComponentModel;
using SUIsei;
using SUIsei.Controls;
using SUIsei.Controls.Primitives;
using SUIsei.Input;
using SUIsei.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// A track at the head of its lane: a stripe in its hue, its name, what it plays, mute and solo, its gain and its meter.
/// A click shows it in the inspector, edged in the accent while it does; right-click (or Shift+F10) for its menu: rename
/// it, give it another instrument, open that instrument, take it out. Near its top or bottom edge a line with a + shows,
/// which adds a track just above or below it.
/// </summary>
internal sealed class TrackHeader : ContentControl
{
	public const float HeaderWidth = 176f;

	private readonly SUIsei.Media.TranslateTransform _stay = new();
	private ArrangeScroll? _scroll;
	private Border? _frame;
	private readonly Border _meter = new() { HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(2f), Background = Theme.Meter, Height = 4f };

	public TrackHeader()
	{
		IsTabStop = false;
		Width = HeaderWidth;
		// A left press its buttons and slider don't take for themselves chooses it
		AddHandler(PointerPressedEvent, (_, e) =>
		{
			if (e is PointerRoutedEventArgs pointer && pointer.Pointer.Buttons.HasFlag(PointerButtons.Left) && Track is { } track)
				track.Inspect();
		});
	}

	public TrackViewModel? Track
	{
		get;
		set
		{
			if (field is not null)
				field.PropertyChanged -= OnChanged;
			field = value;
			if (value is not null)
				value.PropertyChanged += OnChanged;
			Build();
		}
	}

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(TrackViewModel.Peak) && Track is { } track)
			_meter.Width = Display.Meter(track.Peak);
		else if (e.PropertyName == nameof(TrackViewModel.IsInspected))
			Light();
	}

	// Edged in the accent while the inspector shows it
	private void Light()
	{
		if (_frame is null || Track is not { } track)
			return;
		_frame.BorderBrush = track.IsInspected ? Theme.Accent : Theme.Line;
		_frame.BorderThickness = track.IsInspected ? new Thickness(1f) : new Thickness(0f, 0f, 1f, 1f);
		_frame.Background = track.IsInspected ? Theme.Raised : Theme.Panel;
	}

	private void Build()
	{
		if (_scroll is not null)
			_scroll.PropertyChanged -= OnScrolled;
		if (Track is not { } track)
		{
			Content = null;
			return;
		}
		_scroll = track.Scroll;
		_scroll.PropertyChanged += OnScrolled;
		RenderTransform = _stay;
		_stay.X = _scroll.X;
		var grid = new Grid();
		grid.ColumnDefinitions.Add(GridLength.Pixels(4f));
		grid.ColumnDefinitions.Add(GridLength.Star);
		grid.Children.Add(new Border { Background = Theme.TrackHue(track.Index), CornerRadius = new CornerRadius(2f), Margin = new Thickness(0f, 8f, 0f, 8f) });

		var column = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10f, 7f, 8f, 6f) };
		var top = new Grid();
		top.ColumnDefinitions.Add(GridLength.Star);
		top.ColumnDefinitions.Add(GridLength.Auto);
		top.ColumnDefinitions.Add(GridLength.Auto);
		top.Children.Add(new TextBlock { Text = track.Name, FontSize = 13f, Foreground = Theme.Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
		var mute = new ToggleButton { Content = Letter("M"), IsChecked = track.Muted, Width = 26f, Margin = new Thickness(0f, 0f, 3f, 0f), Padding = new Thickness(0f, 1f, 0f, 2f) };
		mute.Click += (_, _) => track.Muted = mute.IsChecked == true;
		ToolTipService.SetToolTip(mute, "Mute");
		Grid.SetColumn(mute, 1);
		var solo = new ToggleButton { Content = Letter("S"), IsChecked = track.Solo, Width = 26f, Padding = new Thickness(0f, 1f, 0f, 2f) };
		solo.Click += (_, _) => track.Solo = solo.IsChecked == true;
		ToolTipService.SetToolTip(solo, "Solo: hear only soloed tracks");
		Grid.SetColumn(solo, 2);
		top.Children.Add(mute);
		top.Children.Add(solo);
		column.Children.Add(top);

		var plays = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 2f, 0f, 2f) };
		plays.Children.Add(track.IsKit ? Glyphs.Kit(11f, Theme.Dim) : Glyphs.Patch(11f, Theme.Dim));
		plays.Children.Add(new TextBlock { Text = track.InstrumentName, FontSize = 11f, Foreground = Theme.Dim, Margin = new Thickness(5f, 0f, 0f, 0f), TextTrimming = TextTrimming.CharacterEllipsis });
		column.Children.Add(plays);

		var gain = new Slider { Minimum = -24, Maximum = 6, StepFrequency = 0.5, Value = track.GainDb, Height = 20f };
		gain.ValueChanged += (_, _) => track.GainDb = (float)gain.Value;
		ToolTipService.SetToolTip(gain, "Track gain in dB, on top of the song's level");
		column.Children.Add(gain);
		column.Children.Add(new Border { Height = 4f, CornerRadius = new CornerRadius(2f), Background = Theme.Sunk, Child = _meter });
		_meter.Width = Display.Meter(track.Peak);
		Grid.SetColumn(column, 1);
		grid.Children.Add(column);

		_frame = new Border
		{
			Background = Theme.Panel,
			BorderBrush = Theme.Line,
			BorderThickness = new Thickness(0f, 0f, 1f, 1f),
			Height = track.Height,
			Child = grid,
		};
		var stack = new Grid();
		stack.Children.Add(_frame);
		if (track.CanEdit)
			stack.Children.Add(Between(track));
		Content = stack;
		Light();
		ContextFlyout = Menu(track);
		ToolTipService.SetToolTip(this, $"{track.Name}. Click to edit; right-click for more options.");
	}

	// The lanes scrolled across: it moves back by as much, so it stays at the left
	private void OnScrolled(object? sender, PropertyChangedEventArgs e)
	{
		if (_scroll is not null)
			_stay.X = _scroll.X;
	}

	// How near the pointer must come to the header's top or bottom edge for the line to show there
	private const float Reach = 8f;

	// Along its top or bottom edge, while the pointer is near it: a line with a + adding a track just above or below it,
	// playing the instrument chosen from a list
	private InsertLine Between(TrackViewModel track)
	{
		var line = new InsertLine("Add a track here, playing a patch or kit");
		var below = false;
		line.Follow(this, at =>
		{
			var height = track.Height;
			if (at.Y <= Reach)
				below = false;
			else if (at.Y >= height - Reach)
				below = true;
			else
				return null;
			return (below ? height : 0f, 0f, HeaderWidth);
		});
		line.Chosen += anchor =>
		{
			line.IsHeld = true;
			var instruments = track.Instruments;
			var list = new ListView
			{
				ItemsSource = instruments.Select(i => (object?)$"{(i.IsKit ? "kit" : "patch")} · {i.Name}").ToList(),
				IsItemClickEnabled = true,
			};
			var flyout = new Flyout { Content = list, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
			var under = below;
			list.ItemClick += (_, e) =>
			{
				var at = list.ItemsSource is List<object?> items ? items.IndexOf(e.ClickedItem) : -1;
				flyout.Hide();
				if (at >= 0 && track.AddTrackBeside(instruments[at].Name, instruments[at].IsKit, under) is { } why)
					Console.WriteLine($"[workbench] {why}");
			};
			flyout.Closed += (_, _) => line.Release();
			flyout.ShowAt(anchor);
		};
		return line;
	}

	private MenuFlyout Menu(TrackViewModel track)
	{
		var menu = new MenuFlyout();
		var rename = new MenuFlyoutItem { Text = "Rename…" };
		rename.Click += (_, _) => Ask(track);
		var instrument = new MenuFlyoutItem { Text = "Change instrument…" };
		instrument.Click += (_, _) => Choose(track);
		var open = new MenuFlyoutItem { Text = track.IsKit ? "Open kit" : "Edit patch" };
		open.Click += (_, _) => track.OpenInstrument();
		var delete = new MenuFlyoutItem { Text = "Delete" };
		delete.Click += (_, _) => track.Delete();
		menu.Items.Add(rename);
		menu.Items.Add(instrument);
		menu.Items.Add(open);
		menu.Items.Add(delete);
		return menu;
	}

	// A small flyout asking for its new name, Enter taking it
	private void Ask(TrackViewModel track)
	{
		var box = new TextBox { Text = track.Name, Width = 200f };
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, Visibility = Visibility.Collapsed };
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = "Rename track", FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		column.Children.Add(box);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		box.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is not KeyRoutedEventArgs { Key: VirtualKey.Enter })
				return;
			var why = track.Rename(box.Text);
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		});
		flyout.ShowAt(this);
	}

	// A flyout of the catalog's patches and kits, the one it plays chosen
	private void Choose(TrackViewModel track)
	{
		var instruments = track.Instruments;
		var list = new ListView
		{
			ItemsSource = instruments.Select(i => (object?)$"{(i.IsKit ? "kit" : "patch")} · {i.Name}").ToList(),
			IsItemClickEnabled = true,
			SelectedIndex = instruments.FindIndex(i => i.Name == track.InstrumentName && i.IsKit == track.IsKit),
		};
		var flyout = new Flyout { Content = list, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		list.ItemClick += (_, e) =>
		{
			var at = list.ItemsSource is List<object?> items ? items.IndexOf(e.ClickedItem) : -1;
			if (at >= 0)
				track.SetInstrument(instruments[at].Name, instruments[at].IsKit);
			flyout.Hide();
		};
		flyout.ShowAt(this);
	}

	private static TextBlock Letter(string text) => new() { Text = text, FontSize = 11f, HorizontalAlignment = HorizontalAlignment.Center };
}
