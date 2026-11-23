using System.Globalization;
using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// A condition built from rows, as a song file writes it: each row a cue (or the section's pass), a comparison and a
/// value (a level, a number of passes, a choice's option), the rows joined by all or any of them; none is "always". A
/// condition the rows can't hold (one with <c>not</c>, or <c>and</c> and <c>or</c> mixed) is edited as text, checked as it is
/// typed, and folds back into rows once it can. Every change is handed to the apply function it was made with, whose answer (why it can't
/// be taken, or null) shows under it.
/// </summary>
internal sealed class ConditionEditor : ContentControl
{
	private const string PassName = "pass";

	private readonly IReadOnlyList<CueBase> _cues;
	private readonly Func<string?, string?> _apply;
	private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical };
	private readonly StackPanel _join = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 0f, 0f, 6f) };
	private readonly ToggleButton _all = Segment("All of");
	private readonly ToggleButton _any = Segment("Any of");
	private readonly TextBox _text = new() { FontSize = 12f };
	private readonly ToggleButton _asText = new() { Content = Small("as text"), Padding = new Thickness(8f, 2f, 8f, 3f) };
	private readonly TextBlock _problem = new() { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
	private readonly List<(string Subject, string Op, string Value)> _model = [];
	private bool _anyOf;
	private bool _showing;

	/// <param name="cues">The cues a row can name.</param>
	/// <param name="when">The condition as written, or null for always.</param>
	/// <param name="apply">Takes the condition as written (null: always); gives why it can't be taken, or null.</param>
	public ConditionEditor(IReadOnlyList<CueBase> cues, string? when, Func<string?, string?> apply)
	{
		_cues = cues;
		_apply = apply;
		IsTabStop = false;
		_join.Children.Add(_all);
		_join.Children.Add(_any);
		_all.Click += (_, _) => Join(anyOf: false);
		_any.Click += (_, _) => Join(anyOf: true);
		ToolTipService.SetToolTip(_all, "Met when every row is met");
		ToolTipService.SetToolTip(_any, "Met when any row is met");
		ToolTipService.SetToolTip(_asText, "Edit the condition as text, for example: intensity > 0.5 and not biome is Forest");

		var add = new Button { Content = Small("+ condition"), Padding = new Thickness(8f, 2f, 8f, 3f), Margin = new Thickness(0f, 0f, 6f, 0f) };
		add.Click += (_, _) => AddRow();
		ToolTipService.SetToolTip(add, "Add a row comparing a cue with a value, or checking the pass");
		_asText.Click += (_, _) => ShowText(_asText.IsChecked == true);
		_text.TextChanged += (_, _) =>
		{
			if (!_showing && _asText.IsChecked == true)
				TakeText();
		};
		var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 6f, 0f, 0f) };
		tools.Children.Add(add);
		tools.Children.Add(_asText);

		// As wide as a row needs and no wider: in a flyout it is offered all the width there is
		var column = new StackPanel { Orientation = Orientation.Vertical, MinWidth = 280f, MaxWidth = 380f };
		column.Children.Add(_join);
		column.Children.Add(_rows);
		column.Children.Add(_text);
		column.Children.Add(tools);
		column.Children.Add(_problem);
		Content = column;
		Load(when);
	}

	// The condition shown: as rows where they can hold it, else as text
	private void Load(string? when)
	{
		_model.Clear();
		var rows = when is null || TryRows(when, _model, out _anyOf);
		_asText.IsChecked = !rows;
		_showing = true;
		_text.Text = when ?? "";
		_showing = false;
		ShowText(!rows);
	}

	private void ShowText(bool asText)
	{
		_text.Visibility = asText ? Visibility.Visible : Visibility.Collapsed;
		_rows.Visibility = asText ? Visibility.Collapsed : Visibility.Visible;
		_join.Visibility = !asText && _model.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
		if (!asText)
		{
			// What was typed folds back into rows where it can; else the text mode stays
			if (_text.Text.Trim().Length > 0 && !TryRows(_text.Text, _model, out _anyOf))
			{
				_asText.IsChecked = true;
				ShowText(true);
				return;
			}
			BuildRows();
		}
	}

	private void Join(bool anyOf)
	{
		_anyOf = anyOf;
		BuildRows();
		Send(Compose());
	}

	private void BuildRows()
	{
		_all.IsChecked = !_anyOf;
		_any.IsChecked = _anyOf;
		_join.Visibility = _model.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
		_rows.Children.Clear();
		if (_model.Count == 0)
			_rows.Children.Add(new TextBlock { Text = "Always", FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 2f, 0f, 2f) });
		for (var i = 0; i < _model.Count; i++)
			_rows.Children.Add(Row(i));
	}

	// One row: what it compares, how, with what, and a button to take it out
	private Grid Row(int index)
	{
		var (subject, op, value) = _model[index];
		var grid = new Grid { Margin = new Thickness(0f, 0f, 0f, 4f) };
		// The cue and the value share what the comparison and the remove button leave, the cue a little more
		grid.ColumnDefinitions.Add(GridLength.Stars(1.2f));
		grid.ColumnDefinitions.Add(GridLength.Pixels(50f));
		grid.ColumnDefinitions.Add(GridLength.Stars(1f));
		grid.ColumnDefinitions.Add(GridLength.Auto);

		var names = new List<object?> { PassName };
		names.InsertRange(0, _cues.Select(c => (object?)c.Name));
		var what = new ComboBox { ItemsSource = names, SelectedItem = subject, Margin = new Thickness(0f, 0f, 4f, 0f) };
		ToolTipService.SetToolTip(what, "A cue set by the game, or the number of times the section has played in a row");
		what.SelectionChanged += (_, _) =>
		{
			if (what.SelectedItem is string chosen && chosen != _model[index].Subject)
			{
				_model[index] = Fresh(chosen);
				BuildRows();
				Send(Compose());
			}
		};
		grid.Children.Add(what);

		var cue = _cues.FirstOrDefault(c => c.Name == subject);
		var choice = cue is not null && cue.OptionNames.Count > 0;
		var ops = choice ? new List<object?> { "is" } : subject == PassName ? [">", "<", "is"] : [">", "<"];
		var how = new ComboBox { ItemsSource = ops, SelectedItem = op, Margin = new Thickness(0f, 0f, 4f, 0f) };
		ToolTipService.SetToolTip(how, subject == PassName
			? "> means after that pass, < before it, is on that pass only"
			: "> means at or above the value, < at or below it");
		Grid.SetColumn(how, 1);
		how.SelectionChanged += (_, _) =>
		{
			if (how.SelectedItem is string chosen)
				Change(index, op: chosen);
		};
		grid.Children.Add(how);

		FrameworkElement with;
		if (choice)
		{
			var options = new ComboBox { ItemsSource = cue!.OptionNames.Cast<object?>().ToList(), SelectedItem = cue.OptionNames.FirstOrDefault(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase)) };
			options.SelectionChanged += (_, _) =>
			{
				if (options.SelectedItem is string chosen)
					Change(index, value: chosen);
			};
			with = options;
		}
		else
		{
			var pass = subject == PassName;
			var number = new NumberBox
			{
				Minimum = pass ? 1 : -1,
				Maximum = pass ? 99 : 2,
				SmallChange = pass ? 1 : 0.05,
				Value = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0,
				SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
				MinWidth = 76f,
			};
			ToolTipService.SetToolTip(number, pass ? "Pass number: 1 is the first time through" : "Value, usually from 0 to 1");
			number.ValueChanged += (_, _) =>
			{
				if (!double.IsNaN(number.Value))
					Change(index, value: (pass ? Math.Round(number.Value) : Math.Round(number.Value, 3)).ToString(CultureInfo.InvariantCulture));
			};
			with = number;
		}
		Grid.SetColumn(with, 2);
		grid.Children.Add(with);

		var remove = new Button { Content = Small("✕"), Padding = new Thickness(7f, 2f, 7f, 3f), Margin = new Thickness(4f, 0f, 0f, 0f) };
		ToolTipService.SetToolTip(remove, "Delete row");
		remove.Click += (_, _) =>
		{
			_model.RemoveAt(index);
			BuildRows();
			Send(Compose());
		};
		Grid.SetColumn(remove, 3);
		grid.Children.Add(remove);
		return grid;
	}

	private void Change(int index, string? op = null, string? value = null)
	{
		var row = _model[index];
		_model[index] = (row.Subject, op ?? row.Op, value ?? row.Value);
		Send(Compose());
	}

	private void AddRow()
	{
		_model.Add(Fresh(_cues.FirstOrDefault(c => c.OptionNames.Count == 0)?.Name ?? _cues.FirstOrDefault()?.Name ?? PassName));
		if (_asText.IsChecked == true)
		{
			_asText.IsChecked = false;
			ShowText(false);
		}
		BuildRows();
		Send(Compose());
	}

	// A new row for a subject: a level above a half, a choice's first option, a second pass
	private (string Subject, string Op, string Value) Fresh(string subject)
	{
		if (subject == PassName)
			return (subject, "<", "2");
		var cue = _cues.FirstOrDefault(c => c.Name == subject);
		return cue is not null && cue.OptionNames.Count > 0 ? (subject, "is", cue.OptionNames[0]) : (subject, ">", "0.5");
	}

	private string? Compose() => _model.Count == 0
		? null
		: string.Join(_anyOf ? " or " : " and ", _model.Select(r => $"{r.Subject} {r.Op} {r.Value}"));

	private void TakeText()
	{
		var text = _text.Text.Trim();
		Send(text.Length == 0 ? null : text);
	}

	private void Send(string? when)
	{
		var problem = _apply(when);
		_problem.Text = problem ?? "";
		_problem.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
		if (problem is null)
		{
			_showing = true;
			_text.Text = when ?? "";
			_showing = false;
		}
	}

	// Rows for a condition that is comparisons joined all by "and" or all by "or", read as ConditionText writes them
	private static bool TryRows(string when, List<(string Subject, string Op, string Value)> rows, out bool anyOf)
	{
		rows.Clear();
		anyOf = false;
		var text = when.Trim();
		if (text.Contains('(', StringComparison.Ordinal) || text.Contains(" not ", StringComparison.Ordinal) || text.StartsWith("not ", StringComparison.Ordinal))
			return false;
		var hasAnd = text.Contains(" and ", StringComparison.Ordinal);
		var hasOr = text.Contains(" or ", StringComparison.Ordinal);
		if (hasAnd && hasOr)
			return false;
		anyOf = hasOr;
		foreach (var piece in text.Split(anyOf ? " or " : " and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var words = piece.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (words.Length != 3 || words[1] is not (">" or "<" or "is"))
			{
				rows.Clear();
				return false;
			}
			rows.Add((words[0], words[1], words[2]));
		}
		return true;
	}

	private static ToggleButton Segment(string text) => new() { Content = Small(text), Padding = new Thickness(10f, 2f, 10f, 3f), Margin = new Thickness(0f, 0f, 2f, 0f) };

	private static TextBlock Small(string text) => new() { Text = text, FontSize = 12f };
}
