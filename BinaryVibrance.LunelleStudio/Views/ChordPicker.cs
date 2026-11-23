using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// One bar of a section's chords, picked: the bar's chords (one, or two or four sharing it) to choose between and split it
/// into, then the chosen one's degree, flat or sharp, and kind, and a bar more or fewer in the progression. Each pick is
/// written back as the chords text at once, through the apply function it was made with; a chord made here is spelled as
/// the song's are, its numeral lower case where the scale's triad on it is minor.
/// </summary>
internal sealed class ChordPicker : ContentControl
{
	private static readonly string[] Numerals = ["I", "II", "III", "IV", "V", "VI", "VII"];
	private static readonly (ChordKind Kind, string Suffix, string Name)[] Kinds =
	[
		(ChordKind.Triad, "", "diatonic"), (ChordKind.Major, "M", "major"), (ChordKind.Minor, "m", "minor"),
		(ChordKind.Power, "5", "power"), (ChordKind.Sus2, "sus2", "sus2"), (ChordKind.Sus4, "sus4", "sus4"),
		(ChordKind.Seventh, "7", "seventh"), (ChordKind.Diminished, "dim", "diminished"),
	];

	private readonly List<List<string>> _bars;
	private readonly int _bar;
	private readonly Scale _scale;
	private readonly Func<string, string?> _apply;
	private readonly StackPanel _column = new() { Orientation = Orientation.Vertical, MinWidth = 320f };
	private int _slot;
	private string? _problem;

	/// <param name="bars">The progression's bars as written.</param>
	/// <param name="bar">Which bar of the progression is picked.</param>
	/// <param name="scale">The scale the song plays in, which spells a new chord's numeral.</param>
	/// <param name="apply">Takes the progression as chords text; gives why it can't be taken, or null.</param>
	public ChordPicker(IReadOnlyList<ChordBar> bars, int bar, Scale scale, Func<string, string?> apply)
	{
		_bars = [.. bars.Select(b => b.Written.ToList())];
		_bar = Math.Clamp(bar, 0, _bars.Count - 1);
		_scale = scale;
		_apply = apply;
		IsTabStop = false;
		Content = _column;
		Build();
	}

	private List<string> Bar => _bars[_bar];

	private void Build()
	{
		_column.Children.Clear();
		_column.Children.Add(Label($"Bar {_bar + 1}"));

		var slots = Row();
		for (var i = 0; i < Bar.Count; i++)
		{
			var slot = i;
			var button = Toggle(Bar[i], slot == _slot, $"Edit chord {slot + 1} in this bar");
			button.Click += (_, _) =>
			{
				_slot = slot;
				Build();
			};
			slots.Children.Add(button);
		}
		slots.Children.Add(new Border { Width = 14f });
		foreach (var count in new[] { 1, 2, 4 })
		{
			var split = Toggle(count == 1 ? "whole" : $"÷{count}", Bar.Count == count, count == 1 ? "One chord for the whole bar" : $"Split the bar into {count} chords");
			split.Click += (_, _) => Split(count);
			slots.Children.Add(split);
		}
		_column.Children.Add(slots);

		var chord = Read(Bar[_slot]);
		_column.Children.Add(Label("Degree"));
		var degrees = Row();
		for (var d = 1; d <= 7; d++)
		{
			var degree = d;
			var button = Toggle(Spell(new Chord(d, chord.Kind, chord.Accidental)), chord.Degree == d, $"Chord on scale degree {Numerals[d - 1]}");
			button.Click += (_, _) => Set(chord with { Degree = degree });
			degrees.Children.Add(button);
		}
		_column.Children.Add(degrees);

		var accidentals = Row();
		foreach (var (shift, text, tip) in new[] { (-1, "flat", "Lower the root a semitone"), (0, "natural", "Root on the scale"), (1, "sharp", "Raise the root a semitone") })
		{
			var button = Toggle(text, chord.Accidental == shift, tip);
			button.Click += (_, _) => Set(chord with { Accidental = shift });
			accidentals.Children.Add(button);
		}
		_column.Children.Add(accidentals);

		_column.Children.Add(Label("Kind"));
		var kinds = Row();
		foreach (var (kind, _, name) in Kinds)
		{
			var button = Toggle(name, chord.Kind == kind, kind == ChordKind.Triad ? "Diatonic: major, minor or diminished, as the scale gives on this degree" : $"{char.ToUpperInvariant(name[0])}{name[1..]} chord, regardless of the scale");
			button.Click += (_, _) => Set(chord with { Kind = kind });
			kinds.Children.Add(button);
		}
		_column.Children.Add(kinds);

		var progression = Row();
		var add = new Button { Content = Text("+ Add bar"), Padding = new Thickness(8f, 2f, 8f, 3f), Margin = new Thickness(0f, 0f, 4f, 0f) };
		ToolTipService.SetToolTip(add, "Add a bar after this one with the same chords");
		add.Click += (_, _) =>
		{
			_bars.Insert(_bar + 1, [.. Bar]);
			Send();
		};
		progression.Children.Add(add);
		var remove = new Button { Content = Text("Delete bar"), Padding = new Thickness(8f, 2f, 8f, 3f), IsEnabled = _bars.Count > 1 };
		ToolTipService.SetToolTip(remove, "Delete this bar from the progression");
		remove.Click += (_, _) =>
		{
			_bars.RemoveAt(_bar);
			Send();
		};
		progression.Children.Add(remove);
		progression.Margin = new Thickness(0f, 10f, 0f, 0f);
		_column.Children.Add(progression);

		if (_problem is not null)
			_column.Children.Add(new TextBlock { Text = _problem, FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0f, 6f, 0f, 0f) });
	}

	private void Split(int count)
	{
		var tokens = Bar;
		var first = tokens[0];
		var made = new List<string>(count);
		for (var i = 0; i < count; i++)
			made.Add(i < tokens.Count ? tokens[i] : tokens[^1]);
		if (count == 1)
			made[0] = first;
		_bars[_bar] = made;
		_slot = Math.Min(_slot, count - 1);
		Send();
	}

	private void Set(Chord chord)
	{
		Bar[_slot] = Spell(chord);
		Send();
	}

	private void Send()
	{
		_problem = _apply(string.Join(" | ", _bars.Select(bar => string.Join(" ", bar))));
		Build();
	}

	// A chord as the song's chords are written: accidentals, the numeral lower case where its third is minor, its suffix
	private string Spell(Chord chord)
	{
		var numeral = Numerals[chord.Degree - 1];
		var minor = chord.Kind switch
		{
			ChordKind.Minor or ChordKind.Diminished => true,
			ChordKind.Major or ChordKind.Power or ChordKind.Sus2 or ChordKind.Sus4 => false,
			_ => new Chord(chord.Degree, ChordKind.Triad, chord.Accidental).Intervals(_scale) is [_, 3, ..],
		};
		var accidentals = chord.Accidental < 0 ? new string('b', -chord.Accidental) : new string('#', chord.Accidental);
		var suffix = Kinds.First(k => k.Kind == chord.Kind).Suffix;
		return accidentals + (minor ? numeral.ToLowerInvariant() : numeral) + suffix;
	}

	// A bar's token as a chord: the tokens came from a section that read them, so one that doesn't read throws rather than
	// showing (and on the next pick writing back) a chord it isn't
	private static Chord Read(string token) => Chord.Parse(token);

	private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 0f, 0f, 6f) };

	private static TextBlock Label(string text) => new() { Text = text.ToUpperInvariant(), FontSize = 10f, Foreground = Theme.Faint, Margin = new Thickness(0f, 2f, 0f, 4f) };

	private static TextBlock Text(string text) => new() { Text = text, FontSize = 12f };

	private static ToggleButton Toggle(string text, bool on, string tip)
	{
		var button = new ToggleButton { Content = Text(text), IsChecked = on, Padding = new Thickness(8f, 2f, 8f, 3f), Margin = new Thickness(0f, 0f, 3f, 0f) };
		ToolTipService.SetToolTip(button, tip);
		return button;
	}
}
