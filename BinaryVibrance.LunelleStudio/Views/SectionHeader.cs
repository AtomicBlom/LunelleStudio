using System.ComponentModel;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;
using BinaryVibrance.Vysara.Input;
using BinaryVibrance.Vysara.RoutedEvents;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// A section at the head of its column: its name (chosen, it is shown in the inspector), a button that plays it from the
/// next bar, then its bars as a ruler, each cell the bar's number and its chords, the bars that repeat the progression to
/// fill the section dimmed, and a click on a cell picking that bar's chords; the bar playing lit and a playhead across the
/// whole section; and under the ruler its exits in a line, where it goes next marked.
/// </summary>
internal sealed class SectionHeader : ContentControl
{
	public const float RulerHeight = 34f;
	public const float Tall = 86f;

	private readonly List<Border> _cells = [];
	private readonly BinaryVibrance.Vysara.Media.TranslateTransform _stay = new();
	private ArrangeScroll? _scroll;
	private readonly Border _playhead = new() { Width = 2f, Height = RulerHeight, Background = Theme.Playhead, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
	private readonly TextBlock _exits = new() { FontSize = 11f, Foreground = Theme.Dim, TextTrimming = TextTrimming.CharacterEllipsis };
	private readonly TextBlock _next = new() { FontSize = 11f, Foreground = Theme.Accent, Margin = new Thickness(8f, 0f, 0f, 0f) };
	private readonly ToggleButton _name = new() { Padding = new Thickness(6f, 1f, 8f, 2f) };
	private readonly TextBlock _queued = new() { Text = "Next bar", FontSize = 11f, Foreground = Theme.Accent, Margin = new Thickness(8f, 3f, 0f, 0f), Visibility = Visibility.Collapsed };
	// The bar cell a left press went down on, which a release over the same cell picks: a right-click or a drag that
	// ends over a bar picks nothing
	private Border? _pressed;

	public SectionHeader()
	{
		IsTabStop = false;
		Height = Tall;
	}

	/// <summary>The section it heads.</summary>
	public SectionViewModel? Section
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
		switch (e.PropertyName)
		{
			case nameof(SectionViewModel.PlayingBar) or nameof(SectionViewModel.Progress):
				ShowPlaying();
				break;
			case nameof(SectionViewModel.NextText):
				_next.Text = Section?.NextText ?? "";
				break;
			case nameof(SectionViewModel.IsInspected):
				_name.IsChecked = Section?.IsInspected == true;
				break;
			case nameof(SectionViewModel.IsQueued):
				_queued.Visibility = Section?.IsQueued == true ? Visibility.Visible : Visibility.Collapsed;
				break;
		}
	}

	private void Build()
	{
		_cells.Clear();
		if (_scroll is not null)
			_scroll.PropertyChanged -= OnScrolled;
		if (Section is not { } section)
		{
			Content = null;
			return;
		}
		_scroll = section.Scroll;
		_scroll.PropertyChanged += OnScrolled;
		RenderTransform = _stay;
		_stay.Y = _scroll.Y;
		Width = section.Width;

		var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4f, 4f, 0f, 4f) };
		_name.Content = new TextBlock { Text = section.Name, FontSize = 13f };
		_name.IsChecked = section.IsInspected;
		_name.Click += (_, _) =>
		{
			_name.IsChecked = true;
			section.Inspect();
		};
		ToolTipService.SetToolTip(_name, $"Edit {section.Name}: bars, chords and exits");
		var play = new Button { Content = Glyphs.Play(10f), Padding = new Thickness(6f, 3f, 6f, 3f), Margin = new Thickness(4f, 0f, 0f, 0f) };
		play.Click += (_, _) => section.Jump();
		ToolTipService.SetToolTip(play, section.Index < 9 ? $"Jump to {section.Name} at the next bar ({section.Index + 1})" : $"Jump to {section.Name} at the next bar");
		top.Children.Add(_name);
		top.Children.Add(play);
		top.Children.Add(new TextBlock { Text = section.Detail, FontSize = 11f, Foreground = Theme.Faint, Margin = new Thickness(8f, 4f, 0f, 0f) });
		top.Children.Add(_queued);

		var ruler = new Grid { Height = RulerHeight };
		var cells = new StackPanel { Orientation = Orientation.Horizontal };
		var progression = section.Progression.Count;
		for (var bar = 0; bar < section.Bars; bar++)
		{
			var at = progression == 0 ? 0 : bar % progression;
			var repeat = progression > 0 && bar >= progression;
			var written = progression == 0 ? "I" : string.Join(" ", section.Progression[at].Written);
			var words = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4f, 2f, 2f, 2f) };
			words.Children.Add(new TextBlock { Text = (bar + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 9f, Foreground = Theme.Faint });
			words.Children.Add(new TextBlock { Text = written, FontSize = 12f, Foreground = Theme.Ink, TextTrimming = TextTrimming.CharacterEllipsis });
			var cell = new Border
			{
				Width = section.BarWidth,
				BorderBrush = Theme.Line,
				BorderThickness = new Thickness(1f, 0f, 0f, 0f),
				Background = Theme.Clear,
				Opacity = repeat ? 0.45f : 1f,
				Child = words,
			};
			ToolTipService.SetToolTip(cell, repeat
				? $"Bar {bar + 1}: {written} (repeats bar {at + 1}). Click to edit."
				: $"Bar {bar + 1}: {written}. Click to edit.");
			var bar1 = at;
			cell.AddHandler(PointerPressedEvent, (_, e) =>
			{
				if (e is PointerRoutedEventArgs { Pointer.Buttons: PointerButtons.Left })
					_pressed = cell;
			});
			cell.AddHandler(PointerReleasedEvent, (_, _) =>
			{
				if (ReferenceEquals(_pressed, cell))
					Pick(cell, bar1);
				_pressed = null;
			});
			cell.PointerExited += (_, _) =>
			{
				if (ReferenceEquals(_pressed, cell))
					_pressed = null;
			};
			_cells.Add(cell);
			cells.Children.Add(cell);
		}
		ruler.Children.Add(cells);
		ruler.Children.Add(_playhead);

		var exits = new Grid { Margin = new Thickness(4f, 4f, 4f, 0f) };
		exits.ColumnDefinitions.Add(GridLength.Star);
		exits.ColumnDefinitions.Add(GridLength.Auto);
		_exits.Text = section.ExitsText;
		ToolTipService.SetToolTip(_exits, section.Exits.Count == 0
			? "No exits: the section repeats. Select the section to add exits."
			: string.Join("\n", section.Exits.Select((e, i) => e.When is { } when ? $"{i + 1}. Go to {e.Then} when {when}" : $"{i + 1}. Otherwise go to {e.Then}")));
		_next.Text = section.NextText;
		Grid.SetColumn(_next, 1);
		exits.Children.Add(_exits);
		exits.Children.Add(_next);

		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(top);
		column.Children.Add(ruler);
		column.Children.Add(exits);
		Content = new Border { Background = Theme.Panel, BorderBrush = Theme.LineStrong, BorderThickness = new Thickness(1f, 0f, 0f, 1f), Child = column };
		ShowPlaying();
	}

	// The lanes scrolled down: it moves down by as much, so it stays at the top
	private void OnScrolled(object? sender, PropertyChangedEventArgs e)
	{
		if (_scroll is not null)
			_stay.Y = _scroll.Y;
	}

	private void Pick(Border cell, int bar)
	{
		if (Section is not { } section)
			return;
		var flyout = new Flyout
		{
			Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
			Content = new ChordPicker(section.Progression, bar, section.Scale, section.SetChords),
		};
		flyout.ShowAt(cell);
	}

	private void ShowPlaying()
	{
		if (Section is not { } section)
			return;
		for (var i = 0; i < _cells.Count; i++)
			_cells[i].Background = i == section.PlayingBar ? Theme.AccentSoft : Theme.Clear;
		_playhead.Visibility = section.PlayingBar >= 0 ? Visibility.Visible : Visibility.Collapsed;
		_playhead.Margin = new Thickness(section.Progress * section.Width, 0f, 0f, 0f);
	}
}
