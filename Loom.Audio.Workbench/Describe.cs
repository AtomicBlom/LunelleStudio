using System.Globalization;

using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>Music in words, for the workbench's labels: a placement's condition, a note's name, a scale's.</summary>
internal static class Describe
{
	private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

	// "when energy above 0.35, level follows calm", or empty when it always plays at its own level
	public static string Placement(Placement placement)
	{
		var parts = new List<string>();
		if (placement.Condition is { } condition)
			parts.Add("when " + Condition(condition));
		if (placement.LevelCue is { } level)
			parts.Add($"level follows {level.Name}");
		if (placement.OnPhraseValue)
			parts.Add("on its phrase");
		return string.Join(", ", parts);
	}

	public static string Condition(Condition condition) => condition switch
	{
		AboveCondition above => $"{above.Cue.Name} above {Number(above.Level)}",
		BelowCondition below => $"{below.Cue.Name} below {Number(below.Level)}",
		IsCondition @is => $"{@is.Cue.Name} is {Option(@is.Cue, @is.Value)}",
		AndCondition and => $"{Condition(and.A)} and {Condition(and.B)}",
		OrCondition or => $"({Condition(or.A)} or {Condition(or.B)})",
		NotCondition not => $"not {Condition(not.A)}",
		_ => "?",
	};

	private static string Option(CueBase cue, float value)
	{
		var names = cue.OptionNames;
		for (var i = 0; i < names.Length; i++)
			if (cue.OptionValue(i) == value)
				return names[i];
		return Number(value);
	}

	private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

	// C4 for 60
	public static string NoteName(float midi)
	{
		var note = (int)MathF.Round(midi);
		return $"{NoteNames[((note % 12) + 12) % 12]}{note / 12 - 1}";
	}

	// "E minor", "F# dorian"
	public static string Scale(Scale scale) => $"{NoteNames[(int)scale.Root]} {scale.Name}";
}
