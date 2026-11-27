using System.Globalization;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

// An export played live (docs/spec/ui-exports.md): the song from its export's start, its cues where the export starts
// them, and each event applied as the song reaches it, to hear an export before rendering it. Live, an event lands on the
// beat it names as the player reaches it, not on its exact frame: the render is the exact one
public sealed partial class Workbench
{
	private sealed record LiveEvent(ExportPosition At, JObject Json, int Order);

	private sealed record LiveRamp(CueBase? Cue, SongTrack? Track, int Macro, float From, float To, double Start, double End);

	private sealed class LiveRun(string name, List<LiveEvent> pending, ExportPosition end)
	{
		public string Name { get; } = name;
		public List<LiveEvent> Pending { get; } = pending;
		public ExportPosition End { get; } = end;
		public List<LiveRamp> Ramps { get; } = [];
		public Dictionary<string, int> Times { get; } = new(StringComparer.Ordinal);
		public Dictionary<(string Track, int Macro), float> Held { get; } = [];
		public MusicPosition? Last { get; set; }
		public double Clock { get; set; }
	}

	private LiveRun? _live;

	/// <summary>The export playing live, or null.</summary>
	public string? LiveExport => _live?.Name;

	/// <summary>
	/// Plays the open song's export <paramref name="name"/> live: its cues where it starts them (else the song's
	/// previews), from its start section, each event applied as the song reaches it, and stopped at its end. Why it can't
	/// be, or null.
	/// </summary>
	public string? PlayExport(string name)
	{
		if (_editor is null)
			return "This song has no file, so no exports.";
		JObject? export;
		try
		{
			export = (JReader.Parse(_editor.Text)["exports"] as JArray)?.Values.OfType<JObject>().FirstOrDefault(e => e["name"]?.Str == name);
		}
		catch (FormatError ex)
		{
			return ex.Message;
		}
		if (export is null)
			return $"This song has no export {name}.";
		var pending = new List<LiveEvent>();
		var order = 0;
		foreach (var item in export["events"]?.Items ?? [])
		{
			if (item is not JObject json || json["at"]?.Str is not { } text)
				continue;
			if (ExportPosition.Parse(text, json, out var why) is not { } at)
				return $"An event's \"at\" ({text}): {why}";
			pending.Add(new LiveEvent(at, json, order++));
		}
		if (export["end"]?["at"]?.Str is not { } endText)
			return "The export has no end (\"end\": { \"at\": \"9:1\" }).";
		if (ExportPosition.Parse(endText, export["end"]!, out var endWhy) is not { } end)
			return $"The export's end ({endText}) can't be read: {endWhy}";
		StopLiveExport();
		Stop();
		PreviewCues();
		foreach (var (reference, value) in (export["cues"] as JObject)?.Members.Select(m => (m.Key, m.Value)) ?? [])
			if (CueFor(reference) is { } cue && LiveValue(cue, value) is { } at)
				SetCue(cue, at);
		ShowCueValues();
		Play();
		if (export["start"]?.Str is { } start && _song?.Sections.FirstOrDefault(s => s.Name == start) is { } section)
			Player.Jump(section);
		_live = new LiveRun(name, pending, end);
		_exportPaths.Remove(name);
		ViewModel.LiveExport = name;
		return null;
	}

	/// <summary>Stops an export playing live, leaving the song playing as it is.</summary>
	public void StopLiveExport()
	{
		if (_live is null)
			return;
		_live = null;
		ViewModel.LiveExport = null;
	}

	// The strip shows each cue as the player has it
	private void ShowCueValues()
	{
		foreach (var row in ViewModel.Cues)
			row.Show(Player.Get(row.Cue));
	}

	// A cue's value as an export writes it: a level's number, a choice's option by name
	private static float? LiveValue(CueBase cue, JNode? value) => cue.OptionNames.Count > 0
		? value?.Str is { } option && cue.OptionNames.ToList().IndexOf(option) is >= 0 and var index ? cue.OptionValue(index) : null
		: value is JNumber number ? (float)number.Value : null;

	// Once a frame while an export plays live: where the song is, what that reaches, and each ramp moved on
	private void StepLiveExport(double deltaSeconds)
	{
		if (_live is not { } live)
			return;
		if (!Player.IsPlaying || Player.Song is not { } song)
		{
			StopLiveExport();
			return;
		}
		live.Clock += deltaSeconds;
		var now = Player.Position;
		string? ended = null, started = null;
		int endedTime = 0, startedTime = 0;
		if (live.Last is not { } last)
		{
			started = now.Section;
			startedTime = live.Times[now.Section] = 1;
		}
		else if (last.Section != now.Section || now.Bar < last.Bar)
		{
			ended = last.Section;
			endedTime = live.Times.GetValueOrDefault(last.Section);
			started = now.Section;
			startedTime = live.Times[now.Section] = live.Times.GetValueOrDefault(now.Section) + 1;
		}
		live.Last = now;
		if (started is not null)
			NoteLivePath(live.Name, now.AbsoluteBar, now.SectionIndex);
		bool Reached(ExportPosition p) => p.Section is null
			? now.AbsoluteBar + 1 > p.Bar || (now.AbsoluteBar + 1 == p.Bar && now.Beat >= (int)Math.Floor(p.Beat))
			: p.IsEnd ? p.Section == ended && p.Time == endedTime : p.Section == started && p.Time == startedTime;
		if (Reached(live.End))
		{
			StopLiveExport();
			Stop();
			return;
		}
		foreach (var due in live.Pending.Where(e => Reached(e.At)).OrderBy(e => e.Order).ToList())
		{
			live.Pending.Remove(due);
			ApplyLive(live, song, due.Json);
		}
		for (var i = live.Ramps.Count - 1; i >= 0; i--)
		{
			var ramp = live.Ramps[i];
			var through = ramp.End <= ramp.Start ? 1.0 : Math.Clamp((live.Clock - ramp.Start) / (ramp.End - ramp.Start), 0.0, 1.0);
			var value = (float)(ramp.From + (ramp.To - ramp.From) * through);
			if (ramp.Cue is { } cue)
				Player.Set(cue, value);
			else if (ramp.Track is { } track)
			{
				Player.HoldMacro(track, new Macro(ramp.Macro), value);
				live.Held[(track.Name, ramp.Macro)] = value;
			}
			if (through >= 1.0)
				live.Ramps.RemoveAt(i);
		}
		ShowCueValues();
	}

	// One event, as SongExport applies it: a jump, a cue set or ramped, a track's macro held, ramped or let go
	private void ApplyLive(LiveRun live, Song song, JObject e)
	{
		var over = e["over"]?.Str is { } overText && ExportDuration.Parse(overText) is { } duration ? Seconds(song, duration) : 0.0;
		if (e["jump"]?.Str is { } jump)
		{
			if (song.Sections.FirstOrDefault(s => s.Name == jump) is { } section)
				Player.Jump(section);
			return;
		}
		if (e["cue"]?.Str is { } reference)
		{
			if (CueFor(reference) is not { } cue || LiveValue(cue, e["to"]) is not { } to)
				return;
			live.Ramps.RemoveAll(r => r.Cue == cue);
			if (over > 0)
				live.Ramps.Add(new LiveRamp(cue, null, -1, Player.Get(cue), to, live.Clock, live.Clock + over));
			else
				Player.Set(cue, to);
			return;
		}
		if (e["track"]?.Str is not { } trackName || song.Tracks.FirstOrDefault(t => t.Name == trackName) is not { } track
			|| MacroId(e["macro"]) is not { } macro)
			return;
		live.Ramps.RemoveAll(r => r.Track == track && r.Macro == macro);
		if (e["to"] is not JNumber value)
		{
			Player.HoldMacro(track, new Macro(macro), null);
			live.Held.Remove((track.Name, macro));
			return;
		}
		if (over > 0)
			live.Ramps.Add(new LiveRamp(null, track, macro, live.Held.GetValueOrDefault((track.Name, macro)), (float)value.Value, live.Clock, live.Clock + over));
		else
		{
			Player.HoldMacro(track, new Macro(macro), (float)value.Value);
			live.Held[(track.Name, macro)] = (float)value.Value;
		}
	}

	// A duration as seconds at the song's tempo: a beat is the meter's
	private static double Seconds(Song song, ExportDuration duration)
	{
		var beat = 60.0 / song.Tempo * 4.0 / song.Meter.Unit;
		return duration.Unit switch
		{
			DurationUnit.Bars => duration.Amount * beat * song.Meter.Beats,
			DurationUnit.Beats => duration.Amount * beat,
			_ => duration.Amount,
		};
	}

	/// <summary>
	/// A macro as an export names it: its number, or its name in the project's manifest (<c>"bright"</c>); null for one it
	/// doesn't name.
	/// </summary>
	internal int? MacroId(JNode? macro)
	{
		if (macro is JNumber number && int.TryParse(number.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
			return id;
		if (macro?.Str is not { } name)
			return null;
		return ManifestMacros().TryGetValue(name, out var named) ? named : null;
	}

	/// <summary>The manifest's macros, by name: the ids an export's events may name them by.</summary>
	internal Dictionary<string, int> ManifestMacros()
	{
		var macros = new Dictionary<string, int>(StringComparer.Ordinal);
		if (_catalog.Root is not { } root || !Files.Exists(Path.Combine(root, EntryIds.Manifest)))
			return macros;
		try
		{
			foreach (var member in (JReader.Parse(Files.ReadText(Path.Combine(root, EntryIds.Manifest)))["macros"] as JObject)?.Members ?? [])
				if ((member.Value as JNumber ?? member.Value["number"] as JNumber) is { } n
					&& int.TryParse(n.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
					macros[member.Key] = id;
		}
		catch (Exception ex) when (ex is FormatError or IOException)
		{
		}
		return macros;
	}
}
