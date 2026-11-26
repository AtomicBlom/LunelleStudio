using System.ComponentModel;
using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Input;
using BinaryVibrance.Vysara.RoutedEvents;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// What is chosen in Arrange, to change: the song (tempo, key and scale), a section (its name, bars, chords, and its
/// exits, each a condition and where it goes, in the order they are tried, the last always), a track (its name, and the
/// instrument it plays, opened from here to change it), or a part (when it plays, the cue its volume follows, whether it
/// waits for its phrase, whether a line follows the chords, its grid, its fill). Its groups fold; deleting is a button at
/// the foot. Its own edits build the song again, which makes the chosen thing's view model again; it shows the new one
/// without being made again, so a field being typed in keeps its focus.
/// </summary>
internal sealed class Inspector : ContentControl
{
	private const string CodeOnly = "This song is defined in code. Only songs with a .song.json file can be edited here.";

	// The grids a part can be written on, by their names in a song file
	private static readonly (string Name, string Label)[] Grids =
		[("quarters", "Quarter notes"), ("eighths", "Eighth notes"), ("sixteenths", "Sixteenth notes"), ("triplets", "Triplets")];

	private static readonly object SongKey = new();

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
			{
				field.PropertyChanged -= OnChanged;
				field.Frame -= OnFrame;
			}
			field = value;
			if (value is not null)
			{
				value.PropertyChanged += OnChanged;
				value.Frame += OnFrame;
			}
			Show();
		}
	}

	private Workbench? Workbench => ViewModel?.Workbench;

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(WorkbenchViewModel.Inspected))
			Show();
	}

	// What it shows by what survives the song being built again: the song, a section's index, a track's name, a part's
	// section and name
	private static object? KeyOf(object? inspected) => inspected switch
	{
		SongViewModel => SongKey,
		SectionViewModel section => section.Index,
		TrackViewModel track => new TrackKey(track.Name),
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
		_playsWhen = null;
		switch (inspected)
		{
			case SongViewModel song:
				ShowSong(song);
				break;
			case SectionViewModel section:
				ShowSection(section);
				break;
			case TrackViewModel track:
				ShowTrack(track);
				break;
			case PlacementViewModel part:
				ShowPart(part);
				break;
			default:
				_column.Children.Add(Note("Select the song's name, a section, a track or a part to edit its properties."));
				break;
		}
	}

	// The view model shown now, which a rebuild replaces
	private object? _current;

	// The part's plays-when editor while it shows a part, kept up to date each frame
	private PlaysWhenEditor? _playsWhen;

	private void OnFrame() => _playsWhen?.Refresh();

	private SongViewModel? Song => _current as SongViewModel;

	private SectionViewModel? Section => _current as SectionViewModel;

	private TrackViewModel? Track => _current as TrackViewModel;

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

	private void ShowSong(SongViewModel song)
	{
		_column.Children.Add(Title(Glyphs.Song(16f), song.Name, $"Song · {song.Meter}"));
		if (!song.CanEdit)
		{
			_column.Children.Add(Note(CodeOnly));
			return;
		}

		var tempo = new NumberBox { Minimum = 20, Maximum = 300, SmallChange = 1, LargeChange = 10, Value = song.Tempo, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
		ToolTipService.SetToolTip(tempo, "Beats per minute");
		tempo.ValueChanged += (_, _) =>
		{
			if (!double.IsNaN(tempo.Value) && tempo.Value != Song?.Tempo && Song is { } s)
				Edit(() =>
				{
					s.SetTempo(tempo.Value);
					return 0;
				});
		};

		var key = new ComboBox { ItemsSource = SongFile.ScaleRoots.Select(r => (object?)r).ToList(), SelectedItem = song.Root, HorizontalAlignment = HorizontalAlignment.Stretch };
		ToolTipService.SetToolTip(key, "The key's root note");
		var scale = new ComboBox { ItemsSource = SongFile.ScaleNames.Select(n => (object?)Capitalised(n)).ToList(), SelectedIndex = IndexOf(SongFile.ScaleNames, song.ScaleName), HorizontalAlignment = HorizontalAlignment.Stretch };
		ToolTipService.SetToolTip(scale, "The scale the song's note numbers count through");
		void TakeScale()
		{
			if (key.SelectedItem is string root && scale.SelectedIndex >= 0 && Song is { } s
				&& (root != s.Root || SongFile.ScaleNames[scale.SelectedIndex] != s.ScaleName))
				Edit(() =>
				{
					s.SetScale(root, SongFile.ScaleNames[scale.SelectedIndex]);
					return 0;
				});
		}
		key.SelectionChanged += (_, _) => TakeScale();
		scale.SelectionChanged += (_, _) => TakeScale();

		_column.Children.Add(Group("Song", true, Field("Tempo", tempo), Field("Key", key), Field("Scale", scale),
			Note("Changes are heard from the next bar.")));
	}

	private void ShowSection(SectionViewModel section)
	{
		_column.Children.Add(Title(Glyphs.Section(16f), section.Name, "Section"));

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
		ToolTipService.SetToolTip(name, "Press Enter to rename. Exits to this section are updated.");
		var bars = new NumberBox { Minimum = 1, Maximum = 256, SmallChange = 1, LargeChange = 4, Value = section.Bars, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
		bars.ValueChanged += (_, _) =>
		{
			if (double.IsNaN(bars.Value))
				return;
			// The box shows what is typed as it is, so a part bar is rounded here, and the box shows what was taken
			var whole = (int)Math.Round(bars.Value);
			if (whole != bars.Value)
				bars.Value = whole;
			else if (whole != Section?.Bars && Section is { } s)
				Edit(() =>
				{
					s.SetBars(whole);
					return 0;
				});
		};
		ToolTipService.SetToolTip(bars, "Length in bars");
		_column.Children.Add(Group("Section", true, Field("Name", name), Field("Bars", bars), problem));

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
		ToolTipService.SetToolTip(chords, "Roman numerals, bars separated by |. Up to four chords can share a bar: i | VI VII | IV");
		_column.Children.Add(Group("Chords", true, chords, chordProblem, Note("Or click a bar on the section's ruler to pick its chords.")));

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
			panel.Children.Add(Note("When the section ends, exits are checked in order and the first whose condition is met is taken."));
			for (var i = 0; i < exits.Count; i++)
			{
				var at = i;
				var card = new StackPanel { Orientation = Orientation.Vertical };
				var head = new Grid { Margin = new Thickness(0f, 0f, 0f, 4f) };
				head.ColumnDefinitions.Add(GridLength.Auto);
				head.ColumnDefinitions.Add(GridLength.Star);
				head.ColumnDefinitions.Add(GridLength.Auto);
				head.Children.Add(Small($"{at + 1}.  Go to", Theme.Dim));
				var to = new ComboBox { ItemsSource = targets, SelectedItem = exits[at].Then, Margin = new Thickness(6f, 0f, 6f, 0f) };
				ToolTipService.SetToolTip(to, "Section to go to");
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
				tools.Children.Add(Tool(Glyphs.Up(12f), "Move up", at > 0, () =>
				{
					(exits[at - 1], exits[at]) = (exits[at], exits[at - 1]);
					Send();
					Build();
				}));
				tools.Children.Add(Tool(Glyphs.Down(12f), "Move down", at < exits.Count - 1, () =>
				{
					(exits[at + 1], exits[at]) = (exits[at], exits[at + 1]);
					Send();
					Build();
				}));
				tools.Children.Add(Tool(Glyphs.Trash(12f), "Delete exit", true, () =>
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
						return "Every exit but the last needs a condition. To always take this one, make it the Otherwise section.";
					var problem = Workbench?.CheckCondition(when);
					if (problem is not null)
						return problem;
					exits[at] = (when, exits[at].Then);
					Send();
					return null;
				}, Workbench));
				panel.Children.Add(new Border
				{
					Background = Theme.Sunk,
					CornerRadius = new CornerRadius(6f),
					Padding = new Thickness(8f),
					Margin = new Thickness(0f, 0f, 0f, 6f),
					Child = card,
				});
			}

			var add = new Button { Content = Small("+ Add exit", Theme.Ink), Padding = new Thickness(8f, 2f, 8f, 3f), Margin = new Thickness(0f, 2f, 0f, 8f) };
			ToolTipService.SetToolTip(add, "Add a conditional exit, checked after the ones above");
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
			var choices = new List<object?> { "Repeat this section" };
			choices.AddRange(targets);
			var fallback = new ComboBox { ItemsSource = choices, SelectedItem = otherwise ?? "Repeat this section", Margin = new Thickness(8f, 0f, 0f, 0f) };
			ToolTipService.SetToolTip(fallback, "Where to go when no exit's condition is met");
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

	private sealed record TrackKey(string Name);

	private void ShowTrack(TrackViewModel track)
	{
		_column.Children.Add(Title(Glyphs.Track(16f), track.Name, $"Track · {track.InstrumentName}"));

		var open = new Button { Padding = new Thickness(10f, 4f, 10f, 5f), Margin = new Thickness(0f, 2f, 0f, 6f), HorizontalAlignment = HorizontalAlignment.Left };
		var label = new StackPanel { Orientation = Orientation.Horizontal };
		label.Children.Add(track.IsKit ? Glyphs.Kit(12f, Theme.Ink) : Glyphs.Patch(12f, Theme.Ink));
		label.Children.Add(new TextBlock
		{
			Text = track.IsKit ? "Open kit" : "Edit patch",
			FontSize = 12f,
			Foreground = Theme.Ink,
			Margin = new Thickness(6f, 0f, 0f, 0f),
			VerticalAlignment = VerticalAlignment.Center,
		});
		open.Content = label;
		ToolTipService.SetToolTip(open, track.IsKit
			? $"Open {track.InstrumentName} in the Kit view"
			: $"Open {track.InstrumentName} in the patch designer. Edits are heard in the song from the next bar and saved to the patch file.");
		open.Click += (_, _) => Track?.OpenInstrument();

		if (!track.CanEdit)
		{
			_column.Children.Add(Group("Instrument", true, Note(CodeOnly), open));
			return;
		}

		var name = new TextBox { Text = track.Name };
		var problem = Problem();
		void Rename()
		{
			if (name.Text.Trim() != Track?.Name && Track is { } t)
				Say(problem, Edit(() => t.Rename(name.Text)));
		}
		name.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is KeyRoutedEventArgs { Key: VirtualKey.Enter })
				Rename();
		});
		name.LostFocus += (_, _) => Rename();
		ToolTipService.SetToolTip(name, "Press Enter to rename. The track's parts are updated.");
		_column.Children.Add(Group("Track", true, Field("Name", name), problem));

		var instruments = track.Instruments;
		var choice = new ComboBox
		{
			ItemsSource = instruments.Select(i => (object?)$"{i.Name} ({(i.IsKit ? "kit" : "patch")})").ToList(),
			SelectedIndex = instruments.FindIndex(i => i.Name == track.InstrumentName && i.IsKit == track.IsKit),
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		ToolTipService.SetToolTip(choice, "The patch or kit this track plays");
		choice.SelectionChanged += (_, _) =>
		{
			var at = choice.SelectedIndex;
			if (at >= 0 && Track is { } t && (instruments[at].Name != t.InstrumentName || instruments[at].IsKit != t.IsKit))
				t.SetInstrument(instruments[at].Name, instruments[at].IsKit);
		};
		var instrument = new List<UIElement> { Field("Plays", choice), open };
		// A patch other songs play too: an edit to it changes them as well, unless this song's track has a copy of its own
		if (track.PatchUsedElsewhere is { Count: > 0 } others)
		{
			instrument.Add(Note(others.Count == 1
				? $"{track.InstrumentName} is also used in {others[0]}. Edits to it change that song too."
				: $"{track.InstrumentName} is also used in {others.Count} other songs ({string.Join(", ", others)}). Edits to it change them too."));
			var copy = new Button { Content = new TextBlock { Text = "Make a copy for this song", FontSize = 12f }, Padding = new Thickness(10f, 4f, 10f, 5f), HorizontalAlignment = HorizontalAlignment.Left };
			ToolTipService.SetToolTip(copy, $"Copy {track.InstrumentName} to a new patch for this track alone, and open it to edit. Other songs keep {track.InstrumentName} as it is.");
			var copyProblem = Problem();
			copy.Click += (_, _) => Say(copyProblem, Track?.CopyPatchForSong());
			instrument.Add(copy);
			instrument.Add(copyProblem);
		}
		_column.Children.Add(Group("Instrument", true, [.. instrument]));

		_column.Children.Add(DeleteButton("Delete track", "Delete this track and all of its parts", () => Track?.Delete()));
	}

	private void ShowPart(PlacementViewModel part)
	{
		var placement = part.Placement;
		_column.Children.Add(Title(Glyphs.Track(16f), placement.Name, $"Part · {placement.Track.Name} · {placement.Section.Name}"));
		if (Workbench?.CanEditSong != true)
		{
			_column.Children.Add(Note(CodeOnly));
			return;
		}

		_playsWhen = new PlaysWhenEditor(part, when => Edit(() => Part?.SetWhen(when)));
		_column.Children.Add(Group("Plays when", true,
			Note("Checked at every bar line, or only where the part's phrase begins."),
			_playsWhen));

		var levels = new List<object?> { "Full" };
		levels.AddRange(Workbench.ConditionCues.Where(c => c.OptionNames.Count == 0).Select(c => (object?)c.Name));
		var level = new ComboBox { ItemsSource = levels, SelectedItem = placement.LevelCue?.Name ?? "Full", HorizontalAlignment = HorizontalAlignment.Stretch };
		ToolTipService.SetToolTip(level, "Full volume, or a cue to follow from 0 (silent) to 1 (full)");
		level.SelectionChanged += (_, _) =>
		{
			if (Part is { } p)
				Edit(() =>
				{
					Workbench?.SetLevelCue(p.Placement, level.SelectedIndex <= 0 ? null : level.SelectedItem as string);
					return 0;
				});
		};
		var phrase = new ToggleSwitch { IsOn = placement.IsOnPhrase, OnContent = "At phrase start", OffContent = "At any bar" };
		ToolTipService.SetToolTip(phrase, "When the part may start and stop: at any bar line, or only where its phrase begins");
		phrase.Toggled += (_, _) =>
		{
			if (Part is { } p)
				Edit(() =>
				{
					Workbench?.SetOnPhrase(p.Placement, phrase.IsOn);
					return 0;
				});
		};
		_column.Children.Add(Group("Playback", true, Field("Volume", level), Field("Starts", phrase)));

		var notes = new List<UIElement>();
		if (part.GridChoice is { } grid)
		{
			var offered = Grids.Where(g => grid.Grids.Contains(g.Name)).ToList();
			var picker = new ComboBox
			{
				ItemsSource = offered.Select(g => (object?)g.Label).ToList(),
				SelectedIndex = offered.FindIndex(g => g.Name == grid.Grid),
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};
			ToolTipService.SetToolTip(picker, "The length of one step in the roll. Notes keep their timing when it changes.");
			var gridProblem = Problem();
			picker.SelectionChanged += (_, _) =>
			{
				if (picker.SelectedIndex >= 0 && Part is { } p && offered[picker.SelectedIndex].Name != p.GridChoice?.Grid)
					Say(gridProblem, p.SetGrid(offered[picker.SelectedIndex].Name));
			};
			notes.Add(Field("Grid", picker));
			notes.Add(gridProblem);
		}
		if (part.IsLine)
		{
			var chords = new ToggleSwitch { IsOn = part.IsOverChords, OnContent = "Follow chords", OffContent = "Scale degrees" };
			ToolTipService.SetToolTip(chords, "Follow chords: 1 is each chord's root, as for a bass line. Scale degrees: numbers count through the song's scale whatever the chord.");
			chords.Toggled += (_, _) =>
			{
				if (Part is { } p)
					Edit(() =>
					{
						p.SetOverChords(chords.IsOn);
						return 0;
					});
			};
			notes.Add(Field("Notes", chords));
		}
		if (notes.Count > 0)
			_column.Children.Add(Group("Notes", true, [.. notes]));

		if (placement.FillPart is not null)
		{
			var edit = new Button { Content = Small("Edit fill", Theme.Ink), Padding = new Thickness(8f, 2f, 8f, 3f), Margin = new Thickness(0f, 0f, 6f, 0f) };
			ToolTipService.SetToolTip(edit, "Open the fill in the roll, as a click on its bar in the lane does");
			edit.Click += (_, _) => Part?.SelectBar(Part.Bars - 1);
			var remove = new Button { Content = Small("Remove fill", Theme.Ink), Padding = new Thickness(8f, 2f, 8f, 3f) };
			ToolTipService.SetToolTip(remove, "Play the part itself in its last bar instead of the fill");
			remove.Click += (_, _) => Part?.RemoveFill();
			var buttons = new StackPanel { Orientation = Orientation.Horizontal };
			buttons.Children.Add(edit);
			buttons.Children.Add(remove);
			_column.Children.Add(Group("Fill", false, Note("The part's last bar plays a fill."), buttons));
		}
		else if (part.IsData)
		{
			var add = new Button { Content = Small("Add fill", Theme.Ink), Padding = new Thickness(8f, 2f, 8f, 3f), HorizontalAlignment = HorizontalAlignment.Left };
			ToolTipService.SetToolTip(add, "End each pass of the section with a bar of its own: its last bar, copied, to change in the roll");
			var problem = Note("");
			problem.Visibility = Visibility.Collapsed;
			add.Click += (_, _) =>
			{
				if (Part?.AddFill() is { } why)
				{
					problem.Text = why;
					problem.Visibility = Visibility.Visible;
				}
			};
			_column.Children.Add(Group("Fill", false, Note("A fill takes the part's place in the section's last bar."), add, problem));
		}

		_column.Children.Add(DeleteButton("Delete part", "Delete this part from the section", () => Part?.Delete()));
	}

	// Its title: what it shows, and a button that puts it away, giving the board its room back
	private Grid Title(UIElement icon, string name, string detail)
	{
		var row = new Grid { Margin = new Thickness(0f, 0f, 0f, 10f) };
		row.ColumnDefinitions.Add(GridLength.Auto);
		row.ColumnDefinitions.Add(GridLength.Star);
		row.ColumnDefinitions.Add(GridLength.Auto);
		row.Children.Add(icon);
		var words = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8f, 0f, 0f, 0f) };
		words.Children.Add(new TextBlock { Text = name, FontSize = 15f, Foreground = Theme.Ink, TextTrimming = TextTrimming.CharacterEllipsis });
		words.Children.Add(new TextBlock { Text = detail, FontSize = 11f, Foreground = Theme.Faint, TextTrimming = TextTrimming.CharacterEllipsis });
		Grid.SetColumn(words, 1);
		row.Children.Add(words);
		var close = Tool(Glyphs.Close(11f), "Close", true, () => Workbench?.CloseInspector());
		Grid.SetColumn(close, 2);
		row.Children.Add(close);
		return row;
	}

	// A delete at the foot of the inspector: a trash can and what it deletes
	private static Button DeleteButton(string text, string tip, Action act)
	{
		var content = new StackPanel { Orientation = Orientation.Horizontal };
		content.Children.Add(Glyphs.Trash(13f, Theme.Dim));
		content.Children.Add(new TextBlock { Text = text, FontSize = 12f, Foreground = Theme.Ink, Margin = new Thickness(6f, 0f, 0f, 0f), VerticalAlignment = VerticalAlignment.Center });
		var button = new Button { Content = content, Padding = new Thickness(10f, 4f, 12f, 5f), Margin = new Thickness(0f, 6f, 0f, 0f), HorizontalAlignment = HorizontalAlignment.Left };
		ToolTipService.SetToolTip(button, tip);
		button.Click += (_, _) => act();
		return button;
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

	private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

	private static int IndexOf(IReadOnlyList<string> list, string item)
	{
		for (var i = 0; i < list.Count; i++)
			if (list[i] == item)
				return i;
		return -1;
	}

	private static TextBlock Problem() => new() { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

	private static void Say(TextBlock problem, string? text)
	{
		problem.Text = text ?? "";
		problem.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
	}

	private static TextBlock Note(string text) => new() { Text = text, FontSize = 11f, Foreground = Theme.Faint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0f, 0f, 0f, 6f) };

	private static TextBlock Small(string text, BinaryVibrance.Vysara.Media.Brush brush) => new() { Text = text, FontSize = 12f, Foreground = brush, VerticalAlignment = VerticalAlignment.Center };
}
