using System.ComponentModel;
using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;
using BinaryVibrance.Vysara.Input;
using BinaryVibrance.Vysara.RoutedEvents;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// Where a track's lane crosses a section: each part it plays there as a clip, and the column tinted while its section
/// plays. A clip's row is a lamp lit while it plays, its name, its "plays when" chip (a gauge of the level it waits for,
/// the options it plays on, or held on or off), which opens the plays-when editor, and ⋯ for its menu (right-click opens
/// it too); under the row its passes across the section. A press anywhere on a clip chooses it; on its passes, it chooses
/// that bar of it, or its fill, for the roll; a press on the lane's empty ground chooses the track. A cell with no parts
/// has a slot to add the track's first part there. Nothing appears on hover; the bar under the pointer is outlined.
/// </summary>
internal sealed class LaneCell : ContentControl
{
	// The parts' clips sit this far in from the cell's edges
	internal const float Inset = 6f;
	private const float RowHeight = 22f;

	private static BinaryVibrance.Vysara.Media.SolidColorBrush Hover => Theme.Hover;

	private readonly Border _ground = new();

	public LaneCell()
	{
		IsTabStop = false;
		// The lane's spare ground is the track's: a press there chooses it
		_ground.AddHandler(PointerPressedEvent, (_, e) =>
		{
			if (e is PointerRoutedEventArgs p && p.Pointer.Buttons.HasFlag(PointerButtons.Left) && Track is { } track)
				track.Inspect();
		});
	}

	public TrackViewModel? Track { get; set { field = value; Build(); } }

	public LaneCellViewModel? Cell
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
		if (e.PropertyName == nameof(LaneCellViewModel.IsCurrent) && Cell is { } cell)
			_ground.Background = Display.Column(cell.IsCurrent);
	}

	private void Build()
	{
		if (Track is not { } track || Cell is not { } cell)
		{
			Content = null;
			return;
		}
		Width = cell.Width;
		Height = track.Height;
		var column = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(Inset, Inset, Inset, 0f), VerticalAlignment = VerticalAlignment.Top };
		foreach (var part in cell.Placements)
			column.Children.Add(PartClip(part, cell.Width - 2f * Inset));
		_ground.Background = Display.Column(cell.IsCurrent);
		_ground.BorderBrush = Theme.Line;
		_ground.BorderThickness = new Thickness(1f, 0f, 0f, 1f);
		ToolTipService.SetToolTip(_ground, $"{track.Name}: click to edit the track");
		var grid = new Grid();
		grid.Children.Add(_ground);
		grid.Children.Add(column);
		// Parts are added only to a song with a file to write them to
		if (track.CanEdit && cell.Placements.Count == 0)
			grid.Children.Add(AddSlot(track, cell.Section));
		Content = grid;
	}

	// In an empty cell: a slot that adds the track's first part in the section
	private static Button AddSlot(TrackViewModel track, int section)
	{
		var content = new StackPanel { Orientation = Orientation.Horizontal };
		content.Children.Add(Glyphs.Plus(10f, Theme.Faint));
		content.Children.Add(new TextBlock { Text = "Add part", FontSize = 11.5f, Foreground = Theme.Faint, Margin = new Thickness(6f, 0f, 0f, 0f) });
		var add = new Button
		{
			Content = content,
			Padding = new Thickness(10f, 4f, 12f, 5f),
			Margin = new Thickness(Inset, 18f, Inset, 0f),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Background = Theme.Clear,
			BorderBrush = Theme.Line,
			BorderThickness = new Thickness(1f),
		};
		ToolTipService.SetToolTip(add, Tip(track));
		add.Click += (_, _) => Ask(track, add, kind => track.AddPart(section, kind, 0));
		return add;
	}

	private static string Tip(TrackViewModel track) => track.IsKit
		? $"Add a beat for {track.Name} here"
		: $"Add a part for {track.Name} here: a melody, a bass line, chords or a strum";

	// What kind of part to add, asked at the anchor: a kit's track has only beats, so it adds one at once
	private static void Ask(TrackViewModel track, FrameworkElement anchor, Action<NewPartKind> add) => Ask(track.IsKit, anchor, add);

	private static void Ask(bool isKit, FrameworkElement anchor, Action<NewPartKind> add)
	{
		if (isKit)
		{
			add(NewPartKind.Melody);
			return;
		}
		var kinds = new MenuFlyout();
		foreach (var (text, kind) in new[] { ("Melody", NewPartKind.Melody), ("Bass line (follows chords)", NewPartKind.Bass), ("Chords (block chords, as on keys)", NewPartKind.Chords), ("Strum (chords, as on a guitar)", NewPartKind.Strum) })
		{
			var item = new MenuFlyoutItem { Text = text };
			item.Click += (_, _) => add(kind);
			kinds.Items.Add(item);
		}
		kinds.ShowAt(anchor);
	}

	// A part: its row (lamp, name, plays-when chip, ⋯) over its passes; pressed anywhere it is chosen, on a bar of its
	// passes at that bar
	private static Border PartClip(PlacementViewModel part, float width)
	{
		var row = new Grid { Height = RowHeight, Margin = new Thickness(0f, 0f, 0f, 3f) };
		row.ColumnDefinitions.Add(GridLength.Pixels(14f));
		row.ColumnDefinitions.Add(GridLength.Star);
		row.ColumnDefinitions.Add(GridLength.Auto);
		row.ColumnDefinitions.Add(GridLength.Auto);

		var lamp = new BinaryVibrance.Vysara.Shapes.Ellipse { Width = 7f, Height = 7f, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Fill = Lamp(part.IsActive) };
		ToolTipService.SetToolTip(lamp, "Lit while it plays");
		row.Children.Add(lamp);

		var name = new TextBlock { Text = part.Title, FontSize = 12f, Foreground = Theme.Ink, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3f, 0f, 4f, 1f) };
		Grid.SetColumn(name, 1);
		row.Children.Add(name);

		var chip = new Button { Padding = new Thickness(6f, 1f, 6f, 2f), Margin = new Thickness(0f, 0f, 2f, 0f), VerticalAlignment = VerticalAlignment.Center, MaxWidth = width * 0.42f };
		ShowChip(chip, part);
		chip.Click += (_, _) => OpenPlaysWhen(chip, part);
		Grid.SetColumn(chip, 2);
		row.Children.Add(chip);

		var more = new Button { Content = Glyphs.More(14f, Theme.Dim), Padding = new Thickness(5f, 4f, 5f, 4f), VerticalAlignment = VerticalAlignment.Center, Background = Theme.Clear };
		ToolTipService.SetToolTip(more, "More: add a part, a fill, duplicate, rename, delete");
		Grid.SetColumn(more, 3);
		row.Children.Add(more);

		// The passes, with the bar under the pointer outlined and the bar chosen edged in the accent
		var passes = new LanePasses { DrawWidth = width, Part = part };
		var marks = new Canvas { Width = width, Height = LanePasses.DrawnHeight, IsHitTestVisible = false };
		var hover = Mark(Theme.LineStrong, 1f);
		var chosen = Mark(Theme.Accent, 1.5f);
		marks.Children.Add(hover);
		marks.Children.Add(chosen);
		var strip = new Grid { Width = width, Height = LanePasses.DrawnHeight };
		strip.Children.Add(passes);
		strip.Children.Add(marks);
		// A bar's span on the passes, where the ruler has it (LanePasses.Across)
		var bars = Math.Max(1, part.Bars);
		void Place(Border mark, int bar)
		{
			mark.Visibility = bar < 0 ? Visibility.Collapsed : Visibility.Visible;
			if (bar < 0)
				return;
			var left = LanePasses.Across((float)bar / bars, width);
			mark.Width = MathF.Max(2f, LanePasses.Across((float)(bar + 1) / bars, width) - left);
			Canvas.SetLeft(mark, left);
		}
		Place(hover, -1);
		Place(chosen, part.IsSelected ? part.ChosenBar : -1);

		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(row);
		column.Children.Add(strip);
		// Its track's hue along its top, in a theme that edges parts, over it rather than above it, so nothing moves
		var face = new Grid();
		face.Children.Add(column);
		face.Children.Add(new Border
		{
			Height = 2f,
			VerticalAlignment = VerticalAlignment.Top,
			CornerRadius = new CornerRadius(2f, 2f, 0f, 0f),
			Background = Theme.PartEdge(part.TrackIndex),
			IsHitTestVisible = false,
		});
		var clip = new Border
		{
			Height = ArrangeLayout.ClipHeight,
			CornerRadius = new CornerRadius(5f),
			Background = part.IsSelected ? Theme.AccentSoft : Theme.Clear,
			Child = face,
		};
		var tip = $"{part.Title}: click to edit it, or a bar of it to open the roll there. Right-click for more.";
		ToolTipService.SetToolTip(clip, tip);
		var menu = Menu(part, more, name);
		more.Click += (_, _) => menu.ShowAt(more);
		clip.ContextFlyout = menu;

		// The bar of the section under a point on the passes, or -1 off them
		int BarAt(RoutedEventArgs e)
		{
			if (e is not PointerRoutedEventArgs p || part.Bars <= 0)
				return -1;
			var at = p.GetCurrentPoint(strip).Position;
			return at.Y < 0f || at.Y > LanePasses.DrawnHeight || at.X < 0f || at.X >= width ? -1 : Math.Clamp((int)(LanePasses.ShareAt(at.X, width) * part.Bars), 0, part.Bars - 1);
		}
		var hovering = false;
		long lastPress = 0;
		clip.AddHandler(PointerPressedEvent, (_, e) =>
		{
			if (e is not PointerRoutedEventArgs p || !p.Pointer.Buttons.HasFlag(PointerButtons.Left))
				return;
			var now = Environment.TickCount64;
			var twice = now - lastPress < 400;
			lastPress = now;
			var bar = BarAt(e);
			if (bar >= 0)
				part.SelectBar(bar);
			else if (twice && part.CanEdit)
				Rename(part, name);
			else
				part.Select();
			e.Handled = true;
		});
		clip.AddHandler(PointerMovedEvent, (_, e) =>
		{
			var bar = BarAt(e);
			Place(hover, bar);
			ToolTipService.SetToolTip(clip, bar < 0 ? tip : part.HasFill && bar == part.Bars - 1 ? "The fill: click to edit it in the roll" : $"Bar {bar + 1}: click to edit the part from here");
		}, handledEventsToo: true);
		clip.PointerEntered += (_, _) =>
		{
			hovering = true;
			if (!part.IsSelected)
				clip.Background = Hover;
		};
		clip.PointerExited += (_, _) =>
		{
			hovering = false;
			Place(hover, -1);
			clip.Background = part.IsSelected ? Theme.AccentSoft : Theme.Clear;
		};

		part.PropertyChanged += (_, e) =>
		{
			switch (e.PropertyName)
			{
				case nameof(PlacementViewModel.IsActive):
					lamp.Fill = Lamp(part.IsActive);
					break;
				case nameof(PlacementViewModel.ConditionMet) or nameof(PlacementViewModel.LevelNow) or nameof(PlacementViewModel.State):
					ShowChip(chip, part);
					break;
				case nameof(PlacementViewModel.IsSelected) or nameof(PlacementViewModel.ChosenBar):
					clip.Background = part.IsSelected ? Theme.AccentSoft : hovering ? Hover : Theme.Clear;
					Place(chosen, part.IsSelected ? part.ChosenBar : -1);
					break;
			}
		};
		return clip;
	}

	private static BinaryVibrance.Vysara.Media.SolidColorBrush Lamp(bool on) => on ? Theme.Lamp : Theme.Off;

	private static Border Mark(BinaryVibrance.Vysara.Media.Brush brush, float thickness) => new()
	{
		Height = LanePasses.DrawnHeight,
		CornerRadius = new CornerRadius(3f),
		BorderBrush = brush,
		BorderThickness = new Thickness(thickness),
		IsHitTestVisible = false,
	};

	// The chip: held on or off; "always" in dim ink; a gauge of the level it waits for and the level; the options it plays on;
	// the pass; "if…" for a condition only text can hold. A "+" says there is more to it than it shows.
	private static void ShowChip(Button chip, PlacementViewModel part)
	{
		var shape = part.Shape;
		var met = part.ConditionMet;
		var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
		var ink = met ? Theme.Ink : Theme.Dim;
		chip.Background = Theme.Sunk;
		string tip;
		switch (part.State)
		{
			case PlacementState.On:
				content.Children.Add(ChipText("ON", Theme.AccentInk));
				chip.Background = Theme.Accent;
				tip = "Held on while you listen (not saved). Click to change.";
				break;
			case PlacementState.Off:
				content.Children.Add(ChipText("OFF", Theme.Error));
				chip.Background = Theme.ErrorSoft;
				tip = "Held off while you listen (not saved). Click to change.";
				break;
			default:
				var more = shape is { Count: > 1 } ? "+" : "";
				switch (shape?.First)
				{
					case null when shape is not null:
						content.Children.Add(ChipText("always", Theme.Dim));
						break;
					case null:
						content.Children.Add(ChipText("if…", ink));
						break;
					case LevelClause level:
						content.Children.Add(Gauge(level, part.LevelNow, met));
						content.Children.Add(ChipText(level.Rule switch
						{
							LevelRule.Above => "≥" + ConditionShape.Short(level.From),
							LevelRule.Below => "≤" + ConditionShape.Short(level.To),
							_ => $"{ConditionShape.Short(level.From)}–{ConditionShape.Short(level.To)}",
						} + more, ink));
						break;
					case ChoiceClause choice:
						var options = ConditionShape.Options(choice);
						content.Children.Add(ChipText((choice.Except ? "not " : "") + options[0] + (options.Count > 1 ? $" +{options.Count - 1}" : "") + more, ink));
						break;
					case PassRule rule:
						content.Children.Add(ChipText(rule switch
						{
							PassRule.First => "1st pass",
							PassRule.FromSecond => "from 2nd",
							_ => $"pass {shape!.OnlyPass}",
						}, ink));
						break;
				}
				tip = (shape?.Sentence() ?? $"Plays when {part.When}.") + " Click to change.";
				break;
		}
		chip.Content = content;
		ToolTipService.SetToolTip(chip, tip);
	}

	private static TextBlock ChipText(string text, BinaryVibrance.Vysara.Media.Brush brush) => new()
	{
		Text = text,
		FontSize = 10.5f,
		Foreground = brush,
		VerticalAlignment = VerticalAlignment.Center,
		TextTrimming = TextTrimming.CharacterEllipsis,
	};

	// A level cue's range drawn small: where the part plays on a rail, and a tick where the cue is now
	private static Canvas Gauge(LevelClause level, float now, bool met)
	{
		const float Width = 28f;
		const float Height = 6f;
		var canvas = new Canvas { Width = Width, Height = 12f, Margin = new Thickness(0f, 0f, 5f, 0f), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
		var rail = new Border { Width = Width, Height = Height, CornerRadius = new CornerRadius(3f), Background = Theme.Raised };
		Canvas.SetTop(rail, 3f);
		var low = level.Rule is LevelRule.Above or LevelRule.Between ? level.From : 0f;
		var high = level.Rule is LevelRule.Below or LevelRule.Between ? level.To : 1f;
		var range = new Border { Width = MathF.Max(2f, (high - low) * Width), Height = Height, CornerRadius = new CornerRadius(3f), Background = met ? Theme.Meter : Theme.Faint };
		Canvas.SetLeft(range, low * Width);
		Canvas.SetTop(range, 3f);
		var tick = new Border { Width = 2f, Height = 12f, CornerRadius = new CornerRadius(1f), Background = Theme.Ink };
		Canvas.SetLeft(tick, Math.Clamp(now, 0f, 1f) * Width - 1f);
		canvas.Children.Add(rail);
		canvas.Children.Add(range);
		canvas.Children.Add(tick);
		return canvas;
	}

	// The plays-when editor in a flyout under the chip, kept up to date each frame while it is open
	private static void OpenPlaysWhen(Button chip, PlacementViewModel part)
	{
		var editor = new PlaysWhenEditor(part);
		var flyout = new Flyout { Content = editor, Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
		var model = part.Workbench.ViewModel;
		model.Frame += editor.Refresh;
		flyout.Closed += (_, _) => model.Frame -= editor.Refresh;
		flyout.ShowAt(chip);
	}

	// A part's menu, from ⋯ or a right-click: parts beside it, its fill, a copy, its name, C#, deleting it
	private static MenuFlyout Menu(PlacementViewModel part, FrameworkElement anchor, FrameworkElement name)
	{
		var menu = new MenuFlyout();
		void Add(string text, Action act, bool enabled = true)
		{
			var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled };
			item.Click += (_, _) => act();
			menu.Items.Add(item);
		}
		var edits = part.CanEdit;
		var isKit = part.Placement.Track.Kit is not null;
		Add(isKit ? "Add a beat above" : "Add part above…", () => Ask(isKit, anchor, kind => part.AddBeside(kind, below: false)), edits);
		Add(isKit ? "Add a beat below" : "Add part below…", () => Ask(isKit, anchor, kind => part.AddBeside(kind, below: true)), edits);
		if (part.HasFill)
		{
			Add("Edit fill", () => part.SelectBar(part.Bars - 1));
			Add("Remove fill", part.RemoveFill, edits && part.IsData);
		}
		else
			Add("Add fill (its last bar)", () => Say(part.AddFill()), edits && part.IsData);
		Add("Duplicate", part.Duplicate, edits);
		Add("Rename…", () => Rename(part, name), edits);
		if (part.CanConvert && part.IsData)
			Add("Convert to C#", part.ConvertToCode);
		Add("Delete", part.Delete, edits);
		return menu;
	}

	private static void Say(string? why)
	{
		if (why is not null)
			Console.WriteLine($"[workbench] {why}");
	}

	// A small flyout asking for the part's new name, Enter taking it; empty gives it its track's name again
	private static void Rename(PlacementViewModel part, FrameworkElement anchor)
	{
		var box = new TextBox { Text = part.Title, Width = 200f };
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, Visibility = Visibility.Collapsed };
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = "Rename part", FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		column.Children.Add(box);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
		box.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is not KeyRoutedEventArgs { Key: VirtualKey.Enter })
				return;
			var why = part.Rename(box.Text);
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		});
		flyout.ShowAt(anchor);
	}
}
