using System.Globalization;
using System.Text.Json.Nodes;

using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// One event of an export on Arrange's automation lane (docs/spec/ui-exports.md): its place in the file's events, where it
/// is (as written), what it does, and the section column it is drawn in with how far across it (0 to 1).
/// </summary>
public sealed record AutomationMark(int Index, string At, string Label, int Section, float Across);

/// <summary>What an event of the lane can do: a cue set, a track's macro held, or a jump, with what the file writes for it.</summary>
public sealed record AutomationTarget(string Label, string? Cue, string? Track, string? Macro, bool IsJump, IReadOnlyList<string> Options);

// The automation lane: an export's events over Arrange's sections, each edited, deleted or added as one undoable step.
// An event at a section's start or end is drawn there; one at a bar where the song was at that bar when the export last
// played live, or, until it has, along the sections in their order from the export's start
public sealed partial class Workbench
{
	// The sections each export passed through as it last played live: from which bar (from 0), which section
	private readonly Dictionary<string, List<(long FromBar, int Section)>> _exportPaths = new(StringComparer.Ordinal);

	/// <summary>Shows export <paramref name="export"/> on the automation lane, or none.</summary>
	public void ShowLane(string? export)
	{
		ViewModel.LaneExport = export;
		ShowLane();
	}

	// The lane's marks for the export it shows, read again from the song's text
	private void ShowLane()
	{
		ViewModel.LaneExports = [.. SongExports.Select(e => e.Name)];
		if (ViewModel.LaneExport is { } shown && !ViewModel.LaneExports.Contains(shown))
			ViewModel.LaneExport = null;
		ViewModel.LaneMarks = ViewModel.LaneExport is { } export ? LaneMarks(export) : [];
		ViewModel.LanePlaced = ViewModel.LaneExport is { } e && _exportPaths.ContainsKey(e);
	}

	// An export's events (as the file writes them), or none
	private List<JsonObject> ExportEvents(string export, out JsonObject? exported)
	{
		exported = null;
		if (_editor is null)
			return [];
		try
		{
			exported = (JsonNode.Parse(_editor.Text)?["exports"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(e => e["name"]?.GetValue<string>() == export);
		}
		catch (System.Text.Json.JsonException)
		{
			return [];
		}
		return exported?["events"] is JsonArray events ? [.. events.OfType<JsonObject>()] : [];
	}

	/// <summary>The marks export <paramref name="export"/>'s events make on the lane, in the file's order.</summary>
	public IReadOnlyList<AutomationMark> LaneMarks(string export)
	{
		if (_song is not { } song || song.Sections.Count == 0)
			return [];
		var events = ExportEvents(export, out var exported);
		var start = exported?["start"]?.GetValue<string>() is { } s && song.Sections.FirstOrDefault(x => x.Name == s) is { } first ? first.Index : 0;
		var marks = new List<AutomationMark>();
		for (var i = 0; i < events.Count; i++)
		{
			var e = events[i];
			if (e["at"]?.GetValue<string>() is not { } text || ExportPosition.Parse(text, new JString(text), out _) is not { } at)
				continue;
			int column;
			float across;
			if (at.Section is { } name)
			{
				if (song.Sections.FirstOrDefault(x => x.Name == name) is not { } section)
					continue;
				(column, across) = (section.Index, at.IsEnd ? 1f : 0f);
			}
			else
				(column, across) = Place(export, song, start, at.Bar - 1 + (at.Beat - 1) / song.Meter.Beats);
			marks.Add(new AutomationMark(i, text, DescribeEvent(e), column, across));
		}
		return marks;
	}

	// Where a bar (from 0, with its beat's share) falls: in the section the export was in then as it last played live, or
	// along the sections in order from its start, round again past the last
	private (int Section, float Across) Place(string export, Song song, int start, double bar)
	{
		if (_exportPaths.TryGetValue(export, out var path) && path.Count > 0)
		{
			var (from, section) = path.LastOrDefault(p => p.FromBar <= bar);
			if (path[0].FromBar > bar)
				(from, section) = path[0];
			var bars = Math.Max(1, song.Sections[section].Bars);
			return (section, (float)Math.Clamp((bar - from) / bars, 0, 1));
		}
		var total = song.Sections.Sum(x => Math.Max(1, x.Bars));
		var at = bar % total;
		for (var k = 0; k < song.Sections.Count; k++)
		{
			var section = song.Sections[(start + k) % song.Sections.Count];
			var bars = Math.Max(1, section.Bars);
			if (at < bars)
				return (section.Index, (float)(at / bars));
			at -= bars;
		}
		return (start, 0f);
	}

	// What an event does, as the lane labels it: "mood → Dark", "lead bright → 0.4 over 2 beats", "jump → chorus"
	private static string DescribeEvent(JsonObject e)
	{
		var over = e["over"]?.GetValue<string>() is { } o ? $" over {o}" : "";
		string To() => e["to"] is { } to ? to.ToJsonString().Trim('"') : "let go";
		if (e["jump"]?.GetValue<string>() is { } jump)
			return $"jump → {jump}";
		if (e["cue"]?.GetValue<string>() is { } cue)
			return $"{cue} → {To()}{over}";
		return $"{e["track"]?.GetValue<string>()} {e["macro"]?.ToJsonString().Trim('"')} → {To()}{over}";
	}

	/// <summary>
	/// What a new event can do in the open song: each cue it imports, each track's macros (by the manifest's names, else
	/// their numbers), and a jump.
	/// </summary>
	public IReadOnlyList<AutomationTarget> LaneTargets()
	{
		var targets = new List<AutomationTarget>();
		foreach (var row in ViewModel.Cues)
			targets.Add(new AutomationTarget(row.Name, row.Name, null, null, false, row.Cue.OptionNames));
		var names = ManifestMacros().ToDictionary(m => m.Value, m => m.Key);
		foreach (var track in _song?.Tracks ?? [])
			foreach (var macro in track.Patch is { } patch ? patch.Macros.Select(m => m.Macro.Id).Distinct().Order().ToList() : [])
			{
				var name = names.GetValueOrDefault(macro);
				targets.Add(new AutomationTarget($"{track.Name} · {name ?? $"macro {macro}"}", null, track.Name, name ?? macro.ToString(CultureInfo.InvariantCulture), false, []));
			}
		targets.Add(new AutomationTarget("jump", null, null, null, true, []));
		return targets;
	}

	/// <summary>
	/// Writes an event into export <paramref name="export"/>: in place of its event <paramref name="index"/>, or added
	/// after its others (null). It happens <paramref name="at"/> (<c>"17:1"</c>, <c>"section lift#2"</c>); it sets
	/// <paramref name="target"/> to <paramref name="to"/> (a level or a macro from 0 to 1, an option's name; empty lets a
	/// macro go; for a jump, the section), over <paramref name="over"/> where given (<c>"2 beats"</c>). One undoable step.
	/// Why it can't be, or null.
	/// </summary>
	public string? PutExportEvent(string export, int? index, string at, AutomationTarget target, string? to, string? over)
	{
		if (_editor is null || _song is not { } song)
			return "This song has no file to keep its exports in.";
		at = at.Trim();
		if (ExportPosition.Parse(at, new JString(at), out var badAt) is null)
			return $"\"{at}\": {badAt}";
		over = string.IsNullOrWhiteSpace(over) ? null : over.Trim();
		if (over is not null && (target.IsJump || ExportDuration.Parse(over) is null))
			return target.IsJump ? "A jump happens at once." : $"\"{over}\" isn't a length: \"2 beats\", \"1 bar\", \"0.5s\".";
		to = to?.Trim() ?? "";
		var e = new JsonObject { ["at"] = at };
		if (target.IsJump)
		{
			if (song.Sections.All(s => s.Name != to))
				return $"A jump goes to a section: {string.Join(", ", song.Sections.Select(s => s.Name))}.";
			e["jump"] = to;
		}
		else if (target.Cue is { } cue)
		{
			e["cue"] = cue;
			if (target.Options.Count > 0)
			{
				if (!target.Options.Contains(to))
					return $"{cue} is one of {string.Join(", ", target.Options)}.";
				e["to"] = to;
			}
			else if (Level(to) is { } level)
				e["to"] = level;
			else
				return $"{cue} is a level, from 0 to 1.";
		}
		else
		{
			e["track"] = target.Track;
			e["macro"] = int.TryParse(target.Macro, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? JsonValue.Create(number) : JsonValue.Create(target.Macro);
			if (to.Length == 0)
				e["to"] = null;
			else if (Level(to) is { } level)
				e["to"] = level;
			else
				return "A macro is held from 0 to 1, or let go (empty).";
		}
		if (over is not null)
			e["over"] = over;
		if (ExportEvents(export, out var exported) is var events && exported is null)
			return $"This song has no export {export}.";
		if (index is { } i && (uint)i >= (uint)events.Count)
			return "That event isn't in the export now.";
		Seal();
		_editor.Edit(root =>
		{
			var target = (root["exports"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x => x["name"]?.GetValue<string>() == export);
			if (target is null)
				return;
			if (target["events"] is not JsonArray list)
				target["events"] = list = [];
			if (index is { } at)
				list[at] = e;
			else
				list.Add(e);
		});
		_editor.Save();
		Seal();
		ShowLane();
		return null;
	}

	/// <summary>Takes event <paramref name="index"/> out of export <paramref name="export"/>. One undoable step. Why it can't be, or null.</summary>
	public string? DeleteExportEvent(string export, int index)
	{
		if (_editor is null)
			return "This song has no file to keep its exports in.";
		if ((uint)index >= (uint)ExportEvents(export, out _).Count)
			return "That event isn't in the export now.";
		Seal();
		_editor.Edit(root =>
		{
			var target = (root["exports"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x => x["name"]?.GetValue<string>() == export);
			if (target?["events"] is JsonArray list)
			{
				list.RemoveAt(index);
				if (list.Count == 0)
					target.Remove("events");
			}
		});
		_editor.Save();
		Seal();
		ShowLane();
		return null;
	}

	/// <summary>An event's fields as the lane's editor shows them: where, what (one of <see cref="LaneTargets"/>), its value, and its length.</summary>
	public (string At, AutomationTarget? Target, string To, string Over)? ExportEvent(string export, int index)
	{
		var events = ExportEvents(export, out _);
		if ((uint)index >= (uint)events.Count)
			return null;
		var e = events[index];
		var targets = LaneTargets();
		var macro = e["macro"]?.ToJsonString().Trim('"');
		var target = e["jump"] is not null ? targets.FirstOrDefault(t => t.IsJump)
			: e["cue"]?.GetValue<string>() is { } cue ? targets.FirstOrDefault(t => t.Cue == cue)
			: targets.FirstOrDefault(t => t.Track == e["track"]?.GetValue<string>() && t.Macro == macro);
		var to = e["jump"]?.GetValue<string>() ?? (e["to"] is { } value ? value.ToJsonString().Trim('"') : "");
		return (e["at"]?.GetValue<string>() ?? "", target, to, e["over"]?.GetValue<string>() ?? "");
	}

	private static float? Level(string text) =>
		float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var level) && level is >= 0f and <= 1f ? level : null;

	// As an export plays live: each section it starts, from the bar it starts at, for the lane to place its bars by
	private void NoteLivePath(string export, long bar, int section)
	{
		if (!_exportPaths.TryGetValue(export, out var path))
			_exportPaths[export] = path = [];
		if (path.Count == 0 || path[^1].Section != section || path[^1].FromBar != bar)
			path.Add((bar, section));
	}
}
