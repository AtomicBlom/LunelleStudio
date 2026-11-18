using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>
/// What the roll does to a part's notation, a step's token at a time, so every bar keeps its steps and the part still
/// parses. A line's note is its token and the holds (<c>-</c>) after it; a beat's or a strum's step one character.
/// </summary>
public static class RollEdits
{
	/// <summary>A beat's step, cycled by a click: a hit, an accent, a ghost note, then nothing.</summary>
	public static string NextHit(string token) => token switch
	{
		"." => "x",
		"x" => "X",
		"X" => "o",
		_ => ".",
	};

	/// <summary>A strum's step, cycled by a click: down, up, a soft down, a soft up, a mute, a held chord, then nothing.</summary>
	public static string NextStroke(string token) => token switch
	{
		"." => "D",
		"D" => "U",
		"U" => "d",
		"d" => "u",
		"u" => "x",
		"x" => "_",
		_ => ".",
	};

	/// <summary>Where the note sounding at <paramref name="step"/> starts, or -1 when none sounds there.</summary>
	public static int NoteAt(NotationText line, int step)
	{
		for (var s = step; s >= 0; s--)
		{
			var token = line[s];
			if (token == ".")
				return -1;
			if (token != "-")
				return NoteToken.Parse(token) is null ? -1 : s;
		}
		return -1;
	}

	/// <summary>How many steps the note starting at <paramref name="start"/> lasts: it and its holds.</summary>
	public static int LengthOf(NotationText line, int start)
	{
		var length = 1;
		while (start + length < line.Count && line[start + length] == "-")
			length++;
		return length;
	}

	/// <summary>
	/// A note placed at <paramref name="step"/> on <paramref name="row"/>, <paramref name="length"/> steps long (to the end
	/// of the part at most); a note it lands in is cut short there, one it covers is gone.
	/// </summary>
	public static void Place(NotationText line, int step, int row, int length = 1)
	{
		Place(line, step, NoteToken.OnRow(row), length);
	}

	/// <inheritdoc cref="Place(NotationText, int, int, int)"/>
	public static void Place(NotationText line, int step, NoteToken note, int length)
	{
		var end = Math.Min(line.Count, step + Math.Max(1, length));
		// What followed the last step covered, if it held a note covered, now starts with nothing
		var after = end < line.Count && line[end] == "-" ? end : -1;
		line[step] = note.ToString();
		for (var s = step + 1; s < end; s++)
			line[s] = "-";
		for (var s = after; s >= 0 && s < line.Count && line[s] == "-"; s++)
			line[s] = ".";
	}

	/// <summary>The note starting at <paramref name="start"/> made to last until <paramref name="last"/> (at least itself).</summary>
	public static void Resize(NotationText line, int start, int last)
	{
		var token = NoteToken.Parse(line[start]) ?? throw new ArgumentException($"No note starts at step {start}.", nameof(start));
		var length = LengthOf(line, start);
		for (var s = start + 1; s < start + length; s++)
			line[s] = ".";
		Place(line, start, token, Math.Max(0, last - start) + 1);
	}

	/// <summary>The note starting at <paramref name="start"/> taken out, its holds with it.</summary>
	public static void Delete(NotationText line, int start)
	{
		var length = LengthOf(line, start);
		for (var s = start; s < start + length; s++)
			line[s] = ".";
	}

	/// <summary>The note starting at <paramref name="start"/> moved to <paramref name="step"/> and <paramref name="row"/>, as long as it was.</summary>
	public static void Move(NotationText line, int start, int step, int row)
	{
		var token = NoteToken.Parse(line[start]) ?? throw new ArgumentException($"No note starts at step {start}.", nameof(start));
		var length = LengthOf(line, start);
		Delete(line, start);
		var moved = NoteToken.OnRow(row) with { Accidental = token.Accidental, Accent = token.Accent, Slide = token.Slide };
		Place(line, Math.Clamp(step, 0, line.Count - 1), moved, length);
	}

	/// <summary>The note starting at <paramref name="start"/> changed by <paramref name="change"/> (an accidental, an accent, a slide).</summary>
	public static void Change(NotationText line, int start, Func<NoteToken, NoteToken> change)
	{
		var token = NoteToken.Parse(line[start]) ?? throw new ArgumentException($"No note starts at step {start}.", nameof(start));
		line[start] = change(token).ToString();
	}
}
