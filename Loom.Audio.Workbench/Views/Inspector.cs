using System.ComponentModel;
using System.Globalization;
using SUIsei;
using SUIsei.Controls;
using SUIsei.Input;
using SUIsei.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// What is chosen in Arrange, to change: a section (its name, bars, chords, and its exits, each a condition and where it
/// goes, in the order they are tried, the last always), or a part (when it plays, the cue its level follows, whether it
/// waits for its phrase, its fill). Its groups fold. Its own edits build the song again, which makes the chosen thing's
/// view model again; it shows the new one without being made again, so a field being typed in keeps its focus.
/// </summary>
internal sealed class Inspector : ContentControl
{
	private readonly StackPanel _column = new() { Orientation = Orientation.Vertical, Margin = new Thickness(14f, 12f, 14f, 14f) };
	private object? _shownKey;
	private bool _ownEdit;

	public Inspector()
	{
		IsTabStop = false;
		Content = new ScrollViewer { Content = _column, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
	}

	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field is not null)
				field.PropertyChanged -= OnChanged;
			field = value;
			if (value is not null)
				value.PropertyChanged += OnChanged;
			Show();
		}
	}

	private Workbench? Workbench => ViewModel?.Workbench;

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(WorkbenchViewModel.Inspected))
			Show();
	}

	// What it shows by what survives the song being built again: a section's index, a part's section and name
	private static object? KeyOf(object? inspected) => inspected switch
	{
		SectionViewModel section => section.Index,
		PlacementViewModel part => (part.Placement.Section.Name, part.Placement.Name),
		_ => null,
	};

	private void Show()
	{
		var inspected = ViewModel?.Inspected;
		var key = KeyOf(inspected);
		if (_ownEdit && Equals(key, _shownKey))
		{
			_ownEdit = false;
			_current = inspected;
			return;
		}
		_ownEdit = false;
		_shownKey = key;
		_current = inspected;
		_column.Children.Clear();
		switch (inspected)
		{
			case SectionViewModel section:
				ShowSection(section);
				break;
			case PlacementViewModel part:
				ShowPart(part);
				break;
			default:
				_column.Children.Add(Note("Choose a section's name or a part's title to change it here."));
				break;
		}
	}

	// The view model shown now, which a rebuild replaces
	private object? _current;

	private SectionViewModel? Section => _current as SectionViewModel;

	private PlacementViewModel? Part => _current as PlacementViewModel;

	// An edit made here: the song built again is shown without this being made again
	private T Edit<T>(Func<T> edit)
	{
		_ownEdit = true;
		var result = edit();
		if (result is string)
			_ownEdit = false;
		return result;
	}

	private void ShowSection(SectionViewModel section)
	{
		_column.Children.Add(Title(Glyphs.Section(16f), section.Name, "section"));

		var name = new TextBox { Text = section.Name };
		var problem = Problem();
		void Rename()
		{
			if (name.Text.Trim() != Section?.Name && Section is { } s)
				Say(problem, Edit(() => s.Rename(name.Text)));
		}
		name.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is KeyRoutedEventArgs { Key: VirtualKey.Enter })
				Rename();
		});
		name.LostFocus += (_, _) => Rename();
		ToolTipService.SetToolTip(name, "Its name: exits that go to it follow a new one (Enter to take it)");
		var bars = new NumberBox { Minimum = 1, Maximum = 256, SmallChange = 1, LargeChange = 4, Value = section.Bars, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
		bars.ValueChanged += (_, _) =>
		{
			if (!double.IsNaN(bars.Value) && (int)bars.Value != Section?.Bars && Section is { } s)
				Edit(() =>
				{
					s.SetBars((int)bars.Value);
					return 0;
				});
		};
		ToolTipService.SetToolTip(bars, "How many bars long it is");
		var about = Group("Section", true, Field("Name", name), Field("Bars", bars), problem);
		_column.Children.Add(about);

		var chords = new TextBox { Text = section.ChordText };
		var chordProblem = Problem();
		void TakeChords()
		{
			if (chords.Text != Section?.ChordText && Section is { } s)
				Say(chordProblem, Edit(() => s.SetChords(chords.Text)));
		}
		chords.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is KeyRoutedEventArgs { Key: VirtualKey.Enter })
				TakeChords();
		});
		chords.LostFocus += (_, _) => TakeChords();
		ToolTipService.SetToolTip(chords, "A bar to each |, one chord or two or four sharing it: i | VI VII | IV. Click a bar on the ruler to pick its chords instead");
		_column.Children.Add(Group("Chords", true, chords, chordProblem, Note("Or click a bar on the ruler to pick its chords.")));

		_column.Children.Add(Group("Exits", true, ExitsEditor(section)));
	}

	// The section's exits: each a condition and where it goes, tried in order as it ends; the last goes there otherwise
	private StackPanel ExitsEditor(SectionViewModel section)
	{
		var panel = new StackPanel { Orientation = Orientation.Vertical };
		var exits = section.Exits.ToList();
		var targets = ViewModel?.Sections.Select(s => (object?)s.Name).ToList() ?? [];
		var otherwise = exits.Count > 0 && exits[^1].When is null ? exits[^1].Then : null;
		if (otherwise is not null)
			exits.RemoveAt(exits.Count - 1);
		var problem = Problem();

		void Send()
		{
			var all = exits.Select(e => (e.When, e.Then)).ToList();
			if (otherwise is not null)
				all.Add((null, otherwise));
			Say(problem, Edit(() => Section?.SetExits(all)));
		}

		void Build()
		{
			panel.Children.Clear();
			panel.Children.Add(Note("Tried in order as it ends: the first that holds is taken."));
			for (var i = 0; i < exits.Count; i++)
			{
				var at = i;
				var card = new StackPanel { Orientation = Orientation.Vertical };
				var head = new Grid { Margin = new Thickness(0f, 0f, 0f, 4f) };
				head.ColumnDefinitions.Add(GridLength.Auto);
				head.ColumnDefinitions.Add(GridLength.Star);
				head.ColumnDefinitions.Add(GridLength.Auto);
				head.Children.Add(Small($"{at + 1}.  to", Theme.Dim));
				var to = new ComboBox { ItemsSource = targets, SelectedItem = exits[at].Then, Margin = new Thickness(6f, 0f, 6f, 0f) };
				ToolTipService.SetToolTip(to, "The section it goes to");
				to.SelectionChanged += (_, _) =>
				{
					if (to.SelectedItem is string chosen && chosen != exits[at].Then)
					{
						exits[at] = (exits[at].When, chosen);
						Send();
					}
				};
				Grid.SetColumn(to, 1);
				head.Children.Add(to);
				var tools = new StackPanel { Orientation = Orientation.Horizontal };
				tools.Children.Add(Tool(Glyphs.Up(12f), "Try it earlier", at > 0, () =>
				{
					(exits[at - 1], exits[at]) = (exits[at], exits[at - 1]);
					Send();
					Build();
				}));
				tools.Children.Add(Tool(Glyphs.Down(12f), "Try it later", at < exits.Count - 1, () =>
				{
					(exits[at + 1], exits[at]) = (exits[at], exits[at + 1]);
					Send();
					Build();
				}));
				tools.Children.Add(Tool(Glyphs.Close(12f), "Take this exit out", true, () =>
				{
					exits.RemoveAt(at);
					Send();
					Build();
				}));
				Grid.SetColumn(tools, 2);
				head.Children.Add(tools);
				card.Children.Add(head);
				card.Children.Add(new ConditionEditor(Workbench?.ConditionCues ?? [], exits[at].When, when =>
				{
					if (when is null)
						return "An exit before the last has a condition: take it out to go there always.";
					var problem = Workbench?.CheckCondition(when);
					if (problem is not null)
						return problem;
					exits[at] = (when, exits[at].Then);
					Send();
					return null;
				}));
				panel.Children.Add(new Border
				{
					Background = Theme.Sunk,
					CornerRadius = new CornerRadius(6f),
					Padding = new Thickness(8f),
					Margin = new Thickness(0f, 0f, 0f, 6f),
					Child = card,
				});
			}

			var add = new Button { Content = Small("+ exit", Theme.Ink), Padding = new Thickness(8f, 2f, 8f, 3f), Margin = new Thickness(0f, 2f, 0f, 8f) };
			ToolTipService.SetToolTip(add, "Add an exit, tried after these: to another section while a condition holds");
			add.Click += (_, _) =>
			{
				var cue = Workbench?.ConditionCues.FirstOrDefault(c => c.OptionNames.Count == 0);
				exits.Add((cue is null ? "pass > 1" : $"{cue.Name} > 0.5", targets.FirstOrDefault(t => (string?)t != section.Name) as string ?? section.Name));
				Send();
				Build();
			};
			panel.Children.Add(add);

			var last = new Grid();
			last.ColumnDefinitions.Add(GridLength.Auto);
			last.ColumnDefinitions.Add(GridLength.Star);
			last.Children.Add(Small("Otherwise", Theme.Dim));
			var choices = new List<object?> { "play it again" };
			choices.AddRange(targets);
			var fallback = new ComboBox { ItemsSource = choices, SelectedItem = otherwise ?? "play it again", Margin = new Thickness(8f, 0f, 0f, 0f) };
			ToolTipService.SetToolTip(fallback, "Where it goes when no exit above holds");
			fallback.SelectionChanged += (_, _) =>
			{
				var chosen = fallback.SelectedIndex <= 0 ? null : fallback.SelectedItem as string;
				if (chosen != otherwise)
				{
					otherwise = chosen;
					Send();
				}
			};
			Grid.SetColumn(fallback, 1);
			last.Children.Add(fallback);
			panel.Children.Add(last);
			panel.Children.Add(problem);
		}

		Build();
		return panel;
	}

	private void ShowPart(PlacementViewModel part)
	{
		var placement = part.Placement;
		_column.Children.Add(Title(Glyphs.Track(16f), placement.Name, $"{placement.Track.Name} · {placement.Section.Name}"));
		if (Workbench?.CanEditSong != true)
		{
			_column.Children.Add(Note("This song is code alone: a song with a .song.json is edited here."));
			return;
		}

		_column.Children.Add(Group("When it plays", true,
			Note("Asked at each bar line, or only where its phrase starts over."),
			new ConditionEditor(Workbench.ConditionCues, part.When, when => Edit(() => Part?.SetWhen(when)))));

		var levels = new List<object?> { "full" };
		levels.AddRange(Workbench.ConditionCues.Where(c => c.OptionNames.Count == 0).Select(c => (object?)c.Name));
		var level = new ComboBox { ItemsSource = levels, SelectedItem = placement.LevelCue?.Name ?? "full" };
		ToolTipService.SetToolTip(level, "A cue its volume follows, 0 silent to 1 full, or full always");
		level.SelectionChanged += (_, _) =>
		{
			if (Part is { } p)
				Edit(() =>
				{
					Workbench?.SetLevelCue(p.Placement, level.SelectedIndex <= 0 ? null : level.SelectedItem as string);
					return 0;
				});
		};
		var phrase = new ToggleSwitch { IsOn = placement.IsOnPhrase, OnContent = "on its phrase", OffContent = "at any bar" };
		ToolTipService.SetToolTip(phrase, "On its phrase, it comes in and goes only where its part starts over");
		phrase.Toggled += (_, _) =>
		{
			if (Part is { } p)
				Edit(() =>
				{
					Workbench?.SetOnPhrase(p.Placement, phrase.IsOn);
					return 0;
				});
		};
		_column.Children.Add(Group("How it plays", true, Field("Level", level), Field("Comes in", phrase)));

		if (placement.FillPart is not null)
		{
			var remove = new Button { Content = Small("Take the fill out", Theme.Ink), Padding = new Thickness(8f, 2f, 8f, 3f), HorizontalAlignment = HorizontalAlignment.Left };
			ToolTipService.SetToolTip(remove, "Its last bar plays the part again instead of the fill");
			remove.Click += (_, _) =>
			{
				if (Part is { } p)
					Workbench?.RemoveFill(p.Placement);
			};
			_column.Children.Add(Group("Fill", false, Note("Its last bar plays a fill."), remove));
		}
	}

	private static StackPanel Title(UIElement icon, string name, string detail)
	{
		var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 0f, 0f, 10f) };
		row.Children.Add(icon);
		var words = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8f, 0f, 0f, 0f) };
		words.Children.Add(new TextBlock { Text = name, FontSize = 15f, Foreground = Theme.Ink });
		words.Children.Add(new TextBlock { Text = detail, FontSize = 11f, Foreground = Theme.Faint });
		row.Children.Add(words);
		return row;
	}

	private static Expander Group(string header, bool open, params UIElement[] content)
	{
		var panel = new StackPanel { Orientation = Orientation.Vertical };
		foreach (var element in content)
			panel.Children.Add(element);
		return new Expander { Header = header, Content = panel, IsExpanded = open, Margin = new Thickness(0f, 0f, 0f, 8f) };
	}

	private static Grid Field(string label, UIElement control)
	{
		var grid = new Grid { Margin = new Thickness(0f, 0f, 0f, 6f) };
		grid.ColumnDefinitions.Add(GridLength.Pixels(80f));
		grid.ColumnDefinitions.Add(GridLength.Star);
		grid.Children.Add(Small(label, Theme.Dim));
		Grid.SetColumn(control, 1);
		grid.Children.Add(control);
		return grid;
	}

	private static Button Tool(UIElement glyph, string tip, bool enabled, Action act)
	{
		var button = new Button { Content = glyph, Padding = new Thickness(5f, 3f, 5f, 3f), Margin = new Thickness(2f, 0f, 0f, 0f), IsEnabled = enabled };
		ToolTipService.SetToolTip(button, tip);
		button.Click += (_, _) => act();
		return button;
	}

	private static TextBlock Problem() => new() { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

	private static void Say(TextBlock problem, string? text)
	{
		problem.Text = text ?? "";
		problem.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
	}

	private static TextBlock Note(string text) => new() { Text = text, FontSize = 11f, Foreground = Theme.Faint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0f, 0f, 0f, 6f) };

	private static TextBlock Small(string text, SUIsei.Media.Brush brush) => new() { Text = text, FontSize = 12f, Foreground = brush, VerticalAlignment = VerticalAlignment.Center };
}
