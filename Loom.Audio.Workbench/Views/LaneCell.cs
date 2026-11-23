using System.ComponentModel;
using Vysara;
using Vysara.Controls;
using Vysara.Controls.Primitives;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// Where a track's lane crosses a section: each part it plays there as a clip (a dot lit while it plays, its title, which
/// chooses it, its condition on a chip that opens the condition to change, held on, off or left to its condition), and
/// under the title its passes across the section. A part is added where the pointer shows a gap for one: a line with a
/// + above, between or under the parts, or a + in a cell with none. The column is tinted while its section plays.
/// </summary>
internal sealed class LaneCell : ContentControl
{
	private readonly Border _ground = new();

	public LaneCell() => IsTabStop = false;

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
		var column = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(Inset, Inset, Inset, 0f) };
		foreach (var part in cell.Placements)
			column.Children.Add(PartClip(part, cell.Width - 2f * Inset));
		_ground.Background = Display.Column(cell.IsCurrent);
		_ground.BorderBrush = Theme.Line;
		_ground.BorderThickness = new Thickness(1f, 0f, 0f, 1f);
		var grid = new Grid();
		grid.Children.Add(_ground);
		grid.Children.Add(column);
		// Parts are added only to a song with a file to write them to
		if (track.CanEdit)
			grid.Children.Add(cell.Placements.Count == 0 ? EmptyAdd(track, cell.Section) : Between(track, cell.Section, cell.Placements.Count, cell.Width));
		Content = grid;
	}

	// The parts' clips sit this far in from the cell's edges
	private const float Inset = 6f;

	// How near the pointer must come to a gap between parts for the line to show there
	private const float Reach = 10f;

	// In an empty cell: a + that shows while the pointer is over the cell, adding the track's first part here
	private UIElement EmptyAdd(TrackViewModel track, int section)
	{
		var add = new Button
		{
			Content = Glyphs.Plus(12f, Theme.Dim),
			Padding = new Thickness(8f, 4f, 8f, 4f),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(Inset, 20f, 0f, 0f),
			Opacity = 0f,
		};
		ToolTipService.SetToolTip(add, Tip(track));
		var open = false;
		add.Click += (_, _) =>
		{
			open = true;
			Ask(track, add, kind => track.AddPart(section, kind, 0), () =>
			{
				open = false;
				add.Opacity = 0f;
			});
		};
		PointerEntered += (_, _) => add.Opacity = 0.7f;
		PointerExited += (_, _) =>
		{
			if (!open)
				add.Opacity = 0f;
		};
		return add;
	}

	// Among parts: a line with a + where the pointer comes near a gap (above the first, between two, under the last),
	// adding a part for the track just there
	private UIElement Between(TrackViewModel track, int section, int parts, float width)
	{
		var line = new InsertLine(Tip(track));
		var slot = 0;
		line.Follow(this, at =>
		{
			var nearest = Math.Clamp((int)MathF.Round((at.Y - Inset) / ArrangeLayout.ClipHeight), 0, parts);
			var y = Inset + nearest * ArrangeLayout.ClipHeight;
			if (MathF.Abs(at.Y - y) > Reach)
				return null;
			slot = nearest;
			return (y, Inset, width - 2f * Inset);
		});
		line.Chosen += anchor =>
		{
			line.IsHeld = true;
			var at = slot;
			Ask(track, anchor, kind => track.AddPart(section, kind, at), line.Release);
		};
		return line;
	}

	private static string Tip(TrackViewModel track) => track.IsKit
		? $"Add a beat for {track.Name} here"
		: $"Add a part for {track.Name} here: a melody, a bass line, chords or a strum";

	// What kind of part to add, asked at the anchor: a kit's track has only beats, so it adds one at once
	private static void Ask(TrackViewModel track, FrameworkElement anchor, Action<NewPartKind> add, Action closed)
	{
		if (track.IsKit)
		{
			add(NewPartKind.Melody);
			closed();
			return;
		}
		var kinds = new MenuFlyout();
		foreach (var (text, kind) in new[] { ("Melody", NewPartKind.Melody), ("Bass line (follows chords)", NewPartKind.Bass), ("Chords (block chords, as on keys)", NewPartKind.Chords), ("Strum (chords, as on a guitar)", NewPartKind.Strum) })
		{
			var item = new MenuFlyoutItem { Text = text };
			item.Click += (_, _) => add(kind);
			kinds.Items.Add(item);
		}
		kinds.Closed += (_, _) => closed();
		kinds.ShowAt(anchor);
	}

	private static StackPanel PartClip(PlacementViewModel part, float width)
	{
		var title = new Grid { Height = 22f, Margin = new Thickness(0f, 0f, 0f, 3f) };
		title.ColumnDefinitions.Add(GridLength.Pixels(12f));
		title.ColumnDefinitions.Add(GridLength.Auto);
		title.ColumnDefinitions.Add(GridLength.Star);
		title.ColumnDefinitions.Add(GridLength.Auto);

		var dot = new Vysara.Shapes.Ellipse { Width = 7f, Height = 7f, VerticalAlignment = VerticalAlignment.Center, Fill = Display.Lit(part.IsActive) };
		ToolTipService.SetToolTip(dot, "Playing");
		title.Children.Add(dot);

		var name = new Button { Content = new TextBlock { Text = part.Title, FontSize = 12f, TextTrimming = TextTrimming.CharacterEllipsis }, Padding = new Thickness(6f, 1f, 6f, 2f), MaxWidth = width * 0.45f };
		name.Background = part.IsSelected ? Theme.AccentSoft : null;
		name.Click += (_, _) => part.Select();
		ToolTipService.SetToolTip(name, $"Edit {part.Title}");
		Grid.SetColumn(name, 1);
		title.Children.Add(name);

		var chipText = new TextBlock { Text = part.WhenShown, FontSize = 10.5f, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = part.When is null ? Theme.Faint : Display.Met(part.ConditionMet) };
		var chip = new Button { Content = chipText, Padding = new Thickness(6f, 1f, 6f, 2f), Margin = new Thickness(4f, 0f, 4f, 0f), HorizontalAlignment = HorizontalAlignment.Left, Background = Theme.Sunk };
		ToolTipService.SetToolTip(chip, part.When is null ? "Always plays. Click to add a condition." : $"Plays when {part.When}. Click to edit.");
		chip.Click += (_, _) =>
		{
			var workbench = part.Workbench;
			var flyout = new Flyout
			{
				Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
				Content = new ConditionEditor(workbench.ConditionCues, part.When, part.SetWhen),
			};
			flyout.ShowAt(chip);
		};
		Grid.SetColumn(chip, 2);
		title.Children.Add(chip);

		var states = new StackPanel { Orientation = Orientation.Horizontal };
		foreach (var (state, text, tip) in new[]
		{
			(Loom.Audio.Synth.Music.PlacementState.Auto, "Auto", "Play when the condition is met"),
			(Loom.Audio.Synth.Music.PlacementState.On, "On", "Always play, ignoring the condition"),
			(Loom.Audio.Synth.Music.PlacementState.Off, "Off", "Never play, ignoring the condition"),
		})
		{
			var toggle = new ToggleButton { Content = new TextBlock { Text = text, FontSize = 10f }, IsChecked = part.State == state, Padding = new Thickness(5f, 1f, 5f, 2f), Margin = new Thickness(1f, 0f, 0f, 0f) };
			toggle.Click += (_, _) =>
			{
				part.State = state;
				for (var i = 0; i < states.Children.Count; i++)
					if (states.Children[i] is ToggleButton other)
						other.IsChecked = ReferenceEquals(other, toggle);
			};
			ToolTipService.SetToolTip(toggle, tip);
			states.Children.Add(toggle);
		}
		Grid.SetColumn(states, 3);
		title.Children.Add(states);

		part.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(PlacementViewModel.IsActive))
				dot.Fill = Display.Lit(part.IsActive);
			else if (e.PropertyName == nameof(PlacementViewModel.ConditionMet) && part.When is not null)
				chipText.Foreground = Display.Met(part.ConditionMet);
			else if (e.PropertyName == nameof(PlacementViewModel.IsSelected))
				name.Background = part.IsSelected ? Theme.AccentSoft : null;
		};

		var clip = new StackPanel { Orientation = Orientation.Vertical, Height = ArrangeLayout.ClipHeight };
		clip.Children.Add(title);
		clip.Children.Add(new LanePasses { DrawWidth = width, Part = part });
		return clip;
	}
}
