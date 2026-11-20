using System.Text.Json.Nodes;
using Loom.Audio.Synth.Chip;
using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

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
			return "A section has a name.";
		if (_song?.Sections.Any(s => s.Index != section && s.Name == name) == true)
			return $"Another section is named '{name}'.";
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
				return $"Exit {i + 1} always holds, so it comes last.";
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

	/// <summary>Takes a part's fill out of its last bar.</summary>
	internal void RemoveFill(Placement placement) =>
		EditPart(placement.Section.Index, IndexIn(placement), part => part.Remove("endWith"));

	/// <summary>Adds a track playing an instrument of the catalog's; the reason it can't be, or null.</summary>
	internal string? AddTrack(string name, string instrument, bool isKit)
	{
		name = name.Trim();
		if (name.Length == 0)
			return "A track has a name.";
		if (_song?.Tracks.Any(t => t.Name == name) == true)
			return $"A track is named '{name}' already.";
		EditSong(root =>
		{
			var tracks = root["tracks"] as JsonArray ?? [];
			root["tracks"] = tracks;
			tracks.Add(new JsonObject { ["name"] = name, [isKit ? "kit" : "patch"] = instrument });
		});
		return null;
	}

	/// <summary>Renames a track, and every part it plays; the reason it can't be, or null.</summary>
	internal string? RenameTrack(string old, string name)
	{
		name = name.Trim();
		if (name.Length == 0)
			return "A track has a name.";
		if (name != old && _song?.Tracks.Any(t => t.Name == name) == true)
			return $"A track is named '{name}' already.";
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
	/// for a kit, on its first piece. It is chosen, so the roll opens on it.
	/// </summary>
	internal void AddPart(int section, string track)
	{
		if (_editor is null || _song?.Tracks.FirstOrDefault(t => t.Name == track) is not { } playing)
			return;
		var beats = _song.Meter.Beats;
		var empty = string.Join(" ", Enumerable.Repeat(".", beats));
		JsonObject part = playing.Kit is { } kit && kit.Pieces.Count > 0
			? new JsonObject { ["track"] = track, ["beat"] = "quarters", ["rows"] = new JsonObject { [kit.Pieces[0].Piece.Id] = new string('.', beats) } }
			: new JsonObject { ["track"] = track, ["line"] = "quarters", ["notes"] = empty };
		var index = -1;
		_editor.Edit(root =>
		{
			if (root["sections"]?[section] is not JsonObject target)
				return;
			var parts = target["parts"] as JsonArray ?? [];
			target["parts"] = parts;
			parts.Add(part);
			index = parts.Count - 1;
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
			return "A patch's name is letters, digits and hyphens: electric-piano.";
		if (PatchFolder() is not { } folder)
			return "This project has no patch files to put one beside.";
		var path = Path.Combine(folder, stem + ".patch.json");
		if (File.Exists(path))
			return $"{Path.GetFileName(path)} is there already.";
		JsonObject file;
		if (copyOf is not null && _catalog.PatchFile(copyOf) is { } source && File.Exists(source))
		{
			file = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
			file["name"] = stem;
		}
		else
			file = new JsonObject
			{
				["format"] = "loom-patch/1",
				["name"] = stem,
				["operators"] = new JsonArray(new JsonObject { ["name"] = "sine1", ["kind"] = "sine", ["envelope"] = "adsr 0.01 0.1 0.8 0.2", ["level"] = 0.5 }),
				["out"] = new JsonArray(new JsonObject { ["op"] = "sine1" }),
			};
		File.WriteAllText(path, Synth.Music.SongFile.Write(file));
		return null;
	}

	/// <summary>
	/// Makes a song file beside the project's others: one section of four bars, one track on the first patch, nothing played
	/// yet. The project builds again as it sees the file, and the library lists it. Why it can't, or null.
	/// </summary>
	internal string? NewSong(string name)
	{
		if (FileName(name) is not { } stem)
			return "A song's name is letters, digits and hyphens: open-road.";
		if (SongFolder() is not { } folder)
			return "This project has no song files to put one beside.";
		var path = Path.Combine(folder, stem + ".song.json");
		if (File.Exists(path))
			return $"{Path.GetFileName(path)} is there already.";
		var patch = _catalog.Patches.Count > 0 ? _catalog.Patches[0].Name : null;
		var file = new JsonObject
		{
			["format"] = Synth.Music.SongFile.Format,
			["name"] = stem,
			["tempo"] = 110,
			["scale"] = "C major",
			["tracks"] = patch is null ? new JsonArray() : new JsonArray(new JsonObject { ["name"] = "lead", ["patch"] = patch }),
			["sections"] = new JsonArray(new JsonObject { ["name"] = "a", ["bars"] = 4, ["chords"] = "I | IV | V | I", ["parts"] = new JsonArray() }),
		};
		File.WriteAllText(path, Synth.Music.SongFile.Write(file));
		return null;
	}

	private string? PatchFolder() =>
		_catalog.Patches.Select(p => _catalog.PatchFile(p.Name)).FirstOrDefault(f => f is not null && File.Exists(f)) is { } file ? Path.GetDirectoryName(file) : null;

	private string? SongFolder() =>
		_catalog.Songs.Select(s => _catalog.SongSource(s.Name)?.Path).FirstOrDefault(f => f is not null && File.Exists(f)) is { } file ? Path.GetDirectoryName(file) : null;

	// A name as a file's stem: lower case letters, digits and hyphens, or null
	private static string? FileName(string name)
	{
		var stem = name.Trim().ToLowerInvariant().Replace(' ', '-');
		return stem.Length > 0 && stem.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? stem : null;
	}
}
