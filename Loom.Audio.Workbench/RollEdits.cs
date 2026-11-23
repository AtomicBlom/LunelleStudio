using BinaryVibrance.Sirrow.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>
/// What the roll does to a part's notation, a step's token at a time, so every bar keeps its steps and the part still
/// parses. A line's note is its token and the holds (<c>-</c>) after it, several notes starting together a chord that
/// shares its holds (<c>1/3/5 - -</c>), each found by its step and row; a beat's or a strum's step one character.
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

	/// <summary>
	/// Where the note sounding at <paramref name="step"/> starts, or -1 when none sounds there; with <paramref name="row"/>,
	/// only when the notes sounding include one on that row (a chord's other notes don't count).
	/// </summary>
	public static int NoteAt(NotationText line, int step, int? row = null)
	{
		for (var s = step; s >= 0; s--)
		{
			var token = line[s];
			if (token == ".")
				return -1;
			if (token == "-")
				continue;
			return NoteToken.ParseStack(token) is { } stack && (row is not { } r || stack.Any(n => n.Row == r)) ? s : -1;
		}
		return -1;
	}

	/// <summary>How many steps the note (or chord) starting at <paramref name="start"/> lasts: it and its holds.</summary>
	public static int LengthOf(NotationText line, int start)
	{
		var length = 1;
		while (start + length < line.Count && line[start + length] == "-")
			length++;
		return length;
	}

	/// <summary>
	/// A note placed at <paramref name="step"/> on <paramref name="row"/>, <paramref name="length"/> steps long (to the end
	/// of the part at most). Where notes already start at that step it joins them as a chord, as long as they are;
	/// otherwise a note it lands in is cut short there, and one it covers is gone.
	/// </summary>
	public static void Place(NotationText line, int step, int row, int length = 1)
	{
		Place(line, step, NoteToken.OnRow(row), length);
	}

	/// <inheritdoc cref="Place(NotationText, int, int, int)"/>
	public static void Place(NotationText line, int step, NoteToken note, int length)
	{
		if (NoteToken.ParseStack(line[step]) is { } stack)
		{
			line[step] = NoteToken.Stack([.. stack, note]);
			return;
		}
		var end = Math.Min(line.Count, step + Math.Max(1, length));
		// What followed the last step covered, if it held a note covered, now starts with nothing
		var after = end < line.Count && line[end] == "-" ? end : -1;
		line[step] = note.ToString();
		for (var s = step + 1; s < end; s++)
			line[s] = "-";
		for (var s = after; s >= 0 && s < line.Count && line[s] == "-"; s++)
			line[s] = ".";
	}

	/// <summary>The note (or chord) starting at <paramref name="start"/> made to last until <paramref name="last"/> (at least itself).</summary>
	public static void Resize(NotationText line, int start, int last)
	{
		var token = line[start];
		if (NoteToken.ParseStack(token) is null)
			throw new ArgumentException($"No note starts at step {start}.", nameof(start));
		var length = LengthOf(line, start);
		for (var s = start; s < start + length; s++)
			line[s] = ".";
		var end = Math.Min(line.Count, start + Math.Max(0, last - start) + 1);
		var after = end < line.Count && line[end] == "-" ? end : -1;
		line[start] = token;
		for (var s = start + 1; s < end; s++)
			line[s] = "-";
		for (var s = after; s >= 0 && s < line.Count && line[s] == "-"; s++)
			line[s] = ".";
	}

	/// <summary>
	/// The note starting at <paramref name="start"/> on <paramref name="row"/> taken out of its chord, or the whole note or
	/// chord with no row; once none is left there, its holds go with it.
	/// </summary>
	public static void Delete(NotationText line, int start, int? row = null)
	{
		if (row is { } r && NoteToken.ParseStack(line[start]) is { } stack && stack.Any(n => n.Row != r))
		{
			line[start] = NoteToken.Stack(stack.Where(n => n.Row != r));
			return;
		}
		var length = LengthOf(line, start);
		for (var s = start; s < start + length; s++)
			line[s] = ".";
	}

	/// <summary>
	/// The note starting at <paramref name="start"/> moved to <paramref name="step"/> and <paramref name="row"/>, as long as
	/// it was. With <paramref name="from"/>, only the note on that row leaves its chord; without, the note or chord moves
	/// whole, every note by as many rows as its lowest.
	/// </summary>
	public static void Move(NotationText line, int start, int step, int row, int? from = null)
	{
		var stack = NoteToken.ParseStack(line[start]) ?? throw new ArgumentException($"No note starts at step {start}.", nameof(start));
		var length = LengthOf(line, start);
		var moving = from is { } f ? stack.Where(n => n.Row == f).ToList() : [.. stack];
		if (moving.Count == 0)
			throw new ArgumentException($"No note starts at step {start} on row {from}.", nameof(from));
		var shift = row - moving.Min(n => n.Row);
		Delete(line, start, from);
		step = Math.Clamp(step, 0, line.Count - 1);
		var first = true;
		foreach (var note in moving)
		{
			var moved = NoteToken.OnRow(note.Row + shift) with { Accidental = note.Accidental, Accent = note.Accent, Slide = note.Slide };
			// The first lands as a note would; the rest join it as its chord
			Place(line, step, moved, first ? length : 1);
			first = false;
		}
	}

	/// <summary>
	/// The note starting at <paramref name="start"/> on <paramref name="row"/> changed by <paramref name="change"/> (an
	/// accidental, an accent, a slide); with no row, every note of its chord.
	/// </summary>
	public static void Change(NotationText line, int start, Func<NoteToken, NoteToken> change, int? row = null)
	{
		var stack = NoteToken.ParseStack(line[start]) ?? throw new ArgumentException($"No note starts at step {start}.", nameof(start));
		line[start] = NoteToken.Stack(stack.Select(n => row is null || n.Row == row ? change(n) : n));
	}

	/// <summary>
	/// A part's steps written on another grid with the same timing: each step of <paramref name="fromTicks"/> as so many of
	/// <paramref name="toTicks"/>, or so many as one. On a finer grid a note runs on through the new steps (a line holds it
	/// with <c>-</c>, a beat or strum lets it ring with <c>.</c>); on a coarser one each note keeps where it starts and fills
	/// its step. Null when a note starts between the coarser grid's steps, or one grid isn't a whole number of the other.
	/// </summary>
	public static List<string>? Regrid(IReadOnlyList<string> steps, int fromTicks, int toTicks, bool characters)
	{
		ArgumentNullException.ThrowIfNull(steps);
		if (fromTicks <= 0 || toTicks <= 0)
			return null;
		var hold = characters ? "." : "-";
		var regridded = new List<string>();
		if (fromTicks >= toTicks)
		{
			if (fromTicks % toTicks != 0)
				return null;
			var each = fromTicks / toTicks;
			foreach (var step in steps)
			{
				regridded.Add(step);
				for (var i = 1; i < each; i++)
					regridded.Add(!characters && step == "." ? "." : hold);
			}
			return regridded;
		}
		if (toTicks % fromTicks != 0)
			return null;
		var group = toTicks / fromTicks;
		if (steps.Count % group != 0)
			return null;
		for (var at = 0; at < steps.Count; at += group)
		{
			for (var i = 1; i < group; i++)
				if (steps[at + i] is not ("." or "-"))
					return null;
			regridded.Add(steps[at]);
		}
		return regridded;
	}

	/// <summary>Steps written as a part's notation: a line's spaced, a beat's or strum's run together a beat at a time, bars split by <c>|</c>.</summary>
	public static string Write(IReadOnlyList<string> steps, int perBar, int perBeat, bool characters)
	{
		ArgumentNullException.ThrowIfNull(steps);
		var bars = new List<string>();
		for (var at = 0; at < steps.Count; at += perBar)
		{
			var bar = steps.Skip(at).Take(perBar).ToList();
			bars.Add(characters
				? string.Join(" ", bar.Chunk(Math.Max(1, perBeat)).Select(beat => string.Concat(beat)))
				: string.Join(" ", bar));
		}
		return string.Join(" | ", bars);
	}
}
