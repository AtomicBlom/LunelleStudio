using System.Text.Json.Nodes;
using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// Arrange's edits of the song's file, each made through the editor (so it can be undone, and is saved a moment later),
/// then the song built again and heard at the next bar: a section's chords, bars, name and exits; a part's condition,
/// level cue, phrase and fill; a track added, renamed, given another instrument or taken out; a part added where a track
/// plays nothing. Each refuses quietly where the song has no file, and says why where a value can't be taken.
/// </summary>
public sealed partial class Workbench
{
	/// <summary>Whether the song chosen can be edited here: it has a file, and it was read.</summary>
	public bool CanEditSong => _editor is not null;

	/// <summary>The cues a condition can name, as the project has them: what a condition editor offers.</summary>
	public IReadOnlyList<CueBase> ConditionCues => _catalog.Cues;

	// The lists in a section of a song's file whose entries name a track: a rename or a delete of the track reaches each
	private static readonly string[] TrackLists = ["parts", "sweeps"];

	/// <summary>The catalog's patches and kits by name: what a track can play.</summary>
	public IReadOnlyList<(string Name, bool IsKit)> Instruments =>
		[.. _catalog.Patches.Select(p => (p.Name, false)), .. _catalog.Kits.Select(k => (k.Name, true))];

	// An edit of one section in the song's file, then the song built again
	private void EditSection(int section, Action<JsonObject> change)
	{
		if (_editor is null)
			return;
		_editor.Edit(root =>
		{
			if (root["sections"]?[section] is JsonObject found)
				change(found);
		});
		Rebuild(swap: true);
	}

	// An edit of the whole file, then the song built again
	private void EditSong(Action<JsonObject> change)
	{
		if (_editor is null)
			return;
		_editor.Edit(change);
		Rebuild(swap: true);
		// A track given another instrument, added or deleted changes which songs the designer's patch is used in
		if ((uint)_benchPatch < (uint)_catalog.Patches.Count)
			ShowPatchUsage(_catalog.Patches[_benchPatch].Name);
	}

	/// <summary>Sets a section's chords, checked first: the reason they can't be read, or null where they were taken.</summary>
	internal string? SetChords(int section, string chords)
	{
		try
		{
			new Song("check").Section("check", 1, chords);
		}
		catch (NotationException ex)
		{
			return ex.Message;
		}
		EditSection(section, s => s["chords"] = chords);
		return null;
	}

	/// <summary>Sets how many bars a section is.</summary>
	internal void SetBars(int section, int bars)
	{
		if (bars >= 1)
			EditSection(section, s => s["bars"] = bars);
	}

	/// <summary>Renames a section, and every exit that goes to it; the reason it can't be, or null.</summary>
	internal string? RenameSection(int section, string name)
	{
		name = name.Trim();
		if (name.Length == 0)
			return "Enter a name for the section.";
		if (_song?.Sections.Any(s => s.Index != section && s.Name == name) == true)
			return $"A section named '{name}' already exists.";
		EditSong(root =>
		{
			if (root["sections"] is not JsonArray sections || sections[section] is not JsonObject renamed)
				return;
			var old = renamed["name"]?.GetValue<string>();
			renamed["name"] = name;
			foreach (var other in sections.OfType<JsonObject>())
				foreach (var exit in (other["exits"] as JsonArray ?? []).OfType<JsonObject>())
					if (exit["then"]?.GetValue<string>() == old)
						exit["then"] = name;
		});
		return null;
	}

	/// <summary>
	/// Sets a section's exits: each a condition as text (null for the last, which always holds) and where it goes. Each
	/// condition is read first; the reason one can't be, or null where they were taken.
	/// </summary>
	internal string? SetExits(int section, IReadOnlyList<(string? When, string Then)> exits)
	{
		for (var i = 0; i < exits.Count; i++)
		{
			if (exits[i].When is { } when && CheckCondition(when) is { } why)
				return $"Exit {i + 1}: {why}";
			if (exits[i].When is null && i < exits.Count - 1)
				return $"Exit {i + 1} has no condition, so it must be last.";
		}
		EditSection(section, s =>
		{
			if (exits.Count == 0)
			{
				s.Remove("exits");
				return;
			}
			s["exits"] = new JsonArray([.. exits.Select(e =>
			{
				var json = new JsonObject();
				if (e.When is { } when)
					json["when"] = when;
				json["then"] = e.Then;
				return (JsonNode)json;
			})]);
		});
		return null;
	}

	/// <summary>Why <paramref name="text"/> isn't a condition over the project's cues, or null where it is one.</summary>
	internal string? CheckCondition(string text)
	{
		try
		{
			ConditionText.Parse(text, CueNamed);
			return null;
		}
		catch (FormatException ex)
		{
			return ex.Message;
		}
	}

	/// <summary>The project's cue named <paramref name="name"/>, or null.</summary>
	internal CueBase? CueNamed(string name) => _catalog.Cues.FirstOrDefault(c => c.Name == name);

	/// <summary>Sets when a part plays (null: always); the reason the condition can't be read, or null.</summary>
	internal string? SetCondition(Placement placement, string? when)
	{
		if (when is not null && CheckCondition(when) is { } why)
			return why;
		EditPart(placement.Section.Index, IndexIn(placement), part =>
		{
			if (when is null)
				part.Remove("when");
			else
				part["when"] = when;
		});
		return null;
	}

	/// <summary>Sets the cue a part's level follows, or none.</summary>
	internal void SetLevelCue(Placement placement, string? cue) =>
		EditPart(placement.Section.Index, IndexIn(placement), part =>
		{
			if (cue is null)
				part.Remove("level");
			else
				part["level"] = cue;
		});

	/// <summary>Sets whether a part comes in only where its phrase starts over.</summary>
	internal void SetOnPhrase(Placement placement, bool onPhrase) =>
		EditPart(placement.Section.Index, IndexIn(placement), part =>
		{
			if (onPhrase)
				part["onPhrase"] = true;
			else
				part.Remove("onPhrase");
		});

	/// <summary>Sets the song's tempo, in beats a minute (20 to 300).</summary>
	internal void SetTempo(double bpm)
	{
		EditSong(root =>
				{
					// As the file writes numbers: a whole tempo as an int, else a float to a tenth
					var bpm10 = Math.Round(Math.Clamp(bpm, 20, 300) * 10) / 10;
					root["tempo"] = bpm10 == Math.Floor(bpm10) ? JsonValue.Create((int)bpm10) : JsonValue.Create((float)bpm10);
				});
	}

	/// <summary>Sets the song's key and scale: a root of <see cref="SongFile.ScaleRoots"/>, a scale of <see cref="SongFile.ScaleNames"/>.</summary>
	internal void SetScale(string root, string scale)
	{
		if (!SongFile.ScaleRoots.Contains(root) || !SongFile.ScaleNames.Contains(scale))
			throw new ArgumentException($"'{root} {scale}' isn't a scale a song file can name.");
		EditSong(file => file["scale"] = $"{root} {scale}");
	}

	/// <summary>Sets whether a line follows the chords: its degree 1 each bar's root, as a bass line plays.</summary>
	internal void SetOverChords(Placement placement, bool over) =>
		EditPart(placement.Section.Index, IndexIn(placement), part =>
		{
			if (over)
				part["overChords"] = true;
			else
				part.Remove("overChords");
		});

	private static int GridTicks(string grid) => grid switch
	{
		"quarters" => Ticks.Quarter,
		"eighths" => Ticks.Eighth,
		"sixteenths" => Ticks.Sixteenth,
		"triplets" => Ticks.Triplet,
		_ => 0,
	};

	/// <summary>
	/// The grid a part's notes are written on (quarters, eighths, sixteenths or triplets) and the grids its kind can be:
	/// a line any, a beat eighths and finer, a strum no triplets. Null for a part that is code.
	/// </summary>
	internal (string Grid, IReadOnlyList<string> Grids)? GridOf(Placement placement)
	{
		if (_editor?.Part(placement.Section.Index, IndexIn(placement)) is not { } part)
			return null;
		if (part["line"]?.GetValue<string>() is { } line)
			return (line, ["quarters", "eighths", "sixteenths", "triplets"]);
		if (part["beat"]?.GetValue<string>() is { } beat)
			return (beat, ["eighths", "sixteenths", "triplets"]);
		if (part["strum"]?.GetValue<string>() is { } strum)
			return (strum, ["quarters", "eighths", "sixteenths"]);
		return null;
	}

	/// <summary>
	/// Writes a part on another grid, its notes where they were: finer for shorter notes, coarser when every note starts on
	/// the coarser grid's steps. Why it can't be, or null.
	/// </summary>
	internal string? SetGrid(Placement placement, string grid)
	{
		var (section, index) = (placement.Section.Index, IndexIn(placement));
		if (_editor?.Part(section, index) is not { } part || GridOf(placement) is not { } current)
			return "This part is defined in code, so its grid is changed there.";
		if (!current.Grids.Contains(grid))
			return $"This kind of part can't be written in {grid}.";
		if (grid == current.Grid)
			return null;
		var (from, to) = (GridTicks(current.Grid), GridTicks(grid));
		if (Math.Max(from, to) % Math.Min(from, to) != 0)
			return $"Steps of {current.Grid} don't line up with {grid}, so the notes can't keep their timing.";
		var perBar = placement.Section.Song.Meter.TicksPerBar / to;
		var perBeat = Ticks.PerBeat / to;
		string? Respaced(string? text, bool characters)
		{
			var steps = (characters ? NotationText.Characters(text ?? "") : NotationText.Line(text ?? "")).Steps.ToList();
			return RollEdits.Regrid(steps, from, to, characters) is { } regridded ? RollEdits.Write(regridded, perBar, perBeat, characters) : null;
		}
		var kind = part["line"] is not null ? "line" : part["beat"] is not null ? "beat" : "strum";
		var why = $"Some notes start between the steps of {grid}, so the part can't be written in {grid} without moving them.";
		if (kind == "beat")
		{
			var rows = new Dictionary<string, string>();
			foreach (var (piece, row) in part["rows"] as JsonObject ?? [])
			{
				if (Respaced(row?.GetValue<string>(), characters: true) is not { } respaced)
					return why;
				rows[piece] = respaced;
			}
			EditPart(section, index, p =>
			{
				p["beat"] = grid;
				p["rows"] = new JsonObject(rows.Select(r => KeyValuePair.Create(r.Key, (JsonNode?)r.Value)));
			});
			return null;
		}
		if (Respaced(part["notes"]?.GetValue<string>(), characters: kind == "strum") is not { } notes)
			return why;
		EditPart(section, index, p =>
		{
			p[kind] = grid;
			p["notes"] = notes;
		});
		return null;
	}

	/// <summary>Deletes a part from its section; the roll and the inspector let it go.</summary>
	internal void DeletePart(Placement placement)
	{
		var (section, index) = (placement.Section.Index, IndexIn(placement));
		if (_editor is null || index < 0)
			return;
		_editor.Edit(root =>
		{
			if (root["sections"]?[section]?["parts"] is JsonArray parts && index < parts.Count)
				parts.RemoveAt(index);
		});
		_selected = null;
		_selectedIndex = -1;
		_inspected = null;
		ViewModel.Inspected = null;
		ViewModel.Roll.ShowNone("Select a part in a lane to edit it here.");
		ViewModel.Pattern.Clear();
		Rebuild(swap: true);
	}

	/// <summary>Takes a part's fill out of its last bar.</summary>
	internal void RemoveFill(Placement placement) =>
		EditPart(placement.Section.Index, IndexIn(placement), part => part.Remove("endWith"));

	/// <summary>Whether a part is data in its song's file (not code, nor added by a hook), so the workbench can change its notes.</summary>
	internal bool IsDataPart(Placement placement) => _editor?.Part(placement.Section.Index, IndexIn(placement)) is not null;

	/// <summary>Whether the chosen part can be converted to C#: its song has a file and a type of code to write it to.</summary>
	internal bool CanEject => Code is not null && _editor is not null && _source?.Type is not null;

	/// <summary>
	/// Gives a part a fill: its last bar, copied, to change in the roll, which opens on it. Why it can't, or null: a part
	/// in code, or one with a fill already.
	/// </summary>
	internal string? AddFill(Placement placement)
	{
		var (section, index) = (placement.Section.Index, IndexIn(placement));
		if (_editor?.Part(section, index) is not { } part)
			return "This part is defined in code, so its fill is written there.";
		if (part["endWith"] is not null)
			return "This part has a fill already.";
		var ticksPerBar = placement.Section.Song.Meter.TicksPerBar;
		var kind = part["line"] is not null ? "line" : part["beat"] is not null ? "beat" : part["strum"] is not null ? "strum" : null;
		if (kind is null || GridOf(placement) is not { } grid)
			return "This kind of part can't be given a fill here.";
		var step = GridTicks(grid.Grid);
		var (perBar, perBeat) = (ticksPerBar / step, Ticks.PerBeat / step);
		// The steps of the part's last bar; a note held into it from the bar before starts there instead
		List<string> LastBar(string text, bool characters)
		{
			var steps = (characters ? NotationText.Characters(text) : NotationText.Line(text)).Steps.ToList();
			var start = steps.Count == 0 ? 0 : (steps.Count - 1) / perBar * perBar;
			var bar = steps.Skip(start).ToList();
			while (bar.Count < perBar)
				bar.Add(".");
			if (!characters && bar[0] == "-")
				bar[0] = ".";
			return bar;
		}
		var fill = new JsonObject { [kind] = grid.Grid };
		if (kind == "beat")
		{
			var rows = new JsonObject();
			foreach (var (piece, hits) in part["rows"] as JsonObject ?? [])
				rows[piece] = RollEdits.Write(LastBar(hits?.GetValue<string>() ?? "", characters: true), perBar, perBeat, characters: true);
			fill["rows"] = rows;
		}
		else
		{
			fill["notes"] = RollEdits.Write(LastBar(part["notes"]?.GetValue<string>() ?? "", characters: kind == "strum"), perBar, perBeat, characters: kind == "strum");
			foreach (var key in new[] { "overChords", "octave", "voicing", "spread" })
				if (part[key] is { } value)
					fill[key] = value.DeepClone();
		}
		EditPart(section, index, p => p["endWith"] = fill);
		if (FindRow(section, index) is { } row)
			SelectPlacement(row, placement.Section.Bars - 1);
		return null;
	}

	/// <summary>Puts a copy of a part just below it in its section, and chooses the copy.</summary>
	internal void DuplicatePart(Placement placement)
	{
		var (section, index) = (placement.Section.Index, IndexIn(placement));
		if (_editor?.RawPart(section, index) is not { } raw)
			return;
		var copy = (JsonObject)raw.DeepClone();
		if (copy["name"]?.GetValue<string>() is { } name)
			copy["name"] = name + " copy";
		_editor.Edit(root =>
		{
			if (root["sections"]?[section]?["parts"] is JsonArray parts && index < parts.Count)
				parts.Insert(index + 1, copy);
		});
		Rebuild(swap: true);
		if (FindRow(section, index + 1) is { } row)
			SelectPlacement(row);
	}

	/// <summary>Renames a part; empty gives it its track's name again. Why it can't be, or null.</summary>
	internal string? RenamePart(Placement placement, string name)
	{
		name = name.Trim();
		var (section, index) = (placement.Section.Index, IndexIn(placement));
		if (_editor?.RawPart(section, index) is null)
			return "This part is added by a hook, so it is named in C#.";
		var keep = name.Length == 0 || name == placement.Track.Name;
		EditPart(section, index, part =>
		{
			if (keep)
				part.Remove("name");
			else
				part["name"] = name;
		});
		if (FindRow(section, index) is { } row)
			SelectPlacement(row);
		return null;
	}

	/// <summary>Adds a part for a part's track just above it or just below it in its section, chosen at once.</summary>
	internal void AddPartBeside(Placement placement, NewPartKind kind, bool below)
	{
		var own = placement.Section.Placements.Where(p => p.Track == placement.Track).ToList();
		var at = own.IndexOf(placement);
		AddPart(placement.Section.Index, placement.Track.Name, kind, below ? at + 1 : at);
	}

	// The clip of the part at `index` in section `section` of the song as built now
	private PlacementViewModel? FindRow(int section, int index)
	{
		if (_song is not { } song || (uint)section >= (uint)song.Sections.Count || (uint)index >= (uint)song.Sections[section].Placements.Count)
			return null;
		var placement = song.Sections[section].Placements[index];
		foreach (var track in ViewModel.Tracks)
			foreach (var clip in track.Placements)
				if (clip.Placement == placement)
					return clip;
		return null;
	}

	/// <summary>Where a cue is now, as the song hears it.</summary>
	internal float CueNow(CueBase cue) => Player.Get(cue);

	/// <summary>Which pass of a part's section in a row is playing: the player's while the section plays, else 1.</summary>
	internal int PassOf(Placement placement) =>
		Player.IsPlaying && Player.Song is { } song && placement.Section.Song == song && Player.Position.SectionIndex == placement.Section.Index
			? Math.Max(1, Player.Pass)
			: 1;

	/// <summary>The other parts in a part's section that wait for a level cue: each one's name and the level it waits at.</summary>
	internal IReadOnlyList<(string Name, float Level)> LevelsInSection(Placement placement, Cue cue)
	{
		var found = new List<(string, float)>();
		foreach (var other in placement.Section.Placements)
		{
			if (other == placement || other.Condition is not { } condition)
				continue;
			foreach (var (level, _) in Levels(condition, cue))
				found.Add((other.Name, level));
		}
		return found;
	}

	/// <summary>
	/// Adds a track playing an instrument of the catalog's, at <paramref name="at"/> among the tracks (the end without);
	/// the reason it can't be, or null.
	/// </summary>
	internal string? AddTrack(string name, string instrument, bool isKit, int? at = null)
	{
		name = name.Trim();
		if (name.Length == 0)
			return "Enter a name for the track.";
		if (_song?.Tracks.Any(t => t.Name == name) == true)
			return $"A track named '{name}' already exists.";
		EditSong(root =>
		{
			var tracks = root["tracks"] as JsonArray ?? [];
			root["tracks"] = tracks;
			var track = new JsonObject { ["name"] = name, [isKit ? "kit" : "patch"] = instrument };
			if (at is { } place && place >= 0 && place < tracks.Count)
				tracks.Insert(place, track);
			else
				tracks.Add(track);
		});
		return null;
	}

	/// <summary>
	/// Adds a track playing an instrument (dropped on the board, or chosen from a + between tracks), named for it
	/// ("warm-pad", then "warm-pad 2" and on), at <paramref name="at"/> among the tracks (the end without), and shows it in
	/// the inspector; the reason it can't be, or null.
	/// </summary>
	internal string? AddTrackPlaying(string instrument, bool isKit, int? at = null)
	{
		if (!CanEditSong)
			return "This song is defined in code. Tracks can be added only to songs with a .song.json file.";
		var name = instrument;
		for (var n = 2; _song?.Tracks.Any(t => t.Name == name) == true; n++)
			name = $"{instrument} {n}";
		if (AddTrack(name, instrument, isKit, at) is { } why)
			return why;
		if (ViewModel.Tracks.FirstOrDefault(t => t.Name == name) is { } added)
			Inspect(added);
		return null;
	}

	/// <summary>Renames a track, and every part it plays; the reason it can't be, or null.</summary>
	internal string? RenameTrack(string old, string name)
	{
		name = name.Trim();
		if (name.Length == 0)
			return "Enter a name for the track.";
		if (name != old && _song?.Tracks.Any(t => t.Name == name) == true)
			return $"A track named '{name}' already exists.";
		// The inspector follows the track to its new name as the song is built again
		if (_inspected is InspectedTrack inspected && inspected.Name == old)
			_inspected = new InspectedTrack(name);
		EditSong(root =>
		{
			foreach (var track in (root["tracks"] as JsonArray ?? []).OfType<JsonObject>())
				if (track["name"]?.GetValue<string>() == old)
					track["name"] = name;
			foreach (var section in (root["sections"] as JsonArray ?? []).OfType<JsonObject>())
				foreach (var key in TrackLists)
					foreach (var node in (section[key] as JsonArray ?? []).OfType<JsonObject>())
						if (node["track"]?.GetValue<string>() == old)
							node["track"] = name;
		});
		return null;
	}

	/// <summary>Gives a track another instrument from the catalog.</summary>
	internal void SetInstrument(string track, string instrument, bool isKit) =>
		EditSong(root =>
		{
			foreach (var t in (root["tracks"] as JsonArray ?? []).OfType<JsonObject>())
				if (t["name"]?.GetValue<string>() == track)
				{
					t.Remove("patch");
					t.Remove("kit");
					t[isKit ? "kit" : "patch"] = instrument;
				}
		});

	/// <summary>Takes a track out, with every part it plays and every sweep of it.</summary>
	internal void DeleteTrack(string track) =>
		EditSong(root =>
		{
			if (root["tracks"] is JsonArray tracks)
				foreach (var t in tracks.OfType<JsonObject>().Where(t => t["name"]?.GetValue<string>() == track).ToList())
					tracks.Remove(t);
			foreach (var section in (root["sections"] as JsonArray ?? []).OfType<JsonObject>())
				foreach (var key in TrackLists)
					if (section[key] is JsonArray list)
						foreach (var gone in list.OfType<JsonObject>().Where(p => p["track"]?.GetValue<string>() == track).ToList())
							list.Remove(gone);
		});

	/// <summary>
	/// Adds an empty part for <paramref name="track"/> in <paramref name="section"/>, a bar long: a line for a patch, a beat
	/// for a kit, on its first piece. With <paramref name="slot"/> it goes there among the parts the track already plays in
	/// the section (0 before the first, their count after the last); without, after every part. It is chosen, so the roll
	/// opens on it.
	/// </summary>
	internal void AddPart(int section, string track, NewPartKind kind = NewPartKind.Melody, int? slot = null)
	{
		if (_editor is null || _song?.Tracks.FirstOrDefault(t => t.Name == track) is not { } playing
			|| (uint)section >= (uint)_song.Sections.Count)
			return;
		var beats = _song.Meter.Beats;
		string Steps(string each) => string.Join(" ", Enumerable.Repeat(each, beats));
		JsonObject part = playing.Kit is { } kit && kit.Pieces.Count > 0
			// A beat is in eighths at the coarsest: two steps a beat
			? new JsonObject { ["track"] = track, ["beat"] = "eighths", ["rows"] = new JsonObject { [kit.Pieces[0].Piece.Id] = new string('.', beats * 2) } }
			: kind switch
			{
				NewPartKind.Bass => new JsonObject { ["track"] = track, ["line"] = "quarters", ["notes"] = Steps("1"), ["overChords"] = true, ["octave"] = -1 },
				// Down on each beat, an up between the last two: a strum to change in the roll
				NewPartKind.Strum => new JsonObject { ["track"] = track, ["strum"] = "eighths", ["notes"] = string.Concat(Enumerable.Range(0, beats).Select(b => b == beats - 1 ? "DU" : "D.")), ["voicing"] = "open" },
				// A block chord on the first beat and, in an even bar, halfway through: a rhythm to change in the roll
				NewPartKind.Chords => new JsonObject
				{
					["track"] = track,
					["strum"] = "quarters",
					["notes"] = string.Concat(Enumerable.Range(0, beats).Select(b => b == 0 || (beats % 2 == 0 && b == beats / 2) ? "D" : ".")),
					["voicing"] = "close",
					["spread"] = 0,
				},
				_ => new JsonObject { ["track"] = track, ["line"] = "quarters", ["notes"] = Steps(".") },
			};
		// Where in the file's parts it goes: before the track's part at the slot, or just after its last
		var own = _song.Sections[section].Placements.Where(p => p.Track.Name == track).ToList();
		int? before = slot is not { } s || own.Count == 0 ? null
			: s < own.Count ? IndexIn(own[Math.Max(0, s)]) : IndexIn(own[^1]) + 1;
		var index = -1;
		_editor.Edit(root =>
		{
			if (root["sections"]?[section] is not JsonObject target)
				return;
			var parts = target["parts"] as JsonArray ?? [];
			target["parts"] = parts;
			if (before is { } at && at >= 0 && at < parts.Count)
			{
				parts.Insert(at, part);
				index = at;
			}
			else
			{
				parts.Add(part);
				index = parts.Count - 1;
			}
		});
		Rebuild(swap: true);
		if (index >= 0 && _song is { } song && index < song.Sections[section].Placements.Count)
			foreach (var row in ViewModel.Tracks)
				foreach (var clip in row.Placements)
					if (clip.Placement == song.Sections[section].Placements[index])
						SelectPlacement(clip);
	}

	/// <summary>
	/// What the track the keyboard plays is sounding in the song now: its notes (for a kit, its pieces by index), whether it
	/// is a kit, and its place in the song for its hue; or nothing, while no song plays or the keyboard plays the bench.
	/// </summary>
	internal (UInt128 Notes, bool IsKit, int Index)? FocusedSounding()
	{
		if (!Player.IsPlaying || _dockTrack is not { } name || Player.Song is not { } song)
			return null;
		for (var i = 0; i < song.Tracks.Count; i++)
			if (song.Tracks[i].Name == name)
				return (Player.Sounding(song.Tracks[i]), song.Tracks[i].Kit is not null, i);
		return null;
	}

	/// <summary>Which of the twelve pitch classes, from C, the song's scale (or the one it is played in) has.</summary>
	internal bool[] ScaleClasses()
	{
		var scale = _scale ?? _song?.Scale ?? Scale.Major(Note.C);
		var classes = new bool[12];
		for (var pc = 0; pc < 12; pc++)
			classes[pc] = scale.Contains(pc);
		return classes;
	}

	/// <summary>Whether a new patch file can be made: the project has patch files, whose folder it goes in.</summary>
	public bool CanMakePatch => PatchFolder() is not null;

	/// <summary>Whether a new song file can be made: the project has song files, whose folder it goes in.</summary>
	public bool CanMakeSong => SongFolder() is not null;

	/// <summary>
	/// Makes a patch file beside the project's others: a copy of <paramref name="copyOf"/>'s file, or a plain sine held while
	/// a key is down. The project builds again as it sees the file, and the library lists it. Why it can't, or null.
	/// </summary>
	internal string? NewPatch(string name, string? copyOf)
	{
		if (FileName(name) is not { } stem)
			return "Use lowercase letters, digits and hyphens, for example electric-piano.";
		if (PatchFolder() is not { } folder)
			return "This project has no patch files, so there is no folder for a new one.";
		var path = Path.Combine(folder, stem + ".patch.json");
		if (Files.Exists(path))
			return $"{Path.GetFileName(path)} already exists.";
		JsonObject file;
		if (copyOf is not null && _catalog.PatchFile(copyOf) is { } source && Files.Exists(source))
		{
			file = JsonNode.Parse(Files.ReadText(source))!.AsObject();
			file["name"] = stem;
		}
		else
			file = new JsonObject
			{
				["format"] = "lunelle-patch/1",
				["name"] = stem,
				["operators"] = new JsonArray(new JsonObject { ["name"] = "sine1", ["kind"] = "sine", ["envelope"] = "adsr 0.01 0.1 0.8 0.2", ["level"] = 0.5 }),
				["out"] = new JsonArray(new JsonObject { ["op"] = "sine1" }),
			};
		Files.WriteText(path, BinaryVibrance.Lunelle.Music.SongFile.Write(file));
		Awaiting(stem, song: false);
		return null;
	}

	/// <summary>
	/// The catalog's songs with a track playing <paramref name="patch"/>, by name, read from their files (the open song as
	/// it is being edited); a song that is code alone isn't counted.
	/// </summary>
	internal IReadOnlyList<string> SongsUsing(string patch)
	{
		var songs = new List<string>();
		foreach (var (name, _) in _catalog.Songs)
		{
			if (_catalog.SongSource(name) is not { } source)
				continue;
			try
			{
				var text = _editor is { } editor && string.Equals(Path.GetFullPath(editor.Path), Path.GetFullPath(source.Path), StringComparison.OrdinalIgnoreCase)
					? editor.Text
					: Files.Exists(source.Path) ? Files.ReadText(source.Path) : null;
				if (text is not null && JsonNode.Parse(text)?["tracks"] is JsonArray tracks
					&& tracks.OfType<JsonObject>().Any(t => t["patch"] is JsonValue value && value.TryGetValue<string>(out var used) && used == patch))
					songs.Add(name);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
			{
				Console.WriteLine($"[workbench] {source.Path} couldn't be read for its patches: {ex.Message}");
			}
		}
		return songs;
	}

	/// <summary>The songs other than the open one with a track playing <paramref name="patch"/>.</summary>
	internal IReadOnlyList<string> OtherSongsUsing(string patch)
	{
		var open = _songIndex >= 0 ? _catalog.Songs[_songIndex].Name : null;
		return [.. SongsUsing(patch).Where(s => s != open)];
	}

	/// <summary>
	/// Makes a copy of <paramref name="patch"/>'s file as <paramref name="name"/>, opened in the patch designer once the
	/// project has built with it. Why it can't be, or null.
	/// </summary>
	internal string? DuplicatePatch(string patch, string name)
	{
		if (_catalog.PatchFile(patch) is not { } source || !Files.Exists(source))
			return $"{patch} is defined in C#, so it has no file to copy. Convert it to a patch file first.";
		return NewPatch(name, patch);
	}

	/// <summary>
	/// Gives <paramref name="track"/> of the open song a copy of its patch of its own: the patch's file copied as
	/// "patch-song" (with a number after it if that is taken), the track playing the copy, and the copy opened in the patch
	/// designer, once the project has built with it. Other songs keep the patch as it is. Why it can't be, or null.
	/// </summary>
	internal string? CopyPatchForSong(string track)
	{
		if (!CanEditSong || _songIndex < 0)
			return "This song is defined in code. Only songs with a .song.json file can be changed here.";
		if (_song?.Tracks.FirstOrDefault(t => t.Name == track) is not { Kit: null } playing)
			return $"{track} plays a kit, not a patch.";
		var patch = playing.Patch?.Name ?? "";
		if (PatchFolder() is not { } folder)
			return "This project has no patch files, so there is no folder for a copy.";
		var song = FileName(_catalog.Songs[_songIndex].Name) ?? "song";
		var stem = FileName($"{patch}-{song}") ?? $"patch-{song}";
		var name = stem;
		for (var n = 2; Files.Exists(Path.Combine(folder, name + ".patch.json")); n++)
			name = $"{stem}-{n}";
		if (DuplicatePatch(patch, name) is { } why)
			return why;
		_repoint = (_catalog.Songs[_songIndex].Name, track, name);
		ViewModel.BuildText = $"Copying {patch} as {name} for this song. {track} plays it once the build finishes.";
		return null;
	}

	// A track of a song to play a patch copied for it, once the project has built with the copy
	private (string Song, string Track, string Patch)? _repoint;

	/// <summary>Says in the patch designer which songs the patch shown is used in, so an edit's reach is clear.</summary>
	private void ShowPatchUsage(string patch)
	{
		var songs = SongsUsing(patch);
		ViewModel.Designer.UsedBy = songs.Count switch
		{
			0 => "Not used in any song",
			1 => $"Used in {songs[0]}",
			<= 3 => $"Used in {string.Join(", ", songs.Take(songs.Count - 1))} and {songs[^1]}. Edits change every one of them.",
			_ => $"Used in {songs.Count} songs: {string.Join(", ", songs.Take(3))} and {songs.Count - 3} more. Edits change every one of them.",
		};
	}

	/// <summary>
	/// Makes a song file beside the project's others: one section of four bars, one track on the first patch, nothing played
	/// yet. The project builds again as it sees the file, and the library lists it. Why it can't, or null.
	/// </summary>
	internal string? NewSong(string name)
	{
		if (FileName(name) is not { } stem)
			return "Use lowercase letters, digits and hyphens, for example open-road.";
		if (SongFolder() is not { } folder)
			return "This project has no song files, so there is no folder for a new one.";
		var path = Path.Combine(folder, stem + ".song.json");
		if (Files.Exists(path))
			return $"{Path.GetFileName(path)} already exists.";
		var patch = _catalog.Patches.Count > 0 ? _catalog.Patches[0].Name : null;
		var file = new JsonObject
		{
			["format"] = BinaryVibrance.Lunelle.Music.SongFile.Format,
			["name"] = stem,
			["tempo"] = 110,
			["scale"] = "C major",
			["tracks"] = patch is null ? new JsonArray() : new JsonArray(new JsonObject { ["name"] = "lead", ["patch"] = patch }),
			["sections"] = new JsonArray(new JsonObject { ["name"] = "a", ["bars"] = 4, ["chords"] = "I | IV | V | I", ["parts"] = new JsonArray() }),
		};
		Files.WriteText(path, BinaryVibrance.Lunelle.Music.SongFile.Write(file));
		Awaiting(stem, song: true);
		return null;
	}

	// A song or patch just made, opened once the project has built again and its catalog lists it
	private (string Name, bool Song)? _awaiting;

	private void Awaiting(string name, bool song)
	{
		_awaiting = (name, song);
		ViewModel.BuildText = $"Created {name}. It opens when the build finishes.";
	}

	// The catalog built again: what was just made, opened if it lists it now; a patch copied for a track, given to it first
	private void OpenAwaited()
	{
		if (_awaiting is not { } awaited)
			return;
		if (awaited.Song && Index(_catalog.Songs, awaited.Name) is >= 0 and var song)
		{
			_awaiting = null;
			ChooseSong(song);
		}
		else if (!awaited.Song && Index(_catalog.Patches, awaited.Name) is >= 0 and var patch)
		{
			_awaiting = null;
			if (_repoint is { } repoint && repoint.Patch == awaited.Name)
			{
				_repoint = null;
				if (_songIndex >= 0 && _catalog.Songs[_songIndex].Name == repoint.Song)
					SetInstrument(repoint.Track, repoint.Patch, isKit: false);
			}
			ChoosePatch(patch);
		}
	}

	private string? PatchFolder() =>
		_catalog.Patches.Select(p => _catalog.PatchFile(p.Name)).FirstOrDefault(f => f is not null && Files.Exists(f)) is { } file ? Path.GetDirectoryName(file) : null;

	private string? SongFolder() =>
		_catalog.Songs.Select(s => _catalog.SongSource(s.Name)?.Path).FirstOrDefault(f => f is not null && Files.Exists(f)) is { } file ? Path.GetDirectoryName(file) : null;

	// A name as a file's stem: lower case letters, digits and hyphens, or null
	private static string? FileName(string name)
	{
		var stem = name.Trim().ToLowerInvariant().Replace(' ', '-');
		return stem.Length > 0 && stem.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? stem : null;
	}
}
