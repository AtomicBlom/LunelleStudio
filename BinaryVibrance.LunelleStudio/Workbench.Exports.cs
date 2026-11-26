using System.Globalization;
using System.Text.Json.Nodes;

using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

// Song exports (docs/spec/ui-exports.md, Lunelle's exports.md): the open song's exports listed, each rendered to a WAV
// the same every time, and a new one recorded from what the user does as the song plays: every cue moved becomes an event
// at the beat it was moved on
public sealed partial class Workbench
{
	private List<(string Cue, int Bar, int Beat, float Value)>? _recording;
	private Dictionary<string, float> _recordedStart = [];

	/// <summary>The open song's exports, by name, with their titles; none for a song with no file.</summary>
	public IReadOnlyList<(string Name, string Title)> SongExports
	{
		get
		{
			if (_editor is null)
				return [];
			try
			{
				return JsonNode.Parse(_editor.Text)?["exports"] is JsonArray exports
					? [.. exports.OfType<JsonObject>().Select(e => (e["name"]?.GetValue<string>() ?? "", e["title"]?.GetValue<string>() ?? e["name"]?.GetValue<string>() ?? ""))
						.Where(e => e.Item1.Length > 0)]
					: [];
			}
			catch (System.Text.Json.JsonException)
			{
				return [];
			}
		}
	}

	/// <summary>Whether a song's export can be rendered here: a project whose music the catalog can render.</summary>
	public bool CanRenderExports => _catalog.RenderExport is not null;

	/// <summary>
	/// Renders the open song's export <paramref name="export"/> to <paramref name="output"/> as a WAV (exports.md §7), the
	/// edits saved first so the render is what the project now says. Why it can't be, or null.
	/// </summary>
	public string? RenderExport(string export, Stream output)
	{
		if (_catalog.RenderExport is not { } render || (uint)_songIndex >= (uint)_catalog.Songs.Count)
			return "This project's songs can't be rendered here.";
		_editor?.Save();
		try
		{
			return render(_catalog.Songs[_songIndex].Name, export, output);
		}
		catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or IOException)
		{
			return ex.Message;
		}
	}

	/// <summary>Whether a recording is under way.</summary>
	public bool IsRecording => _recording is not null;

	/// <summary>
	/// Starts recording an export: the song plays from its start, its cues' values now its export's starting values, and
	/// every cue moved from here (on the strip) is an event at the beat it was moved on. Why it can't be, or null.
	/// </summary>
	public string? StartRecording()
	{
		if (_editor is null)
			return "This song has no file to record an export into.";
		_recording = [];
		_recordedStart = ViewModel.Cues.ToDictionary(c => c.Name, c => Player.Get(c.Cue));
		Stop();
		Play();
		ViewModel.IsRecording = true;
		return null;
	}

	// A cue moved while recording: an event at the beat it was moved on, a later move on the same beat taking its place
	private void Record(CueBase cue, float value)
	{
		if (_recording is not { } recording || !Player.IsPlaying)
			return;
		var position = Player.Position;
		var bar = (int)position.AbsoluteBar + 1;
		recording.RemoveAll(e => e.Cue == cue.Name && e.Bar == bar && e.Beat == position.Beat);
		recording.Add((cue.Name, bar, position.Beat, value));
	}

	/// <summary>
	/// Stops recording, and writes it into the song as an export named <paramref name="name"/> (exports.md §2): its
	/// starting cues, an event for each cue moved, and its end at the bar after the one playing. One undoable step. Why
	/// it can't be, or null.
	/// </summary>
	public string? StopRecording(string name)
	{
		if (_recording is not { } recording)
			return "Nothing is being recorded.";
		var end = (int)Player.Position.AbsoluteBar + 2;
		_recording = null;
		ViewModel.IsRecording = false;
		Stop();
		if (_editor is null)
			return "This song has no file to record an export into.";
		if (BinaryVibrance.Lunelle.Format.EntryIds.SegmentProblem(name) is { } bad)
			return $"An export's name is an id: {bad}.";
		if (SongExports.Any(e => e.Name == name))
			return $"This song has an export {name} already.";
		string Value(CueBase cue, float value) =>
			cue.OptionNames.Count > 0 && CueViewModel.OptionIndex(cue, value) is >= 0 and var option
				? JsonValue.Create(cue.OptionNames[option])!.ToJsonString()
				: value.ToString("0.###", CultureInfo.InvariantCulture);
		var export = new JsonObject { ["name"] = name };
		var cues = new JsonObject();
		foreach (var row in ViewModel.Cues)
			if (_recordedStart.TryGetValue(row.Name, out var start))
				cues[row.Name] = JsonNode.Parse(Value(row.Cue, start));
		if (cues.Count > 0)
			export["cues"] = cues;
		var events = new JsonArray();
		foreach (var (cue, bar, beat, value) in recording.OrderBy(e => e.Bar).ThenBy(e => e.Beat))
			if (ViewModel.Cues.FirstOrDefault(c => c.Name == cue) is { } row)
				events.Add(new JsonObject { ["at"] = $"{bar}:{beat}", ["cue"] = cue, ["to"] = JsonNode.Parse(Value(row.Cue, value)) });
		if (events.Count > 0)
			export["events"] = events;
		export["end"] = new JsonObject { ["at"] = $"{end}:1" };
		Seal();
		_editor.Edit(song =>
		{
			if (song["exports"] is not JsonArray exports)
				song["exports"] = exports = [];
			exports.Add(export);
		});
		_editor.Save();
		Seal();
		ViewModel.BuildText = $"Recorded the export {name}: {events.Count} event{(events.Count == 1 ? "" : "s")}.";
		return null;
	}
}
