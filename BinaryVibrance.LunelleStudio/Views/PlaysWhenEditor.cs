using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;
using BinaryVibrance.Vysara.Input;
using BinaryVibrance.Vysara.RoutedEvents;
using BinaryVibrance.Vysara.Shapes;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// When a part plays, shaped to the project's cues: held on or off while listening (not saved), the condition in a
/// sentence, whether it holds now and why, then a clause a cue (a level cue's range on a rail, dragged; a choice cue's
/// options as chips, or every one but them; which passes of the section), and the condition as the song file writes it,
/// which can be edited as text. A condition the clauses can't hold (an <c>or</c> across cues, say) is edited as text
/// alone, and takes the clauses back once it can. Each change is written at once (a drag as it is let go); the host
/// calls <see cref="Refresh"/> each frame while it shows it, for the line that says whether the part plays now.
/// </summary>
internal sealed class PlaysWhenEditor : ContentControl
{
	private const float RailWidth = 288f;

	private readonly PlacementViewModel _part;
	private readonly IReadOnlyList<CueBase> _cues;
	private readonly Func<string?, string?> _apply;
	private readonly StackPanel _column = new() { Orientation = Orientation.Vertical, Width = RailWidth + 24f };
	private readonly TextBlock _sentence = Text("", 13f, Theme.Ink);
	private readonly TextBlock _now = Text("", 11f, Theme.Dim);
	private readonly TextBlock _held = Text("", 11f, Theme.Error);
	private readonly TextBlock _problem = Text("", 11f, Theme.Error);
	private readonly List<Action> _refreshers = [];
	private readonly CueMaker _maker;
	private ConditionShape? _shape;
	private string? _when;
	private bool _asText;
	private PlacementState _state;

	/// <param name="part">The part: the state it holds on or off, and the cues and section it reads.</param>
	/// <param name="apply">Takes the condition as written (null: always) and gives why it can't be, or null; the part's own by default.</param>
	public PlaysWhenEditor(PlacementViewModel part, Func<string?, string?>? apply = null)
	{
		IsTabStop = false;
		_part = part;
		_apply = apply ?? part.SetWhen;
		_cues = part.Cues;
		_when = part.When;
		_state = part.State;
		_shape = ConditionShape.Read(part.Placement.Condition, _cues);
		_asText = _shape is null;
		_sentence.TextWrapping = TextWrapping.Wrap;
		_now.TextWrapping = TextWrapping.Wrap;
		_held.TextWrapping = TextWrapping.Wrap;
		_problem.TextWrapping = TextWrapping.Wrap;
		// A condition naming a cue the project hasn't offers to make it, then is taken again
		_maker = new CueMaker(part.Workbench, condition =>
		{
			var why = _apply(condition);
			Problem(why, condition);
			if (why is null)
			{
				_when = condition;
				Build();
			}
		});
		Content = _column;
		Build();
	}

	/// <summary>Brings what changes as the song plays up to date: whether the part plays now, and where each level cue is.</summary>
	public void Refresh()
	{
		ShowNow();
		foreach (var refresh in _refreshers)
			refresh();
	}

	private void Build()
	{
		_column.Children.Clear();
		_refreshers.Clear();

		var play = Segments(["Auto", "On", "Off"], (int)_state, at =>
		{
			_state = (PlacementState)at;
			_part.State = _state;
			Build();
		}, ["Play by its condition", "Play always, while you listen", "Never play, while you listen"]);
		_column.Children.Add(Row(Label("Play"), play));
		_held.Text = _state == PlacementState.Auto ? "" : $"Held {(_state == PlacementState.On ? "on" : "off")} while you listen. Not saved: the song keeps its condition.";
		_held.Visibility = _state == PlacementState.Auto ? Visibility.Collapsed : Visibility.Visible;
		_held.Margin = new Thickness(0f, 4f, 0f, 0f);
		_column.Children.Add(_held);

		_sentence.Margin = new Thickness(0f, 10f, 0f, 2f);
		_column.Children.Add(_sentence);
		_column.Children.Add(_now);

		if (_shape is { } shape && !_asText)
		{
			foreach (var level in shape.Levels)
				_column.Children.Add(LevelClause(level));
			foreach (var choice in shape.Choices)
				_column.Children.Add(ChoiceClause(choice));
			_column.Children.Add(PassClause(shape));
		}
		else if (_shape is null)
		{
			_column.Children.Add(Note("This condition joins its cues in a way the controls can't show, so it is edited as text."));
		}
		_column.Children.Add(TextClause());
		_problem.Visibility = Visibility.Collapsed;
		_column.Children.Add(_problem);
		_maker.Offer(null);
		_column.Children.Add(_maker);
		ShowSentence();
		ShowNow();
	}

	// A level cue: any level, from one up, up to one, or between two; a rail to drag them on, where the cue is now, and the
	// levels the section's other parts wait at
	private StackPanel LevelClause(LevelClause clause)
	{
		var box = Clause();
		var rules = Segments(["Any", "Above", "Below", "Between"], (int)clause.Rule, at =>
		{
			clause.Rule = (LevelRule)at;
			if (clause.Rule == LevelRule.Between && clause.To <= clause.From)
				clause.To = MathF.Min(1f, clause.From + 0.3f);
			Commit();
			Build();
		}, ["Plays at any level", "Plays at or above a level", "Plays at or under a level", "Plays between two levels"]);
		box.Children.Add(Row(Title(clause.Cue.Name), rules));

		var canvas = new Canvas { Width = RailWidth, Height = 44f, Background = Theme.Clear, Margin = new Thickness(6f, 4f, 0f, 0f) };
		var rail = new Border { Width = RailWidth, Height = 6f, CornerRadius = new CornerRadius(3f), Background = Theme.Sunk };
		Canvas.SetTop(rail, 18f);
		var range = new Border { Height = 6f, CornerRadius = new CornerRadius(3f), Background = Theme.Accent, Opacity = 0.75f };
		Canvas.SetTop(range, 18f);
		var from = Handle();
		var to = Handle();
		var fromText = Text("", 10.5f, Theme.Ink);
		var toText = Text("", 10.5f, Theme.Ink);
		var now = new Border { Width = 2f, Height = 18f, CornerRadius = new CornerRadius(1f), Background = Theme.Meter, IsHitTestVisible = false };
		Canvas.SetTop(now, 12f);
		canvas.Children.Add(rail);
		canvas.Children.Add(range);
		// The section's other parts waiting on the cue, a tick and a name each under the rail
		foreach (var (name, level) in _part.OthersOn(clause.Cue))
		{
			var tick = new Border { Width = 1f, Height = 5f, Background = Theme.Faint };
			Canvas.SetLeft(tick, X(level));
			Canvas.SetTop(tick, 27f);
			var label = Text(name, 9.5f, Theme.Faint);
			Canvas.SetLeft(label, MathF.Min(X(level) - 8f, RailWidth - 40f));
			Canvas.SetTop(label, 31f);
			ToolTipService.SetToolTip(tick, $"{name}: waits at {ConditionShape.Number(level)}");
			canvas.Children.Add(tick);
			canvas.Children.Add(label);
		}
		canvas.Children.Add(now);
		canvas.Children.Add(from);
		canvas.Children.Add(to);
		canvas.Children.Add(fromText);
		canvas.Children.Add(toText);
		ToolTipService.SetToolTip(canvas, clause.Rule == LevelRule.Any ? $"Click to play only above a level of {clause.Cue.Name}" : "Drag to change; the green tick is where the cue is now");

		void Place()
		{
			var any = clause.Rule == LevelRule.Any;
			var low = clause.Rule is LevelRule.Above or LevelRule.Between;
			var high = clause.Rule is LevelRule.Below or LevelRule.Between;
			canvas.Opacity = any ? 0.5f : 1f;
			range.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
			var left = low ? clause.From : 0f;
			var right = high ? clause.To : 1f;
			Canvas.SetLeft(range, X(left));
			range.Width = MathF.Max(2f, X(right) - X(left));
			Show(from, fromText, low ? clause.From : -1f);
			Show(to, toText, high ? clause.To : -1f);
		}
		void Show(Ellipse handle, TextBlock text, float level)
		{
			handle.Visibility = text.Visibility = level < 0f ? Visibility.Collapsed : Visibility.Visible;
			if (level < 0f)
				return;
			Canvas.SetLeft(handle, X(level) - 7f);
			Canvas.SetTop(handle, 14f);
			text.Text = ConditionShape.Number(level);
			Canvas.SetLeft(text, Math.Clamp(X(level) - 10f, 0f, RailWidth - 24f));
			Canvas.SetTop(text, 0f);
		}
		_refreshers.Add(() => Canvas.SetLeft(now, X(Math.Clamp(_part.CueNow(clause.Cue), 0f, 1f)) - 1f));

		// Dragging the nearer of the levels shown; a press while any level plays starts it playing from there up
		var dragging = 0;
		float LevelAt(RoutedEventArgs e) => e is PointerRoutedEventArgs p
			? MathF.Round(Math.Clamp(p.GetCurrentPoint(canvas).Position.X / RailWidth, 0f, 1f) * 100f) / 100f
			: 0f;
		void Move(float level)
		{
			if (dragging == 1)
				clause.From = clause.Rule == LevelRule.Between ? MathF.Min(level, clause.To - 0.01f) : level;
			else
				clause.To = clause.Rule == LevelRule.Between ? MathF.Max(level, clause.From + 0.01f) : level;
			Place();
			ShowSentence();
			ShowNow();
		}
		canvas.PointerPressed += (_, e) =>
		{
			if (e is not PointerRoutedEventArgs p || !p.Pointer.Buttons.HasFlag(PointerButtons.Left))
				return;
			var level = LevelAt(e);
			if (clause.Rule == LevelRule.Any)
				clause.Rule = LevelRule.Above;
			dragging = clause.Rule switch
			{
				LevelRule.Above => 1,
				LevelRule.Below => 2,
				_ => MathF.Abs(level - clause.From) <= MathF.Abs(level - clause.To) ? 1 : 2,
			};
			canvas.CapturePointer(p.PointerId);
			Move(level);
			e.Handled = true;
		};
		canvas.PointerMoved += (_, e) =>
		{
			if (dragging != 0)
				Move(LevelAt(e));
		};
		void Let()
		{
			if (dragging == 0)
				return;
			dragging = 0;
			Commit();
			Build();
		}
		canvas.PointerReleased += (_, _) => Let();
		canvas.PointerCaptureLost += (_, _) => Let();
		Place();
		box.Children.Add(canvas);
		return box;
	}

	// A choice cue: its options as chips, any lit playing it; "except" plays every option but those lit
	private StackPanel ChoiceClause(ChoiceClause clause)
	{
		var box = Clause();
		var except = new ToggleButton { Content = Small(clause.Except ? "except ✓" : "except"), IsChecked = clause.Except, Padding = new Thickness(7f, 1f, 7f, 2f) };
		ToolTipService.SetToolTip(except, "Play on every option but those lit");
		except.Click += (_, _) =>
		{
			clause.Except = !clause.Except;
			Commit();
			Build();
		};
		box.Children.Add(Row(Title(clause.Cue.Name), except));
		var names = clause.Cue.OptionNames;
		StackPanel? line = null;
		for (var i = 0; i < names.Count; i++)
		{
			if (i % 3 == 0)
			{
				line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 3f, 0f, 0f) };
				box.Children.Add(line);
			}
			var value = clause.Cue.OptionValue(i);
			var on = clause.Values.Contains(value);
			var text = Small(names[i]);
			var chip = new ToggleButton { Content = text, IsChecked = on, Padding = new Thickness(9f, 2f, 9f, 3f), Margin = new Thickness(0f, 0f, 4f, 0f), Width = 92f };
			ToolTipService.SetToolTip(chip, on ? $"Stop playing on {names[i]}" : $"Play on {names[i]}");
			chip.Click += (_, _) =>
			{
				if (!clause.Values.Remove(value))
				{
					clause.Values.Add(value);
					clause.Values.Sort();
				}
				Commit();
				Build();
			};
			line!.Children.Add(chip);
			// The option the cue is on now, its name in the meter's green
			var cue = clause.Cue;
			_refreshers.Add(() => text.Foreground = _part.CueNow(cue) == value ? Theme.Meter : Theme.Ink);
		}
		return box;
	}

	// The section's passes: every one, the first alone, from the second on (or the one pass a file names)
	private StackPanel PassClause(ConditionShape shape)
	{
		var box = Clause();
		List<string> words = ["Every", "First only", "From the 2nd"];
		if (shape.Pass == PassRule.Only)
			words.Add($"Pass {shape.OnlyPass}");
		var rules = Segments(words, (int)shape.Pass, at =>
		{
			shape.Pass = (PassRule)at;
			Commit();
			Build();
		}, ["Every pass of the section in a row", "Only the first time through", "From the second time through on", "That pass alone"]);
		box.Children.Add(Row(Title("pass"), rules));
		return box;
	}

	// The condition as the song file writes it: shown, or edited as text, checked as it is typed
	private StackPanel TextClause()
	{
		var box = Clause();
		var edit = new Button { Content = Small(_asText ? "Done" : "Edit as text"), Padding = new Thickness(8f, 1f, 8f, 2f) };
		box.Children.Add(Row(Label("As text"), edit));
		if (_asText)
		{
			var text = new TextBox { Text = _when ?? "", FontSize = 12f, Margin = new Thickness(0f, 4f, 0f, 0f) };
			ToolTipService.SetToolTip(text, "For example: intensity > 0.5 and not biome is Forest. Empty plays always.");
			text.TextChanged += (_, _) =>
			{
				var typed = text.Text.Trim();
				var why = _apply(typed.Length == 0 ? null : typed);
				Problem(why, typed);
				if (why is null)
				{
					_when = typed.Length == 0 ? null : typed;
					_shape = Reread(_when);
					ShowSentence();
					ShowNow();
				}
			};
			box.Children.Add(text);
			edit.Click += (_, _) =>
			{
				// Back to the clauses where the text has a shape; else it stays text
				if (_shape is not null)
					_asText = false;
				Build();
			};
		}
		else
		{
			var written = Text(_when ?? "(always)", 11.5f, Theme.Dim);
			written.TextWrapping = TextWrapping.Wrap;
			written.Margin = new Thickness(0f, 4f, 0f, 0f);
			box.Children.Add(written);
			edit.Click += (_, _) =>
			{
				_asText = true;
				Build();
			};
		}
		return box;
	}

	// The clauses written, as the song file writes them; why they can't be shows under the editor
	private void Commit()
	{
		if (_shape is null)
			return;
		var when = _shape.Write();
		var why = _apply(when);
		Problem(why, when);
		if (why is null)
			_when = when;
	}

	private ConditionShape? Reread(string? when)
	{
		if (when is null)
			return ConditionShape.Always(_cues);
		try
		{
			return ConditionShape.Read(ConditionText.Parse(when, name => _cues.FirstOrDefault(c => c.Name == name)), _cues);
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private void Problem(string? why, string? condition = null)
	{
		_problem.Text = why ?? "";
		_problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
		_maker.Offer(why is null ? null : condition);
	}

	private void ShowSentence() => _sentence.Text = _shape?.Sentence() ?? $"Plays when {_when}.";

	// Whether it plays now and why: held, or the first clause that doesn't hold, or each that does
	private void ShowNow()
	{
		var (met, why) = _state switch
		{
			PlacementState.On => (true, "held on"),
			PlacementState.Off => (false, "held off"),
			_ => _shape?.Now(_part.CueNow, _part.SectionPass) ?? (_part.ConditionMet, "its condition is " + (_part.ConditionMet ? "met" : "not met")),
		};
		var text = $"{(met ? "Playing" : "Silent")} now: {why}.";
		if (_now.Text != text)
			_now.Text = text;
		_now.Foreground = met ? Theme.Meter : Theme.Dim;
	}

	private static float X(float level) => Math.Clamp(level, 0f, 1f) * RailWidth;

	private static Ellipse Handle() => new() { Width = 14f, Height = 14f, Fill = Theme.Ink, IsHitTestVisible = false };

	// A row of toggles, one lit: the one chosen
	private static StackPanel Segments(IReadOnlyList<string> words, int chosen, Action<int> choose, IReadOnlyList<string> tips)
	{
		var row = new StackPanel { Orientation = Orientation.Horizontal };
		for (var i = 0; i < words.Count; i++)
		{
			var at = i;
			var toggle = new ToggleButton { Content = Small(words[i]), IsChecked = i == chosen, Padding = new Thickness(8f, 1f, 8f, 2f), Margin = new Thickness(1f, 0f, 0f, 0f) };
			if (i < tips.Count)
				ToolTipService.SetToolTip(toggle, tips[i]);
			toggle.Click += (_, _) =>
			{
				toggle.IsChecked = true;
				if (at != chosen)
					choose(at);
			};
			row.Children.Add(toggle);
		}
		return row;
	}

	private static StackPanel Clause() => new() { Orientation = Orientation.Vertical, Margin = new Thickness(0f, 12f, 0f, 0f) };

	private static Grid Row(UIElement left, UIElement right)
	{
		var grid = new Grid();
		grid.ColumnDefinitions.Add(GridLength.Star);
		grid.ColumnDefinitions.Add(GridLength.Auto);
		grid.Children.Add(left);
		Grid.SetColumn(right, 1);
		grid.Children.Add(right);
		return grid;
	}

	private static TextBlock Title(string text)
	{
		var title = Text(text, 12f, Theme.Ink);
		title.VerticalAlignment = VerticalAlignment.Center;
		return title;
	}

	private static TextBlock Label(string text)
	{
		var label = Text(text.ToUpperInvariant(), 10f, Theme.Faint);
		label.VerticalAlignment = VerticalAlignment.Center;
		return label;
	}

	private static TextBlock Note(string text)
	{
		var note = Text(text, 11f, Theme.Dim);
		note.TextWrapping = TextWrapping.Wrap;
		note.Margin = new Thickness(0f, 10f, 0f, 0f);
		return note;
	}

	private static TextBlock Small(string text) => new() { Text = text, FontSize = 11.5f };

	private static TextBlock Text(string text, float size, BinaryVibrance.Vysara.Media.Brush brush) => new() { Text = text, FontSize = size, Foreground = brush };
}
