using System.Globalization;
using System.Text.Json.Nodes;

using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

// Song exports (docs/spec/ui-exports.md, Lunelle's exports.md): the open song's exports listed, each rendered to a WAV
// the same every time, and a new one recorded from what the user does as the song plays: every cue moved, and every
// track's macro held or let go, becomes an event at the beat nearest where it was moved
public sealed partial class Workbench
{
	// A move recorded: a cue's, or a track's macro's (null: let go), at a bar and beat
	private sealed record Recorded(string? Cue, string? Track, int Macro, int Bar, int Beat, float? Value);

	private List<Recorded>? _recording;
	private Dictionary<string, float> _recordedStart = [];

	// The beat playing, and how long it has, for snapping a move to the nearest beat
	private (long Bar, int Beat) _beatAt;
	private double _beatSince;

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

	// A cue moved while recording: an event at the beat nearest the move, a later move on the same beat taking its place
	private void Record(CueBase cue, float value)
	{
		if (_recording is not { } recording || !Player.IsPlaying)
			return;
		var (bar, beat) = SnappedBeat();
		recording.RemoveAll(e => e.Cue == cue.Name && e.Bar == bar && e.Beat == beat);
		recording.Add(new Recorded(cue.Name, null, -1, bar, beat, value));
	}

	// A track's macro held (or let go) while recording: an event as a cue's is
	private void RecordMacro(string track, int macro, float? value)
	{
		if (_recording is not { } recording || !Player.IsPlaying)
			return;
		var (bar, beat) = SnappedBeat();
		recording.RemoveAll(e => e.Track == track && e.Macro == macro && e.Bar == bar && e.Beat == beat);
		recording.Add(new Recorded(null, track, macro, bar, beat, value));
	}

	// Once a frame: how long the beat playing has had
	private void TrackBeat(double deltaSeconds)
	{
		if (!Player.IsPlaying)
			return;
		var now = Player.Position;
		if ((now.AbsoluteBar, now.Beat) != _beatAt)
		{
			_beatAt = (now.AbsoluteBar, now.Beat);
			_beatSince = 0;
		}
		else
			_beatSince += deltaSeconds;
	}

	/// <summary>
	/// The bar (from 1) and beat a move now lands on: the beat playing, or the next where more than half of this one has
	/// gone (the bar's first beat after its last).
	/// </summary>
	internal (int Bar, int Beat) SnappedBeat()
	{
		var position = Player.Position;
		var bar = (int)position.AbsoluteBar + 1;
		var beat = position.Beat;
		if (_song is { } song && (position.AbsoluteBar, position.Beat) == _beatAt && _beatSince > 30.0 / song.Tempo * 4.0 / song.Meter.Unit)
		{
			beat++;
			if (beat > song.Meter.Beats)
			{
				beat = 1;
				bar++;
			}
		}
		return (bar, beat);
	}

	/// <summary>
	/// The macros track <paramref name="track"/>'s patch has, by id, each with its name (the manifest's, else its number)
	/// and where the song holds it now (null: not held); none for a kit's track or a track the song hasn't.
	/// </summary>
	public IReadOnlyList<(int Id, string Name, float? Held)> TrackMacros(string track)
	{
		if (_song?.Tracks.FirstOrDefault(t => t.Name == track) is not { Patch: { } patch })
			return [];
		var names = ManifestMacros().ToDictionary(m => m.Value, m => m.Key);
		return [.. patch.Macros.Select(m => m.Macro.Id).Distinct().Order()
			.Select(id => (id, names.GetValueOrDefault(id) ?? $"macro {id}", _heldMacros.TryGetValue((track, id), out var held) ? held : (float?)null))];
	}

	// The macros held from the track inspector, by track and macro, so it shows them as held
	private readonly Dictionary<(string Track, int Macro), float> _heldMacros = [];

	/// <summary>
	/// Holds track <paramref name="track"/>'s macro <paramref name="macro"/> at <paramref name="value"/> as the song plays
	/// (Lunelle's HoldMacro), or lets it go (null) to what the song makes it follow; recorded while recording.
	/// </summary>
	public void HoldTrackMacro(string track, int macro, float? value)
	{
		if (_song?.Tracks.All(t => t.Name != track) != false)
			return;
		// Held on the song playing; one not playing takes it up as it starts (Play)
		if (Player.IsPlaying && Player.Song?.Tracks.FirstOrDefault(t => t.Name == track) is { } playing)
			Player.HoldMacro(playing, new BinaryVibrance.Lunelle.Chip.Macro(macro), value);
		if (value is { } held)
			_heldMacros[(track, macro)] = held;
		else
			_heldMacros.Remove((track, macro));
		RecordMacro(track, macro, value);
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
		var macroNames = ManifestMacros().ToDictionary(m => m.Value, m => m.Key);
		foreach (var move in recording.OrderBy(e => e.Bar).ThenBy(e => e.Beat))
		{
			if (move.Cue is { } cue && ViewModel.Cues.FirstOrDefault(c => c.Name == cue) is { } row)
				events.Add(new JsonObject { ["at"] = $"{move.Bar}:{move.Beat}", ["cue"] = cue, ["to"] = JsonNode.Parse(Value(row.Cue, move.Value!.Value)) });
			else if (move.Track is { } track)
				events.Add(new JsonObject
				{
					["at"] = $"{move.Bar}:{move.Beat}",
					["track"] = track,
					["macro"] = macroNames.TryGetValue(move.Macro, out var named) ? JsonValue.Create(named) : JsonValue.Create(move.Macro),
					["to"] = move.Value is { } held ? JsonValue.Create(MathF.Round(held, 3)) : null,
				});
		}
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
