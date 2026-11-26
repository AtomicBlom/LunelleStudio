using System.Text.Json.Nodes;

using BinaryVibrance.Lunelle.Format;

namespace BinaryVibrance.LunelleStudio;

// Patterns (docs/spec/ui-patterns.md, Lunelle's patterns.md): a part's notes extracted into a pattern of the project's,
// a pattern a part plays detached back into notes of its own, and a pattern from the library put into the part chosen.
// Each is one undoable step, and the part sounds the same before and after an extract or a detach
public sealed partial class Workbench
{
	// What a part's content is: its notation's keys, and the options a pattern carries
	private static readonly string[] ContentKeys = ["line", "beat", "strum", "notes", "rows", "sequence", "overChords", "octave", "voicing", "spread"];

	/// <summary>
	/// Makes a pattern of the part chosen (its notes, its step and its notation's options) at
	/// <c>Patterns/&lt;id&gt;.pattern.json</c>, and has the part play it in their place: its track, condition and placement
	/// stay. One undoable step. Why it can't be, or null.
	/// </summary>
	public string? ExtractSelected(string id)
	{
		id = id.Trim();
		if (_catalog.Root is not { } root)
			return "This project has no folder for patterns.";
		if (Chosen() is not { } chosen || _editor!.Part(chosen.Section, chosen.Index) is not { } part)
			return "Choose a part with notes of its own.";
		if (part["pattern"] is not null)
			return "This part plays a pattern already.";
		var kind = part["beat"] is not null ? "beat" : part["line"] is not null ? "line" : part["strum"] is not null ? "strum" : null;
		if (kind is null)
			return "This part's notes are a sequence of parts, which a pattern of one kind can't hold: make each its own pattern.";
		if (EntryIds.IdProblem(id) is { } bad)
			return $"{bad}: use lowercase letters, digits and hyphens, a '/' before a folder's name (drums/groove).";
		var path = Path.Combine(root, "Patterns", id.Replace('/', Path.DirectorySeparatorChar) + ".pattern.json");
		if (Files.Exists(path))
			return $"There is a pattern {id} already.";
		var pattern = new JObject()
			.Set("format", new JString("lunelle-pattern/1"))
			.Set("kind", new JString(kind))
			.Set("step", new JString(part[kind]!.GetValue<string>()));
		var meter = _editor.Text is { } text && JsonNode.Parse(text)?["meter"]?.GetValue<string>() is { } m ? m : null;
		if (meter is not null)
			pattern.Set("meter", new JString(meter));
		foreach (var key in new[] { "rows", "notes", "overChords", "octave", "voicing", "spread" })
			if (part[key] is { } value)
				pattern.Set(key, JReader.Parse(value.ToJsonString()));
		using (Document?.Transaction($"extract pattern {id}"))
		{
			Files.WriteText(path, JWriter.Write(pattern) + "\n");
			Seal();
		_editor.Edit(song =>
			{
				var edited = (JsonObject)song["sections"]![chosen.Section]!["parts"]![chosen.Index]!;
				foreach (var key in ContentKeys)
					edited.Remove(key);
				edited["pattern"] = id;
			});
			_editor.Save();
		}
		ViewModel.BuildText = $"Made the pattern {id}. The part plays it once the project is read again.";
		return null;
	}

	/// <summary>Extracts the part chosen as a pattern named for its track and section, the first such name free.</summary>
	public string? ExtractSelectedNamed()
	{
		if (_selected is not { } selected || FindPlacement(selected) is not { } placement || _catalog.Root is not { } root)
			return ExtractSelected("");
		string Slug(string text) => new([.. text.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);
		var stem = $"parts/{Slug(placement.Track.Name)}-{Slug(placement.Section.Name)}".Replace("--", "-", StringComparison.Ordinal).TrimEnd('-');
		var id = stem;
		for (var n = 2; Files.Exists(Path.Combine(root, "Patterns", id.Replace('/', Path.DirectorySeparatorChar) + ".pattern.json")); n++)
			id = $"{stem}-{n}";
		return ExtractSelected(id);
	}

	/// <summary>
	/// Writes the pattern the part chosen plays into the part, with its transforms (patterns.md §5), so its notes are its
	/// own to edit. One undoable step. Why it can't be, or null.
	/// </summary>
	public string? DetachSelected()
	{
		if (Chosen() is not { } chosen || _editor!.Part(chosen.Section, chosen.Index) is not { } part || part["pattern"] is null)
			return "Choose a part that plays a pattern.";
		if (_catalog.ResolveSong is not { } resolve || _catalog.Songs.Count <= _songIndex)
			return "This project can't resolve its songs here.";
		var (json, problem) = resolve(_catalog.Songs[_songIndex].Name, _editor.Text);
		if (problem is not null || json is null)
			return problem ?? "The song couldn't be resolved.";
		var resolvedParts = json["sections"]?.Items.ElementAtOrDefault(chosen.Section)?["parts"]?.Items;
		var rawParts = (JsonNode.Parse(_editor.Text)?["sections"]?[chosen.Section]?["parts"] as JsonArray)?.Count;
		if (resolvedParts is null || resolvedParts.Count != rawParts || resolvedParts[chosen.Index] is not JObject written)
			return "The part's pattern can't be written in: another part in its section can't be played.";
		var content = JsonNode.Parse(written.ToString())!.AsObject();
		Seal();
		_editor.Edit(song =>
		{
			var parts = (JsonArray)song["sections"]![chosen.Section]!["parts"]!;
			parts[chosen.Index] = content.DeepClone();
		});
		_editor.Save();
		Seal();
		Rebuild(swap: true);
		return null;
	}

	/// <summary>
	/// Has the part chosen play pattern <paramref name="reference"/> (<c>drums/groove</c>, <c>lunelle:…</c>) in place of
	/// its notes: its track, condition and placement stay. One undoable step. Why it can't be, or null.
	/// </summary>
	public string? UsePattern(string reference)
	{
		if (Chosen() is not { } chosen || _editor!.RawPart(chosen.Section, chosen.Index) is not { } part || part["code"] is not null)
			return "Choose a part, in a lane, to play the pattern.";
		Seal();
		_editor.Edit(song =>
		{
			var edited = (JsonObject)song["sections"]![chosen.Section]!["parts"]![chosen.Index]!;
			foreach (var key in ContentKeys.Append("part"))
				edited.Remove(key);
			edited["pattern"] = reference;
		});
		_editor.Save();
		Seal();
		Rebuild(swap: true);
		return null;
	}

	/// <summary>
	/// What kind of music a pattern of the project's is: <c>"beat"</c>, <c>"line"</c>, <c>"strum"</c> or
	/// <c>"sequence"</c>; null for one that can't be read.
	/// </summary>
	public string? PatternKind(string id)
	{
		if (_catalog.Root is not { } root)
			return null;
		try
		{
			var path = Path.Combine(root, "Patterns", id.Replace('/', Path.DirectorySeparatorChar) + ".pattern.json");
			if (!Files.Exists(path) || JReader.Parse(Files.ReadText(path)) is not JObject json)
				return null;
			return json["sequence"] is not null ? "sequence" : json["kind"]?.Str;
		}
		catch (Exception ex) when (ex is FormatError or IOException)
		{
			return null;
		}
	}

	/// <summary>
	/// Why pattern <paramref name="id"/> wouldn't fit a part of track <paramref name="track"/> (a beat over a patch, a
	/// line or a strum over a kit, patterns.md §6), or null where it would, or where its kind can't be told.
	/// </summary>
	public string? PatternMisfit(string id, string track)
	{
		if (_song?.Tracks.FirstOrDefault(t => t.Name == track) is not { } playing)
			return null;
		var isKit = playing.Kit is not null;
		return PatternKind(id) switch
		{
			"beat" when !isKit => $"{id} is a beat, for a kit's track; {track} plays a patch.",
			"line" or "strum" when isKit => $"{id} is a {PatternKind(id)}, for a patch's track; {track} plays a kit.",
			_ => null,
		};
	}

	/// <summary>
	/// A pattern dropped on Arrange (docs/spec/ui-patterns.md): onto <paramref name="onto"/>, a part of track
	/// <paramref name="track"/> in section <paramref name="section"/>, it plays the pattern in place of its notes; onto the
	/// lane's empty ground, a new part of the track plays it, after the track's others there. With
	/// <paramref name="inline"/> the pattern's content is written into the part instead, its own to edit. One undoable
	/// step. Why it can't be, or null.
	/// </summary>
	public string? DropPattern(string id, string track, int section, PlacementViewModel? onto, bool inline)
	{
		if (_editor is null)
			return "This song is code: drop a pattern on a song with a file.";
		if (PatternMisfit(id, track) is { } misfit)
			return misfit;
		using (Document?.Transaction(inline ? $"copy pattern {id}" : $"use pattern {id}"))
		{
			if (onto is not null)
			{
				SelectPlacement(onto);
				if (UsePattern(id) is { } why)
					return why;
			}
			else
			{
				Seal();
				AddPart(section, track, content: new JsonObject { ["track"] = track, ["pattern"] = id });
				_editor.Save();
				Seal();
			}
			return inline ? DetachSelected() : null;
		}
	}

	// The library's patterns dimmed where they wouldn't fit the part chosen, of the track named
	private void ShowPatternFits(string track)
	{
		foreach (var pattern in ViewModel.LibraryPatterns)
			pattern.Misfit = PatternMisfit(pattern.Id, track);
	}

	/// <summary>How many parts, in all the project's songs, play pattern <paramref name="id"/>.</summary>
	public int PatternUses(string id)
	{
		if (_catalog.Root is not { } root)
			return 0;
		var count = 0;
		foreach (var song in Files.List(Path.Combine(root, "Songs")).Where(f => f.EndsWith(".song.json", StringComparison.Ordinal)))
		{
			try
			{
				if (JReader.Parse(Files.ReadText(Path.Combine(root, "Songs", song))) is not JObject json)
					continue;
				foreach (var section in json["sections"]?.Items ?? [])
					foreach (var part in section["parts"]?.Items ?? [])
						count += (part["pattern"]?.Str == id ? 1 : 0) + (part["endWith"]?["pattern"]?.Str == id ? 1 : 0);
			}
			catch (Exception ex) when (ex is FormatError or IOException)
			{
			}
		}
		return count;
	}

	// The part chosen, by its section and its index in the file, while its song has a file
	private (int Section, int Index)? Chosen() =>
		_editor is not null && _selected is { } selected && FindPlacement(selected) is { } placement
			? (placement.Section.Index, IndexIn(placement))
			: null;

	// The roll, for a part that plays a pattern: which, and how many play it; or that a part of notes can be extracted
	private bool ShowPatternInRoll(JsonObject part, string label)
	{
		var roll = ViewModel.Roll;
		if (part["pattern"]?.GetValue<string>() is not { } pattern)
		{
			roll.ShowPattern(null, canExtract: _catalog.Root is not null && part["sequence"] is null);
			return false;
		}
		var uses = PatternUses(pattern);
		roll.ShowNone($"This part plays the pattern {pattern}{(uses > 1 ? $", which {uses} parts play" : "")}. Detach it to edit its notes here; edit the pattern's file to change them for every part.", label);
		roll.ShowPattern(pattern, canExtract: false);
		roll.ShowCode(false, false, "");
		return true;
	}
}
