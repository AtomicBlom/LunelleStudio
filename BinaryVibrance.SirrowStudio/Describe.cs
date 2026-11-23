using System.Globalization;

using BinaryVibrance.Sirrow.Synth.Music;

namespace BinaryVibrance.SirrowStudio;

/// <summary>Music in words, for the workbench's labels: a placement's condition, a note's name, a scale's.</summary>
internal static class Describe
{
	private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

	// "when energy above 0.35, level follows calm", or empty when it always plays at its own level
	public static string Placement(Placement placement)
	{
		var parts = new List<string>();
		if (placement.Condition is { } condition)
			parts.Add("when " + condition);
		if (placement.LevelCue is { } level)
			parts.Add($"volume follows {level.Name}");
		if (placement.IsOnPhrase)
			parts.Add("starts on phrase");
		return string.Join(", ", parts);
	}

	// C4 for 60
	public static string NoteName(float midi)
	{
		var note = (int)MathF.Round(midi);
		return $"{NoteNames[((note % 12) + 12) % 12]}{note / 12 - 1}";
	}

	// "E minor", "F# dorian"
	public static string Scale(Scale scale) => $"{NoteNames[(int)scale.Root]} {scale.Name}";
}
