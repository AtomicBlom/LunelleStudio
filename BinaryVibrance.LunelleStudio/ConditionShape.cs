using System.Globalization;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>How a level cue limits a part: not at all, from a level up, up to a level, or between two.</summary>
internal enum LevelRule
{
	Any,
	Above,
	Below,
	Between,
}

/// <summary>Which of its section's passes in a row a part plays on.</summary>
internal enum PassRule
{
	Every,
	First,
	FromSecond,
	Only,
}

/// <summary>A level cue's clause: its rule, the level it starts at (<see cref="From"/>) and the one it stops at (<see cref="To"/>).</summary>
internal sealed class LevelClause(Cue cue)
{
	public Cue Cue { get; } = cue;
	public LevelRule Rule { get; set; }
	public float From { get; set; } = 0.5f;
	public float To { get; set; } = 0.8f;
}

/// <summary>A choice cue's clause: the options it plays on, by value, or with <see cref="Except"/> every option but those.</summary>
internal sealed class ChoiceClause(CueBase cue)
{
	public CueBase Cue { get; } = cue;
	public List<float> Values { get; } = [];
	public bool Except { get; set; }
}

/// <summary>
/// A condition as the plays-when editor holds it: a clause per cue, all of which must hold (a range for a level cue, a set
/// of options for a choice cue), and which passes of the section it plays on. <see cref="Read"/> takes a condition apart
/// into one where it can; one it can't (an <c>or</c> across cues, say) stays text. <see cref="Write"/> puts it back
/// together as a song file writes it.
/// </summary>
internal sealed class ConditionShape
{
	private ConditionShape(IReadOnlyList<CueBase> cues)
	{
		foreach (var cue in cues)
		{
			if (cue is Cue level)
				Levels.Add(new LevelClause(level));
			else if (cue.OptionNames.Count > 0)
				Choices.Add(new ChoiceClause(cue));
		}
	}

	/// <summary>A clause for each of the project's level cues, in the catalog's order.</summary>
	public List<LevelClause> Levels { get; } = [];

	/// <summary>A clause for each of the project's choice cues, in the catalog's order.</summary>
	public List<ChoiceClause> Choices { get; } = [];

	/// <summary>Which passes it plays on.</summary>
	public PassRule Pass { get; set; }

	/// <summary>The pass it plays on alone, for <see cref="PassRule.Only"/>.</summary>
	public int OnlyPass { get; set; } = 1;

	/// <summary>An empty one, which always holds.</summary>
	public static ConditionShape Always(IReadOnlyList<CueBase> cues) => new(cues);

	/// <summary>
	/// The shape of <paramref name="condition"/> (null, always) over <paramref name="cues"/>, or null when it has no shape:
	/// anything but comparisons, a choice's options joined by <c>or</c> (or <c>not</c> of them) and a pass, all joined by
	/// <c>and</c>, each cue named once.
	/// </summary>
	public static ConditionShape? Read(Condition? condition, IReadOnlyList<CueBase> cues)
	{
		var shape = new ConditionShape(cues);
		if (condition is null)
			return shape;
		var passSet = false;
		var choicesSet = new HashSet<CueBase>();
		foreach (var term in Terms(condition, and: true))
		{
			switch (term)
			{
				case AboveCondition above when shape.Level(above.Cue) is { } clause:
					if (clause.Rule == LevelRule.Any)
						(clause.Rule, clause.From) = (LevelRule.Above, above.Level);
					else if (clause.Rule == LevelRule.Below && above.Level < clause.To)
						(clause.Rule, clause.From) = (LevelRule.Between, above.Level);
					else
						return null;
					break;
				case BelowCondition below when shape.Level(below.Cue) is { } clause:
					if (clause.Rule == LevelRule.Any)
						(clause.Rule, clause.To) = (LevelRule.Below, below.Level);
					else if (clause.Rule == LevelRule.Above && below.Level > clause.From)
						(clause.Rule, clause.To) = (LevelRule.Between, below.Level);
					else
						return null;
					break;
				case PassCondition pass when !passSet:
					passSet = true;
					switch (pass)
					{
						case { Comparison: PassComparison.Below, Count: 2 }:
							shape.Pass = PassRule.First;
							break;
						case { Comparison: PassComparison.Above, Count: 1 }:
							shape.Pass = PassRule.FromSecond;
							break;
						case { Comparison: PassComparison.Is, Count: >= 1 }:
							(shape.Pass, shape.OnlyPass) = (PassRule.Only, pass.Count);
							break;
						default:
							return null;
					}
					break;
				default:
					var except = term is NotCondition;
					var options = Terms(term is NotCondition not ? not.A : term, and: false).ToList();
					if (options.Count == 0 || options.Any(o => o is not IsCondition)
						|| options.Cast<IsCondition>().Select(o => o.Cue).Distinct().Count() != 1)
						return null;
					var cue = ((IsCondition)options[0]).Cue;
					if (shape.Choice(cue) is not { } choice || !choicesSet.Add(cue))
						return null;
					choice.Except = except;
					foreach (var option in options.Cast<IsCondition>())
						if (!choice.Values.Contains(option.Value))
							choice.Values.Add(option.Value);
					choice.Values.Sort();
					break;
			}
		}
		return shape;
	}

	// The terms joined by and (or by or), however they are bracketed
	private static IEnumerable<Condition> Terms(Condition condition, bool and) => condition switch
	{
		AndCondition both when and => [.. Terms(both.A, and), .. Terms(both.B, and)],
		OrCondition either when !and => [.. Terms(either.A, and), .. Terms(either.B, and)],
		_ => [condition],
	};

	/// <summary>The clause for a level cue, or null when the project hasn't it.</summary>
	public LevelClause? Level(CueBase cue) => Levels.FirstOrDefault(l => l.Cue == cue);

	/// <summary>The clause for a choice cue, or null when the project hasn't it.</summary>
	public ChoiceClause? Choice(CueBase cue) => Choices.FirstOrDefault(c => c.Cue == cue);

	/// <summary>Whether every clause lets the part play anywhere: the condition is "always".</summary>
	public bool IsAlways => Clauses().Count == 0;

	/// <summary>The clause it shows first in a lane: its first level, else its first choice, else its pass; null when always.</summary>
	public object? First => Clauses().FirstOrDefault();

	/// <summary>How many clauses limit it.</summary>
	public int Count => Clauses().Count;

	// The clauses that limit it, in the order it writes them: levels, choices, the pass
	private List<object> Clauses()
	{
		var clauses = new List<object>();
		clauses.AddRange(Levels.Where(l => l.Rule != LevelRule.Any));
		clauses.AddRange(Choices.Where(c => c.Values.Count > 0));
		if (Pass != PassRule.Every)
			clauses.Add(Pass);
		return clauses;
	}

	/// <summary>It as a condition, or null for always.</summary>
	public Condition? ToCondition()
	{
		Condition? all = null;
		void And(Condition c) => all = all is null ? c : all & c;
		foreach (var level in Levels)
		{
			if (level.Rule is LevelRule.Above or LevelRule.Between)
				And(new AboveCondition(level.Cue, Round(level.From)));
			if (level.Rule is LevelRule.Below or LevelRule.Between)
				And(new BelowCondition(level.Cue, Round(level.To)));
		}
		foreach (var choice in Choices.Where(c => c.Values.Count > 0))
		{
			Condition any = new IsCondition(choice.Cue, choice.Values[0]);
			foreach (var value in choice.Values.Skip(1))
				any |= new IsCondition(choice.Cue, value);
			And(choice.Except ? !any : any);
		}
		switch (Pass)
		{
			case PassRule.First:
				And(new PassCondition(PassComparison.Below, 2));
				break;
			case PassRule.FromSecond:
				And(new PassCondition(PassComparison.Above, 1));
				break;
			case PassRule.Only:
				And(new PassCondition(PassComparison.Is, Math.Max(1, OnlyPass)));
				break;
		}
		return all;
	}

	/// <summary>It as a song file writes it, or null for always.</summary>
	public string? Write() => ToCondition() is { } condition ? ConditionText.Write(condition) : null;

	/// <summary>What it means, in a sentence: "Plays when intensity is at or above 0.45 and biome is Mountains or Plateau."</summary>
	public string Sentence()
	{
		var said = Clauses().Select(Say).ToList();
		return said.Count == 0 ? "Plays always." : $"Plays when {Join(said, "and")}.";
	}

	/// <summary>Whether it holds for the cues as <paramref name="value"/> gives them on pass <paramref name="pass"/>, and why: the first clause that doesn't hold, or every one that does.</summary>
	public (bool Met, string Why) Now(Func<CueBase, float> value, int pass)
	{
		var clauses = Clauses();
		if (clauses.Count == 0)
			return (true, "it always plays");
		var held = new List<string>();
		foreach (var clause in clauses)
		{
			var (met, why) = Check(clause, value, pass);
			if (!met)
				return (false, why);
			held.Add(why);
		}
		return (true, string.Join(", ", held));
	}

	private (bool Met, string Why) Check(object clause, Func<CueBase, float> value, int pass)
	{
		switch (clause)
		{
			case LevelClause level:
				{
					var now = value(level.Cue);
					var at = $"{level.Cue.Name} {Number(now)}";
					var low = level.Rule is LevelRule.Above or LevelRule.Between;
					var high = level.Rule is LevelRule.Below or LevelRule.Between;
					if (low && now < level.From)
						return (false, $"{at} is under {Number(level.From)}");
					if (high && now > level.To)
						return (false, $"{at} is over {Number(level.To)}");
					return (true, level.Rule switch
					{
						LevelRule.Above => $"{at} is at or above {Number(level.From)}",
						LevelRule.Below => $"{at} is at or under {Number(level.To)}",
						_ => $"{at} is between {Number(level.From)} and {Number(level.To)}",
					});
				}
			case ChoiceClause choice:
				{
					var now = value(choice.Cue);
					var name = OptionName(choice.Cue, now);
					var has = choice.Values.Contains(now);
					if (has != choice.Except)
						return (true, $"{choice.Cue.Name} is {name}");
					return (false, choice.Except
						? $"{choice.Cue.Name} is {name}, which is left out"
						: $"{choice.Cue.Name} is {name}, not {Join(Options(choice), "or")}");
				}
			default:
				{
					var rule = (PassRule)clause;
					var met = rule switch { PassRule.First => pass < 2, PassRule.FromSecond => pass > 1, _ => pass == OnlyPass };
					return (met, $"this is pass {pass}");
				}
		}
	}

	private string Say(object clause) => clause switch
	{
		LevelClause { Rule: LevelRule.Above } l => $"{l.Cue.Name} is at or above {Number(l.From)}",
		LevelClause { Rule: LevelRule.Below } l => $"{l.Cue.Name} is at or under {Number(l.To)}",
		LevelClause l => $"{l.Cue.Name} is between {Number(l.From)} and {Number(l.To)}",
		ChoiceClause { Except: true } c => $"{c.Cue.Name} is anything but {Join(Options(c), "or")}",
		ChoiceClause c => $"{c.Cue.Name} is {Join(Options(c), "or")}",
		PassRule.First => "on its first pass",
		PassRule.FromSecond => "from its second pass",
		_ => $"on pass {OnlyPass} alone",
	};

	/// <summary>A choice's options by name, in the cue's order.</summary>
	public static List<string> Options(ChoiceClause choice) => [.. choice.Values.Select(v => OptionName(choice.Cue, v))];

	/// <summary>The name of a choice cue's option with value <paramref name="value"/>, or the number where none has it.</summary>
	public static string OptionName(CueBase cue, float value)
	{
		for (var i = 0; i < cue.OptionNames.Count; i++)
			if (cue.OptionValue(i) == value)
				return cue.OptionNames[i];
		return Number(value);
	}

	/// <summary>A level as the editor shows it: 0.45, 0.5, 1.</summary>
	public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

	/// <summary>A level as a lane's chip shows it, short: .45, .5, 1.</summary>
	public static string Short(float value)
	{
		var text = Number(value);
		return text.StartsWith("0.", StringComparison.Ordinal) ? text[1..] : text;
	}

	// Levels kept to hundredths, as the editor sets them
	private static float Round(float value) => MathF.Round(value * 100f) / 100f;

	private static string Join(IReadOnlyList<string> words, string last) => words.Count switch
	{
		0 => "",
		1 => words[0],
		_ => $"{string.Join(", ", words.Take(words.Count - 1))} {last} {words[^1]}",
	};
}
