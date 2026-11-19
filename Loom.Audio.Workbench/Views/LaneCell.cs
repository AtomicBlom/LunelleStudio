using System.ComponentModel;
using SUIsei;
using SUIsei.Controls;
using SUIsei.Controls.Primitives;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// Where a track's lane crosses a section: each part it plays there as a clip (a dot lit while it plays, its title, which
/// chooses it, its condition on a chip that opens the condition to change, held on, off or left to its condition), and
/// under the title its passes across the section; or, where it plays nothing, a button that adds a part. The column is
/// tinted while its section plays.
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
		var column = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(6f, 6f, 6f, 0f) };
		if (cell.Placements.Count == 0)
		{
			var add = new Button { Content = Glyphs.Plus(12f, Theme.Dim), Padding = new Thickness(8f, 4f, 8f, 4f), Opacity = 0.6f, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0f, 14f, 0f, 0f) };
			ToolTipService.SetToolTip(add, $"Add a part for {track.Name} here: a bar to write in the roll");
			add.Click += (_, _) => track.AddPart(cell.Section);
			column.Children.Add(add);
		}
		foreach (var part in cell.Placements)
			column.Children.Add(PartClip(part, cell.Width - 12f));
		_ground.Background = Display.Column(cell.IsCurrent);
		_ground.BorderBrush = Theme.Line;
		_ground.BorderThickness = new Thickness(1f, 0f, 0f, 1f);
		var grid = new Grid();
		grid.Children.Add(_ground);
		grid.Children.Add(column);
		Content = grid;
	}

	private static StackPanel PartClip(PlacementViewModel part, float width)
	{
		var title = new Grid { Height = 22f, Margin = new Thickness(0f, 0f, 0f, 3f) };
		title.ColumnDefinitions.Add(GridLength.Pixels(12f));
		title.ColumnDefinitions.Add(GridLength.Auto);
		title.ColumnDefinitions.Add(GridLength.Star);
		title.ColumnDefinitions.Add(GridLength.Auto);

		var dot = new SUIsei.Shapes.Ellipse { Width = 7f, Height = 7f, VerticalAlignment = VerticalAlignment.Center, Fill = Display.Lit(part.IsActive) };
		ToolTipService.SetToolTip(dot, "Lit while it plays");
		title.Children.Add(dot);

		var name = new Button { Content = new TextBlock { Text = part.Title, FontSize = 12f, TextTrimming = TextTrimming.CharacterEllipsis }, Padding = new Thickness(6f, 1f, 6f, 2f), MaxWidth = width * 0.45f };
		name.Background = part.IsSelected ? Theme.AccentSoft : null;
		name.Click += (_, _) => part.Select();
		ToolTipService.SetToolTip(name, $"{part.Title}: open it in the roll and the inspector");
		Grid.SetColumn(name, 1);
		title.Children.Add(name);

		var chipText = new TextBlock { Text = part.WhenShown, FontSize = 10.5f, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = part.When is null ? Theme.Faint : Display.Met(part.ConditionMet) };
		var chip = new Button { Content = chipText, Padding = new Thickness(6f, 1f, 6f, 2f), Margin = new Thickness(4f, 0f, 4f, 0f), HorizontalAlignment = HorizontalAlignment.Left, Background = Theme.Sunk };
		ToolTipService.SetToolTip(chip, part.When is null ? "It always plays: click to give it a condition" : $"When {part.When}: click to change it");
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
			(Loom.Audio.Synth.Music.PlacementState.Auto, "auto", "Plays by its condition"),
			(Loom.Audio.Synth.Music.PlacementState.On, "on", "Held on, whatever its condition"),
			(Loom.Audio.Synth.Music.PlacementState.Off, "off", "Held off, whatever its condition"),
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
