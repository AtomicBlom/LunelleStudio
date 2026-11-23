using System.Text.Json.Nodes;
using BinaryVibrance.Sirrow;
using BinaryVibrance.Sirrow.Synth.Chip;
using BinaryVibrance.Sirrow.Synth.Music;
using BinaryVibrance.Kaldra.Input;

namespace BinaryVibrance.SirrowStudio;

/// <summary>
/// The music workbench's model: plays a catalog's songs, rebuilds the one chosen whenever hot reload changes code and
/// swaps it in on the next bar, steers it (sections, mutes, solos, gains, forced parts, cues), plays patches and kits
/// from the keyboard, and fills a <see cref="WorkbenchViewModel"/> the views bind to. It needs no window, so it is tested
/// headless; an app hosts it with <c>WorkbenchView</c> over its view model.
/// </summary>
/// <remarks>Every call is for the thread that made the engine: the app's frame thread.</remarks>
public sealed partial class Workbench : IDisposable
{
	// The keyboard's keys, an octave from C, as a piano's white and black keys lie on a QWERTY board
	private static readonly Key[] Keys = [Key.A, Key.W, Key.S, Key.E, Key.D, Key.F, Key.T, Key.G, Key.Y, Key.H, Key.U, Key.J, Key.K];

	private static int _changes;

	private const double RefreshSeconds = 1.0 / 30.0;

	private readonly AudioEngine _audio;
	private readonly AudioBus _bus;
	private WorkbenchCatalog _catalog;
	private readonly Dictionary<string, bool> _muted = [];
	private readonly Dictionary<string, bool> _soloed = [];
	private readonly Dictionary<string, float> _gainDb = [];
	private readonly Dictionary<(string Section, string Placement), PlacementState> _forced = [];
	private readonly NoteHandle[] _held = new NoteHandle[Keys.Length];
	private int _seenChanges = _changes;
	private int _songIndex = -1;
	private Song? _song;
	private Scale? _scale;
	private bool _loop;
	private double _sinceRefresh;
	private (string Section, string Placement)? _selected;

	private SynthChip? _bench;
	private ChipInstrument _benchInstrument;
	private DrumKit? _benchKit;
	private int _benchPatch = -1;
	private int _benchKitIndex = -1;
	// The section a jump waits on until the next bar, or −1
	private int _queued = -1;
	// The patch and kit last chosen for their modes, kept while Arrange has the bench playing a track
	private int _patchChoice = -1;
	private int _kitChoice = -1;
	// The track the keyboard plays in Arrange, by name, so a rebuild finds it again
	private string? _dockTrack;
	// What the workbench's bus heard last, for the scope and spectrum
	private readonly AudioTap _tap = new(8192);

	/// <summary>
	/// Puts text on the clipboard (the patch designer's code), returning whether it could; null when the host has none.
	/// The host sets it: a desktop app to its window's clipboard.
	/// </summary>
	public Func<string, bool>? CopyText { get; set; }

	internal int ReadScope(Span<float> into) => _tap.Read(into);

	internal int SampleRate => _audio.SampleRate;

	// The designer's patch built after an edit: the keyboard's instrument plays it from its next note
	private void OnDesignerBuilt(Patch patch)
	{
		if (_bench is { } chip && _benchKit is null && _benchPatch >= 0)
			chip.Replace(_benchInstrument, patch);
		// An edit is heard in the song too, wherever a track plays this patch, once the edits pause a moment
		if (ViewModel.Designer.IsDirty && (uint)_benchPatch < (uint)_catalog.Patches.Count)
		{
			_heard = (_catalog.Patches[_benchPatch].Name, patch);
			_heardSince = 0;
		}
		// An edit, not the patch opening: written to its file a moment after the edits pause
		if (_patchFile is not null && ViewModel.Designer.IsDirty)
			_patchSince = 0;
	}

	// The designer's patch as edited, which the song plays in place of the catalog's of that name until the project is
	// compiled again with it saved; and how long since the last edit, until the song is built again with it
	private (string Name, Patch Patch)? _heard;
	private double _heardSince = -1;

	// How long the designer's edits pause before the song is built again with them: a slider dragged swaps it in once
	private const double HeardAfterSeconds = 0.15;

	// The patches the song last built asked for by name, so an edit to one of them builds it again
	private readonly HashSet<string> _songPatches = new(StringComparer.Ordinal);

	// The song's bindings, with the designer's patch in place of the catalog's while it is being edited
	private SongBindings Bindings(SongBindings bindings) => new()
	{
		Patch = name =>
		{
			_songPatches.Add(name);
			return _heard is { } heard && name == heard.Name ? heard.Patch : bindings.Patch(name);
		},
		Kit = bindings.Kit,
		Cue = bindings.Cue,
		Macro = bindings.Macro,
		Part = bindings.Part,
		Placement = bindings.Placement,
		Hook = bindings.Hook,
	};

	private void OnCopyRequested(string text) =>
		ViewModel.CopyStatus = CopyText is null ? "No clipboard is available. Select the text to copy it."
			: CopyText(text) ? $"Copied at {DateTime.Now:HH:mm:ss}" : "Couldn't copy to the clipboard.";

	// The beat sketcher's own player, made when it first loops, so the song's player is left alone
	private MusicPlayer? _sketchPlayer;

	// The sketch changed or started or stopped: it plays from the start, or is swapped in at the bar, or stops
	private void OnSketchEdited()
	{
		var sketch = ViewModel.Sketch;
		if (!sketch.IsLooping || sketch.Song() is not { } song)
		{
			if (_sketchPlayer is { IsPlaying: true } playing)
				playing.Stop(0.05f);
			sketch.ShowStep(-1);
			return;
		}
		_sketchPlayer ??= new MusicPlayer(_audio, _bus);
		if (_sketchPlayer.IsPlaying)
			_sketchPlayer.Swap(song);
		else
			_sketchPlayer.Play(song, fadeSeconds: 0f);
	}

	private void RefreshSketch()
	{
		if (_sketchPlayer is not { IsPlaying: true } player || player.Song is not { } song)
			return;
		var stepTicks = song.Meter.TicksPerBar / 16;
		ViewModel.Sketch.ShowStep(player.SectionTick < 0 ? -1 : player.SectionTick / stepTicks % 16);
	}

	/// <summary>A workbench playing on <paramref name="bus"/> of <paramref name="audio"/>, over <paramref name="catalog"/>; its first song is chosen, not yet playing.</summary>
	public Workbench(AudioEngine audio, AudioBus bus, WorkbenchCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(audio);
		ArgumentNullException.ThrowIfNull(catalog);
		_audio = audio;
		_bus = bus;
		_catalog = catalog;
		Player = new MusicPlayer(audio, bus);
		ViewModel = new WorkbenchViewModel(this);
		audio.AddEffect(bus, _tap);
		ViewModel.Designer.Built += OnDesignerBuilt;
		ViewModel.Designer.CopyRequested += OnCopyRequested;
		ViewModel.Designer.EjectRequested += EjectPatch;
		ViewModel.Files.CopyRequested += OnCopyRequested;
		ViewModel.Designer.BringBackRequested += BringBackPatch;
		ViewModel.Sketch.Edited += OnSketchEdited;
		ViewModel.Sketch.CopyRequested += OnCopyRequested;
		foreach (var (name, _) in catalog.Songs)
			ViewModel.AddSong(name);
		foreach (var (name, _) in catalog.Patches)
			ViewModel.AddPatch(name);
		foreach (var (name, _) in catalog.Kits)
			ViewModel.AddKit(name);
		foreach (var cue in catalog.Cues)
			ViewModel.Cues.Add(new CueViewModel(this, cue, Player.Get(cue)));
		if (catalog.Songs.Count > 0)
			SelectSong(0);
	}

	/// <summary>What the views bind to.</summary>
	public WorkbenchViewModel ViewModel { get; }

	/// <summary>The player the songs play on.</summary>
	public MusicPlayer Player { get; }

	/// <summary>A scale to play in place of each song's own, or null for the song's.</summary>
	public Scale? ScaleOverride
	{
		get => _scale;
		set
		{
			_scale = value;
			if (_song is { } song)
				ViewModel.ScaleText = Describe.Scale(value ?? song.Scale);
		}
	}

	/// <summary>Any thread: code has changed (hot reload says so), so every workbench rebuilds what it plays at its next <see cref="Update"/>.</summary>
	public static void CodeChanged() => Interlocked.Increment(ref _changes);

	/// <summary>
	/// Takes up a newly built catalog, a project compiled again: its songs, patches and kits replace the old by name, what
	/// was chosen stays chosen where the new one has it, each cue keeps its value by name (a new build makes new cue
	/// objects), and the song chosen is built again and swapped in at the next bar, as a code change does.
	/// </summary>
	public void UseCatalog(WorkbenchCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(catalog);
		string? Name<T>(IReadOnlyList<(string Name, Func<T> Build)> list, int index) => (uint)index < (uint)list.Count ? list[index].Name : null;
		var song = Name(_catalog.Songs, _songIndex);
		var patch = Name(_catalog.Patches, _benchPatch);
		var kit = Name(_catalog.Kits, _benchKitIndex);
		var patchChoice = Name(_catalog.Patches, _patchChoice);
		var kitChoice = Name(_catalog.Kits, _kitChoice);
		var cues = ViewModel.Cues.ToDictionary(c => c.Name, c => Player.Get(c.Cue));

		_catalog = catalog;
		_heard = null;
		_heardSince = -1;
		WorkbenchViewModel.ReplaceChoices(ViewModel.Songs, [.. catalog.Songs.Select(s => s.Name)], ViewModel.AddSong);
		WorkbenchViewModel.ReplaceChoices(ViewModel.Patches, [.. catalog.Patches.Select(s => s.Name)], ViewModel.AddPatch);
		WorkbenchViewModel.ReplaceChoices(ViewModel.Kits, [.. catalog.Kits.Select(s => s.Name)], ViewModel.AddKit);
		ViewModel.FilterLibrary();
		ViewModel.Cues.Clear();
		foreach (var cue in catalog.Cues)
		{
			var value = cues.TryGetValue(cue.Name, out var was) ? was : Player.Get(cue);
			Player.Set(cue, value);
			ViewModel.Cues.Add(new CueViewModel(this, cue, value));
		}

		_songIndex = Index(catalog.Songs, song);
		_benchPatch = Index(catalog.Patches, patch);
		_benchKitIndex = Index(catalog.Kits, kit);
		_patchChoice = Index(catalog.Patches, patchChoice);
		_kitChoice = Index(catalog.Kits, kitChoice);
		if (_songIndex < 0 && catalog.Songs.Count > 0)
			_songIndex = 0;
		ViewModel.ShowSelectedSong(_songIndex);
		ViewModel.ShowBench(_benchPatch, _benchKitIndex);
		Rebuild();
		RebuildBench();
		if (ViewModel.Mode == WorkbenchMode.Files)
			RefreshFiles();

		OpenAwaited();
	}

	/// <summary>
	/// Shows a project's compile in the build pill: how long a good one took, or a failed one's errors (the last that built
	/// plays on).
	/// </summary>
	public void ReportBuild(IReadOnlyList<string> errors, TimeSpan took)
	{
		ArgumentNullException.ThrowIfNull(errors);
		if (errors.Count == 0)
		{
			ViewModel.Error = "";
			ViewModel.BuildText = $"compiled {DateTime.Now:HH:mm:ss} in {took.TotalMilliseconds:0} ms";
			return;
		}
		ViewModel.Error = string.Join("\n", errors.Take(8)) + (errors.Count > 8 ? $"\n… and {errors.Count - 8} more" : "");
		ViewModel.BuildText = "Build failed · last build still playing";
	}

	/// <summary>Chooses a song and builds it; if one was playing, it plays.</summary>
	public void SelectSong(int index)
	{
		if ((uint)index >= (uint)_catalog.Songs.Count)
			throw new ArgumentOutOfRangeException(nameof(index), index, "No such song in the catalog.");
		var playing = Player.IsPlaying;
		_songIndex = index;
		_song = null;
		_selected = null;
		_selectedIndex = -1;
		ViewModel.Roll.ShowNone("Select a part in a lane to edit it here.");
		ViewModel.Pattern.Clear();
		ViewModel.Inspected = null;
		_inspected = null;
		_muted.Clear();
		_soloed.Clear();
		_gainDb.Clear();
		_forced.Clear();
		_queued = -1;
		ViewModel.ShowSelectedSong(index);

		Rebuild(swap: false);
		if (playing && _song is not null)
			Play();
	}

	/// <summary>Plays the song chosen from its start.</summary>
	public void Play()
	{
		if (_song is null)
			Rebuild(swap: false);
		if (_song is not { } song)
			return;
		Player.Play(song, _scale, fadeSeconds: 0.3f);
		foreach (var track in song.Tracks)
		{
			if (_muted.GetValueOrDefault(track.Name))
				Player.Mute(track, true);
			if (_soloed.GetValueOrDefault(track.Name))
				Player.Solo(track, true);
			if (_gainDb.TryGetValue(track.Name, out var db))
				Player.SetTrackGain(track, db);
		}
		foreach (var section in song.Sections)
			foreach (var placement in section.Placements)
				if (_forced.TryGetValue((section.Name, placement.Name), out var state) && state != PlacementState.Auto)
					Player.Force(placement, state);
		if (_loop)
			Player.LoopSection(true);
		ViewModel.ShowPlaying(true);
	}

	/// <summary>Stops the song.</summary>
	public void Stop()
	{
		Player.Stop(0.3f);
		ViewModel.ShowPlaying(false);
		ViewModel.PositionText = "stopped";
	}

	/// <summary>
	/// Builds the chosen song again (its factory may return new code) and swaps it in at the next bar if one plays. A song
	/// that fails to build shows its error, and the one playing plays on.
	/// </summary>
	public void Rebuild() => Rebuild(swap: true);

	private void Rebuild(bool swap)
	{
		if (_songIndex < 0)
			return;
		var (name, build) = _catalog.Songs[_songIndex];
		Song song;
		try
		{
			OpenEditor();
			_songPatches.Clear();
			song = _editor is { } editor && _source is { } source
				? editor.Build(Bindings(source.Bindings))
				: build() ?? throw new InvalidOperationException("Its factory returned no song.");
		}
		catch (Exception ex)
		{
			ViewModel.Error = $"{name}: {ex.Message}";
			ViewModel.BuildText = _song is null ? "Build failed" : "Build failed · last build still playing";
			Console.WriteLine($"[workbench] {name} didn't build: {ex.Message}");
			return;
		}
		ViewModel.Error = "";
		ViewModel.BuildText = _editorNote ?? $"built {DateTime.Now:HH:mm:ss}";
		_editorNote = null;
		_song = song;
		if (swap && Player.IsPlaying)
			Player.Swap(song);
		Fill(song);
		Console.WriteLine($"[workbench] rebuilt {name}");
	}

	// The view model's song from a newly built one: its sections, its tracks with their parts by section, its cues and what
	// reads them, each with what was set by name before
	private void Fill(Song song)
	{
		ViewModel.SongName = song.Name;
		ViewModel.Song = new SongViewModel(this, song);
		ViewModel.TempoText = $"{song.Tempo:0} bpm";
		ViewModel.ScaleText = Describe.Scale(_scale ?? song.Scale);
		ViewModel.Sections.Clear();
		foreach (var section in song.Sections)
			ViewModel.Sections.Add(new SectionViewModel(this, section.Index, section.Name, section.Bars, section.ChordText)
			{
				Progression = section.Progression,
				Scale = _scale ?? song.Scale,
				Exits = [.. section.Exits.Select(e => (e.When is { } when ? ConditionText.Write(when) : null, e.To.Name))],
			});
		ViewModel.Tracks.Clear();
		foreach (var track in song.Tracks)
		{
			var row = new TrackViewModel(this, track.Name, _muted.GetValueOrDefault(track.Name), _soloed.GetValueOrDefault(track.Name),
				_gainDb.GetValueOrDefault(track.Name))
			{
				Instrument = track.Patch is { } patch ? $"patch {patch.Name}" : track.Kit is { } kit ? $"kit {kit.Name}" : "",
				Index = IndexOfTrack(song, track),
				InstrumentName = track.Patch?.Name ?? track.Kit?.Name ?? "",
				IsKit = track.Kit is not null,
			};
			foreach (var section in song.Sections)
			{
				var cell = new LaneCellViewModel(section.Index, ArrangeLayout.SectionWidth(section.Bars)) { Track = row };
				foreach (var placement in section.Placements)
					if (placement.Track == track)
					{
						var clip = Clip(placement);
						row.Placements.Add(clip);
						cell.Placements.Add(clip);
					}
				row.Cells.Add(cell);
			}
			ViewModel.Tracks.Add(row);
		}
		ViewModel.HasTracks = ViewModel.Tracks.Count > 0;
		foreach (var cue in song.Cues)
			if (!ViewModel.Cues.Any(c => c.Cue == cue))
				ViewModel.Cues.Add(new CueViewModel(this, cue, Player.Get(cue)));
		var used = song.Cues;
		foreach (var cue in ViewModel.Cues)
		{
			cue.IsUsed = used.Contains(cue.Cue);
			cue.Readers.Clear();
			foreach (var section in song.Sections)
			{
				foreach (var placement in section.Placements)
					if (placement.Condition is { } condition)
						foreach (var (level, above) in Levels(condition, cue.Cue))
							cue.Readers.Add(new CueReaderViewModel(level, above, $"{section.Name} · {placement.Name}"));
				foreach (var exit in section.Exits)
					if (exit.When is { } when)
						foreach (var (level, above) in Levels(when, cue.Cue))
							cue.Readers.Add(new CueReaderViewModel(level, above, $"{section.Name} → {exit.To.Name}"));
			}
		}
		if (_selected is { } selected && FindPlacement(selected) is { } again)
		{
			ShowPattern(again);
			ShowRoll(again);
		}
		// What the inspector showed, found again in the song rebuilt
		ViewModel.Inspected = _inspected switch
		{
			int section when section < ViewModel.Sections.Count => ViewModel.Sections[section],
			InspectedTrack track => ViewModel.Tracks.FirstOrDefault(t => t.Name == track.Name),
			InspectedSong => ViewModel.Song,
			ValueTuple<string, string> key => ViewModel.Tracks.SelectMany(t => t.Placements).FirstOrDefault(p => (p.Placement.Section.Name, p.Placement.Name) == key),
			_ => null,
		};
		if (ViewModel.Inspected is SectionViewModel inspected)
			inspected.IsInspected = true;
		if (ViewModel.Inspected is TrackViewModel inspectedTrack)
			inspectedTrack.IsInspected = true;
		if (ViewModel.Inspected is SongViewModel inspectedSong)
			inspectedSong.IsInspected = true;
		RefreshConditions();
	}

	// What the inspector shows, by what survives a rebuild: a section's index, a track's name, or a placement's section and
	// name
	private object? _inspected;

	private sealed record InspectedTrack(string Name);

	/// <summary>Puts the inspector away until something is chosen again.</summary>
	internal void CloseInspector()
	{
		_inspected = null;
		Unmark();
		ViewModel.Inspected = null;
	}

	/// <summary>Shows a section in the inspector.</summary>
	internal void Inspect(SectionViewModel section)
	{
		_inspected = section.Index;
		Unmark();
		section.IsInspected = true;
		ViewModel.Inspected = section;
	}

	/// <summary>Shows a track in the inspector: its name, and the instrument it plays.</summary>
	internal void Inspect(TrackViewModel track)
	{
		_inspected = new InspectedTrack(track.Name);
		Unmark();
		track.IsInspected = true;
		ViewModel.Inspected = track;
	}

	private sealed record InspectedSong;

	/// <summary>Shows the song itself in the inspector, its tempo, key and scale, in Arrange where the inspector is.</summary>
	internal void InspectSong()
	{
		if (ViewModel.Song is not { } song)
			return;
		if (ViewModel.Mode != WorkbenchMode.Arrange)
			SetMode(WorkbenchMode.Arrange);
		_inspected = new InspectedSong();
		Unmark();
		song.IsInspected = true;
		ViewModel.Inspected = song;
	}

	// No section or track lit as the one the inspector shows
	private void Unmark()
	{
		if (ViewModel.Song is { } song)
			song.IsInspected = false;

		foreach (var section in ViewModel.Sections)
			section.IsInspected = false;
		foreach (var track in ViewModel.Tracks)
			track.IsInspected = false;
	}

	/// <summary>
	/// Opens what a track plays to change it: its patch in the designer, whose edits the song plays from the next bar after
	/// they pause, or its kit in Kit.
	/// </summary>
	internal void OpenInstrument(TrackViewModel track)
	{
		var index = track.IsKit
			? _catalog.Kits.ToList().FindIndex(k => k.Name == track.InstrumentName)
			: _catalog.Patches.ToList().FindIndex(p => p.Name == track.InstrumentName);
		if (index < 0)
		{
			ViewModel.Error = $"{track.InstrumentName} isn't in the project's catalog, so it can't be opened.";
			return;
		}
		if (track.IsKit)
			ChooseKit(index);
		else
			ChoosePatch(index);
	}

	// A part as a clip in Arrange: everything it plays through its section, what it plays, whether it is the one chosen
	private PlacementViewModel Clip(Placement placement)
	{
		SectionPattern pattern;
		try
		{
			pattern = Player.SectionPatternOf(placement, _scale);
		}
		catch (ArgumentException)
		{
			pattern = new SectionPattern(0, 0, -1, 0, null, []);
		}
		var events = pattern.Events;
		var kit = pattern.Kit;
		List<float> rows = kit is not null
			? [.. events.Select(e => (float)e.Piece).Distinct().Order()]
			: [.. events.Select(e => MathF.Round(e.Note)).Distinct().OrderDescending()];
		var key = (placement.Section.Name, placement.Name);
		return new PlacementViewModel(this, placement, Describe.Placement(placement), _forced.GetValueOrDefault(key))
		{
			KindText = (kit is not null ? "hits" : "notes") + (placement.IsOnPhrase ? " · starts on phrase" : ""),
			Notes = [.. events.Select(e => new PatternNote(e.Tick, e.Length, rows.IndexOf(kit is not null ? e.Piece : MathF.Round(e.Note)), e.Velocity))],
			LoopTicks = pattern.LoopTicks,
			LengthTicks = pattern.LengthTicks,
			FillTick = pattern.FillTick,
			TrackIndex = IndexOfTrack(placement.Section.Song, placement.Track),
			When = placement.Condition is { } condition ? ConditionText.Write(condition) : null,
			Rows = rows.Count,
			IsSelected = _selected == key,
		};
	}

	// A track's place in its song, which gives it its hue
	private static int IndexOfTrack(Song song, SongTrack track)
	{
		var tracks = song.Tracks;
		for (var i = 0; i < tracks.Count; i++)
			if (tracks[i] == track)
				return i;
		return 0;
	}

	// Each level a condition compares a cue with, and whether it waits for the cue above it; a not turns the comparison over
	private static IEnumerable<(float Level, bool Above)> Levels(Condition condition, CueBase cue) => condition switch
	{
		AboveCondition a when a.Cue == cue => [(a.Level, true)],
		BelowCondition b when b.Cue == cue => [(b.Level, false)],
		AndCondition both => [.. Levels(both.A, cue), .. Levels(both.B, cue)],
		OrCondition either => [.. Levels(either.A, cue), .. Levels(either.B, cue)],
		NotCondition n => [.. Levels(n.A, cue).Select(l => (l.Level, !l.Above))],
		_ => [],
	};

	// Whether each part's condition holds, and each level a cue is waited on at, for the cues as they are now
	private void RefreshConditions()
	{
		foreach (var row in ViewModel.Tracks)
			foreach (var clip in row.Placements)
				clip.ConditionMet = clip.Placement.Condition?.IsMet(Player.Get) ?? true;
		foreach (var cue in ViewModel.Cues)
		{
			var value = Player.Get(cue.Cue);
			foreach (var reader in cue.Readers)
				reader.IsMet = reader.Above ? value >= reader.Level : value <= reader.Level;
		}
	}

	// Where the song is in Arrange: how far through each section, which column the playhead crosses, a jump waiting, and
	// what plays next
	private void RefreshArrange(Song song, MusicPosition position)
	{
		var progress = Math.Clamp(Player.SectionTick / (float)Math.Max(1, song.Sections[position.SectionIndex].Bars * song.Meter.TicksPerBar), 0f, 1f);
		if (_queued == position.SectionIndex)
			_queued = -1;
		var nextExit = Player.NextExit;
		foreach (var section in ViewModel.Sections)
		{
			var current = section.Index == position.SectionIndex;
			section.Progress = current ? progress : 0f;
			section.IsQueued = section.Index == _queued;
			section.PlayingBar = current ? position.Bar - 1 : -1;
			section.NextExit = current ? nextExit : -1;
		}
		foreach (var row in ViewModel.Tracks)
			foreach (var cell in row.Cells)
			{
				cell.IsCurrent = cell.Section == position.SectionIndex;
				cell.Progress = cell.IsCurrent ? progress : 0f;
			}
		ViewModel.NextText = _queued >= 0 && _queued < song.Sections.Count
			? $"Next: {song.Sections[_queued].Name} at the next bar"
			: _loop
				? $"Next: {position.Section} again (looping)"
				: Player.NextSection is { } next ? $"Next: {next.Name}" : "";
	}

	private void ClearArrange()
	{
		foreach (var section in ViewModel.Sections)
		{
			section.Progress = 0f;
			section.IsQueued = false;
			section.PlayingBar = -1;
			section.NextExit = -1;
		}
		foreach (var row in ViewModel.Tracks)
			foreach (var cell in row.Cells)
			{
				cell.IsCurrent = false;
				cell.Progress = 0f;
			}
		ViewModel.NextText = "";
		_queued = -1;
	}

	/// <summary>Once a frame: rebuilds after a code change, hears the player, and refreshes what the view model shows (at most 30 times a second).</summary>
	public void Update(double deltaSeconds)
	{
		var changes = Volatile.Read(ref _changes);
		if (changes != _seenChanges)
		{
			_seenChanges = changes;
			Rebuild();
			RebuildBench();
		}
		try
		{
			Player.Update();
			_sketchPlayer?.Update();
		}
		catch (InvalidOperationException ex)
		{
			ViewModel.Error = ex.Message;
			Console.WriteLine($"[workbench] {ex.Message}");
		}
		if (_patchSince >= 0 && (_patchSince += deltaSeconds) >= SongEditor.SaveAfterSeconds)
			SavePatch();
		if (_heardSince >= 0 && (_heardSince += deltaSeconds) >= HeardAfterSeconds)
		{
			_heardSince = -1;
			if (_heard is { } heard && _songPatches.Contains(heard.Name))
				Rebuild(swap: true);
		}
		if (_editor?.Update(deltaSeconds) == true)
			ViewModel.BuildText = $"saved {Path.GetFileName(_editor.Path)} {DateTime.Now:HH:mm:ss}";
		// Thirty refreshes a second, on average: what is left over carries to the next, so a frame rate that doesn't divide it
		// evenly still refreshes thirty times rather than every third frame at 60
		_sinceRefresh += deltaSeconds;
		if (_sinceRefresh >= RefreshSeconds)
		{
			_sinceRefresh = Math.Min(_sinceRefresh - RefreshSeconds, RefreshSeconds);
			Refresh();
			RefreshSketch();
			ViewModel.RaiseRefreshed();
		}
		ViewModel.RaiseFrame();
	}

	private void Refresh()
	{
		var stats = Player.Stats;
		ViewModel.StatsText = $"{stats.ActiveVoices} voices · {stats.ActiveOperators} operators · {_audio.Stats.RenderLoad * 100:0}% load";
		RefreshConditions();
		if (!Player.IsPlaying || Player.Song is not { } song)
		{
			foreach (var section in ViewModel.Sections)
			{
				section.ShowCurrent(false);
				section.Loops = false;
			}
			ClearArrange();
			ViewModel.Pattern.PlayheadTick = -1;
			return;
		}
		var position = Player.Position;
		ViewModel.PositionText = $"{position.Section} · bar {position.Bar} · beat {position.Beat}";
		foreach (var section in ViewModel.Sections)
		{
			section.ShowCurrent(section.Index == position.SectionIndex);
			section.Loops = section.IsCurrent && _loop;
		}
		if (song == _song)
			RefreshArrange(song, position);
		foreach (var row in ViewModel.Tracks)
		{
			var track = TrackOf(row.Name);
			row.Peak = track is null ? 0f : Player.TrackPeak(track);
			foreach (var placement in row.Placements)
			{
				var mine = placement.Placement.Section.Song == song;
				placement.IsActive = mine && Player.IsActive(placement.Placement);
				var at = mine ? Player.PositionOf(placement.Placement) : default;
				placement.PassPlaying = at.IsPlaying ? at.Pass : -1;
				placement.SectionTick = at.IsPlaying ? Player.SectionTick : -1;
			}
		}
		ViewModel.Pattern.PlayheadTick = _selected is { } selected && FindPlacement(selected) is { } shown
			&& shown.Section.Index == position.SectionIndex && Player.IsActive(shown) && ViewModel.Pattern.LengthTicks > 0
			? Player.SectionTick % ViewModel.Pattern.LengthTicks
			: -1;
	}

	private SongTrack? TrackOf(string name)
	{
		if (Player.Song is not { } song)
			return null;
		foreach (var track in song.Tracks)
			if (track.Name == name)
				return track;
		return null;
	}

	private Placement? FindPlacement((string Section, string Placement) key)
	{
		if (_song is not { } song)
			return null;
		foreach (var section in song.Sections)
			if (section.Name == key.Section)
			{
				// The one chosen by where it stands, where two share a name (a track's two parts in a section)
				if ((uint)_selectedIndex < (uint)section.Placements.Count && section.Placements[_selectedIndex].Name == key.Placement)
					return section.Placements[_selectedIndex];
				foreach (var placement in section.Placements)
					if (placement.Name == key.Placement)
						return placement;
			}
		return null;
	}

	internal void SetMute(string track, bool muted)
	{
		_muted[track] = muted;
		if (Player.IsPlaying && TrackOf(track) is { } playing)
			Player.Mute(playing, muted);
	}

	internal void SetSolo(string track, bool solo)
	{
		_soloed[track] = solo;
		if (Player.IsPlaying && TrackOf(track) is { } playing)
			Player.Solo(playing, solo);
	}

	internal void SetGain(string track, float db)
	{
		_gainDb[track] = db;
		if (Player.IsPlaying && TrackOf(track) is { } playing)
			Player.SetTrackGain(playing, db);
	}

	internal void Force(PlacementViewModel row, PlacementState state)
	{
		var placement = row.Placement;
		_forced[(placement.Section.Name, placement.Name)] = state;
		if (Player.IsPlaying && placement.Section.Song == Player.Song)
			Player.Force(placement, state);
	}

	internal void SetCue(CueBase cue, float value) => Player.Set(cue, value);

	internal void SetLoop(bool loop)
	{
		_loop = loop;
		if (Player.IsPlaying)
			Player.LoopSection(loop);
	}

	internal void JumpTo(int section)
	{
		if (Player.IsPlaying && Player.Song is { } song && (uint)section < (uint)song.Sections.Count)
		{
			Player.Jump(song.Sections[section]);
			_queued = section;
		}
	}

	internal void SelectPlacement(PlacementViewModel row)
	{
		_selected = (row.Placement.Section.Name, row.Placement.Name);
		_selectedIndex = IndexIn(row.Placement);
		_inspected = _selected;
		Unmark();
		ViewModel.Inspected = row;
		foreach (var track in ViewModel.Tracks)
			foreach (var clip in track.Placements)
				clip.IsSelected = clip == row;
		ShowPattern(row.Placement);
		ShowRoll(row.Placement);
		if (ViewModel.Mode == WorkbenchMode.Arrange)
			DockTrack(row.Placement.Track);
	}

	// A placement's notes over one pass of its part: a row per pitch (highest first) or per drum piece
	private void ShowPattern(Placement placement)
	{
		var pattern = Player.Pattern(placement, _scale);
		var events = pattern.Events;
		var kit = pattern.Kit;
		List<float> rows;
		string[] labels;
		if (kit is not null)
		{
			rows = [.. events.Select(e => (float)e.Piece).Distinct().Order()];
			labels = [.. rows.Select(r => kit.Pieces[(int)r].Piece.Id)];
		}
		else
		{
			rows = [.. events.Select(e => MathF.Round(e.Note)).Distinct().OrderDescending()];
			labels = [.. rows.Select(Describe.NoteName)];
		}
		var notes = events.Select(e => new PatternNote(e.Tick, e.Length, rows.IndexOf(kit is not null ? e.Piece : MathF.Round(e.Note)), e.Velocity)).ToArray();
		ViewModel.Pattern.Show($"{placement.Name} · {placement.Section.Name}", pattern.LoopTicks, pattern.TicksPerBar, notes, labels);
	}

	/// <summary>Chooses a patch for the bench, which the keyboard plays.</summary>
	public void SelectPatch(int index)
	{
		if ((uint)index >= (uint)_catalog.Patches.Count)
			throw new ArgumentOutOfRangeException(nameof(index), index, "No such patch in the catalog.");
		var (name, build) = _catalog.Patches[index];
		Patch patch;
		try
		{
			patch = build();
		}
		catch (Exception ex)
		{
			ViewModel.Error = $"{name}: {ex.Message}";
			Console.WriteLine($"[workbench] {name} didn't build: {ex.Message}");
			return;
		}
		var chip = NewBench();
		_benchInstrument = chip.AddInstrument(patch);
		_benchKit = null;
		_benchPatch = _patchChoice = index;
		_benchKitIndex = -1;
		_dockTrack = null;
		ViewModel.DockLabel = $"patch {name}";

		ViewModel.ShowBench(index, -1);
		ViewModel.ShowPads([]);
		foreach (var macro in ViewModel.Macros)
			chip.SetMacro(_benchInstrument, new Macro(macro.Index), macro.Value);
		// The same patch built again after a code change keeps the designer's edits; another opens afresh
		ViewModel.Designer.Load(patch, _catalog.MacroName, keepEdits: true, names: OpenPatchFile(name));

		ShowPatchUsage(name);
	}

	/// <summary>Chooses a kit for the bench, whose pads (and keys) play its pieces.</summary>
	public void SelectKit(int index)
	{
		if ((uint)index >= (uint)_catalog.Kits.Count)
			throw new ArgumentOutOfRangeException(nameof(index), index, "No such kit in the catalog.");
		var (name, build) = _catalog.Kits[index];
		DrumKit kit;
		try
		{
			kit = build();
		}
		catch (Exception ex)
		{
			ViewModel.Error = $"{name}: {ex.Message}";
			Console.WriteLine($"[workbench] {name} didn't build: {ex.Message}");
			return;
		}
		var chip = NewBench();
		_benchInstrument = chip.AddKit(kit);
		_benchKit = kit;
		_benchKitIndex = _kitChoice = index;
		_benchPatch = -1;
		_dockTrack = null;
		ViewModel.DockLabel = $"kit {name}";

		ViewModel.ShowBench(-1, index);
		ViewModel.ShowPads(kit.Pieces.Select(piece => piece.Piece.Id));
		ViewModel.Sketch.Load(kit);
	}

	// A new chip for the bench on the workbench's bus, the old one let go
	private SynthChip NewBench()
	{
		if (_bench is { } old)
			_audio.Stop(old, 0.1f);
		var chip = new SynthChip(new ChipOptions { Voices = 16 });
		_audio.Play(chip, _bus);
		_bench = chip;
		return chip;
	}

	// After a code change, the keyboard's instrument built again: the track it plays in Arrange, found again by name, or the
	// patch or kit it plays
	private void RebuildBench()
	{
		if (ViewModel.Mode == WorkbenchMode.Arrange && _dockTrack is { } name && _song?.Tracks.FirstOrDefault(t => t.Name == name) is { } track)
			DockTrack(track);
		else if (_benchPatch >= 0)
			SelectPatch(_benchPatch);
		else if (_benchKitIndex >= 0)
			SelectKit(_benchKitIndex);
	}

	/// <summary>
	/// Gives the middle to <paramref name="mode"/>, and the keyboard to what it has chosen: the part chosen's track in
	/// Arrange, the patch last chosen in Patch, the kit in Kit (the catalog's first when none was).
	/// </summary>
	public void SetMode(WorkbenchMode mode)
	{
		ViewModel.ShowMode(mode);
		switch (mode)
		{
			case WorkbenchMode.Arrange:
				if (_selected is { } selected && FindPlacement(selected) is { } placement)
					DockTrack(placement.Track);
				break;
			case WorkbenchMode.Patch when _catalog.Patches.Count > 0:
				SelectPatch(Math.Max(0, _patchChoice));
				break;
			case WorkbenchMode.Kit when _catalog.Kits.Count > 0:
				SelectKit(Math.Max(0, _kitChoice));
				break;
			case WorkbenchMode.Files:
				RefreshFiles();
				break;
		}
	}

	/// <summary>A song chosen by hand (the library, the palette): it is built, and Arrange shows it.</summary>
	public void ChooseSong(int index)
	{
		SelectSong(index);
		SetMode(WorkbenchMode.Arrange);
		ViewModel.Chose();
	}

	/// <summary>A patch chosen by hand: the keyboard plays it, and the patch bench shows it.</summary>
	public void ChoosePatch(int index)
	{
		_patchChoice = index;
		SetMode(WorkbenchMode.Patch);
		ViewModel.Chose();
	}

	/// <summary>A kit chosen by hand: the keyboard and pads play it, and Kit shows it.</summary>
	public void ChooseKit(int index)
	{
		_kitChoice = index;
		SetMode(WorkbenchMode.Kit);
		ViewModel.Chose();
	}

	// The keyboard plays a track of the song: its patch, or its kit's pieces
	private void DockTrack(SongTrack track)
	{
		var chip = NewBench();
		if (track.Kit is { } kit)
		{
			_benchInstrument = chip.AddKit(kit);
			_benchKit = kit;
		}
		else if (track.Patch is { } patch)
		{
			_benchInstrument = chip.AddInstrument(patch);
			_benchKit = null;
		}
		_dockTrack = track.Name;
		ViewModel.DockLabel = $"{track.Name} · {track.Kit?.Name ?? track.Patch?.Name}";
	}

	// Everything the command palette offers, built when it opens: the catalog, the song's sections, and commands
	internal IEnumerable<PaletteItemViewModel> PaletteCommands()
	{
		for (var i = 0; i < _catalog.Songs.Count; i++)
		{
			var index = i;
			yield return new PaletteItemViewModel("Song", _catalog.Songs[i].Name, () => ChooseSong(index));
		}
		for (var i = 0; i < _catalog.Patches.Count; i++)
		{
			var index = i;
			yield return new PaletteItemViewModel("Patch", _catalog.Patches[i].Name, () => ChoosePatch(index));
		}
		for (var i = 0; i < _catalog.Kits.Count; i++)
		{
			var index = i;
			yield return new PaletteItemViewModel("Kit", _catalog.Kits[i].Name, () => ChooseKit(index));
		}
		if (_song is { } song)
			foreach (var section in song.Sections)
			{
				var index = section.Index;
				yield return new PaletteItemViewModel("Section", $"Jump to {section.Name}", () => JumpTo(index));
			}
		yield return new PaletteItemViewModel("Command", Player.IsPlaying ? "Stop" : "Play", () =>
		{
			if (Player.IsPlaying)
				Stop();
			else
				Play();
		});
		yield return new PaletteItemViewModel("Command", _loop ? "Stop looping section" : "Loop current section", () => ViewModel.LoopSection = !_loop);
		yield return new PaletteItemViewModel("Command", ViewModel.LibraryPinned ? "Unpin the library" : "Pin the library", () => ViewModel.LibraryPinned = !ViewModel.LibraryPinned);
		yield return new PaletteItemViewModel("Mode", "Arrange", () => SetMode(WorkbenchMode.Arrange));
		yield return new PaletteItemViewModel("Mode", "Patch", () => SetMode(WorkbenchMode.Patch));
		yield return new PaletteItemViewModel("Mode", "Kit", () => SetMode(WorkbenchMode.Kit));
	}

	/// <summary>Starts a note on the bench's patch (or hits the kit's piece the note falls on); returns it, to let go.</summary>
	public NoteHandle AuditionOn(float note, float velocity)
	{
		if (_bench is not { } chip)
			return default;
		if (_benchKit is { } kit)
		{
			if (kit.Pieces.Count > 0)
				chip.Hit(_benchInstrument, kit.Pieces[(int)MathF.Round(note) % 12 % kit.Pieces.Count].Piece, velocity);
			return default;
		}
		return chip.NoteOn(_benchInstrument, Math.Clamp(note, 0f, 127f), Math.Clamp(velocity, 0f, 1f));
	}

	/// <summary>Lets a note started with <see cref="AuditionOn"/> go.</summary>
	public void AuditionOff(NoteHandle note)
	{
		if (_bench is { } chip && note.Id != 0)
			chip.NoteOff(note);
	}

	/// <summary>Hits a piece of the bench's kit.</summary>
	public void AuditionHit(DrumPiece piece, float velocity)
	{
		if (_bench is { } chip && _benchKit is not null)
			chip.Hit(_benchInstrument, piece, Math.Clamp(velocity, 0f, 1f));
	}

	internal void SetBenchMacro(int index, float value)
	{
		if (_bench is { } chip && _benchKit is null && _benchPatch >= 0)
			chip.SetMacro(_benchInstrument, new Macro(index), value);
	}

	/// <summary>
	/// Once a frame, the keys: Ctrl K opens the command palette and Escape closes what is open (the palette, the library
	/// over the view, a build's error). While a text field has the keys (the palette, the library's filter), nothing else is
	/// taken. Otherwise Space plays or stops; A W S E D F T G Y H U J K are an octave of piano keys from C (Z and X move it
	/// an octave); 1 to 9 jump to that section.
	/// </summary>
	public void HandleInput(InputSnapshot input)
	{
		ArgumentNullException.ThrowIfNull(input);
		var control = input.Down(Key.LeftControl) || input.Down(Key.RightControl);
		ViewModel.ShiftHeld = input.Down(Key.LeftShift) || input.Down(Key.RightShift);
		if (control && input.Pressed(Key.K))
		{
			ViewModel.PaletteOpen = !ViewModel.PaletteOpen;
			return;
		}
		if (input.Pressed(Key.Escape))
		{
			if (ViewModel.PaletteOpen)
				ViewModel.PaletteOpen = false;
			else if (ViewModel.LibraryFlyout)
				ViewModel.LibraryOpen = false;
			else if (ViewModel.ErrorOpen)
				ViewModel.ErrorOpen = false;
			return;
		}
		if (control && !ViewModel.IsTyping && input.Pressed(Key.Z))
		{
			if (input.Down(Key.LeftShift) || input.Down(Key.RightShift))
				RedoEdit();
			else
				UndoEdit();
		}
		if (control && !ViewModel.IsTyping && input.Pressed(Key.Y))
			RedoEdit();
		if (!control && !ViewModel.IsTyping && input.Pressed(Key.Delete))
			ViewModel.Roll.DeleteCommand.Execute(null);
		if (ViewModel.IsTyping || ViewModel.PaletteOpen || control)
		{
			ReleaseHeld();
			return;
		}
		if (input.Pressed(Key.Space))
		{
			if (Player.IsPlaying)
				Stop();
			else
				Play();
		}
		if (input.Pressed(Key.Z))
			ViewModel.Octave--;
		if (input.Pressed(Key.X))
			ViewModel.Octave++;
		for (var k = 0; k < Keys.Length; k++)
		{
			if (input.Pressed(Keys[k]))
				_held[k] = AuditionOn(12 * (ViewModel.Octave + 1) + k, ViewModel.Velocity);
			else if (input.Released(Keys[k]))
			{
				AuditionOff(_held[k]);
				_held[k] = default;
			}
		}
		for (var d = 0; d < 9; d++)
			if (input.Pressed(Key.Digit1 + d))
				JumpTo(d);
	}

	// Lets every key held on the computer's keyboard go, as typing starts
	private void ReleaseHeld()
	{
		for (var k = 0; k < _held.Length; k++)
			if (_held[k].Id != 0)
			{
				AuditionOff(_held[k]);
				_held[k] = default;
			}
	}

	/// <summary>What the workbench is doing, to save.</summary>
	public WorkbenchState CaptureState()
	{
		var state = new WorkbenchState
		{
			Song = _songIndex >= 0 ? _catalog.Songs[_songIndex].Name : null,
			Playing = Player.IsPlaying,
			LoopSection = _loop,
			Section = Player.IsPlaying ? Player.Position.Section : null,
			ScaleRoot = _scale?.Root.ToString(),
			ScaleName = _scale?.Name,
			Patch = _benchPatch >= 0 ? _catalog.Patches[_benchPatch].Name : null,
			Kit = _benchKitIndex >= 0 ? _catalog.Kits[_benchKitIndex].Name : null,
			Octave = ViewModel.Octave,
			PartPanelHeight = ViewModel.PartPanelHeight,
			Velocity = ViewModel.Velocity,
			Mode = ViewModel.Mode.ToString(),
			LibraryPinned = ViewModel.LibraryPinned,
			SelectedPart = _selected is { } selected ? $"{selected.Section}/{selected.Placement}" : null,
		};
		foreach (var cue in ViewModel.Cues)
			state.Cues[cue.Name] = Player.Get(cue.Cue);
		foreach (var name in _muted.Keys.Concat(_soloed.Keys).Concat(_gainDb.Keys).Distinct())
			state.Tracks[name] = new TrackSetting
			{
				Muted = _muted.GetValueOrDefault(name),
				Solo = _soloed.GetValueOrDefault(name),
				GainDb = _gainDb.GetValueOrDefault(name),
			};
		foreach (var ((section, placement), forced) in _forced)
			if (forced != PlacementState.Auto)
				state.Forced[$"{section}/{placement}"] = forced.ToString();
		return state;
	}

	/// <summary>Takes up a saved state; anything named in it that is gone is ignored.</summary>
	public void RestoreState(WorkbenchState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		var song = -1;
		for (var i = 0; i < _catalog.Songs.Count; i++)
			if (_catalog.Songs[i].Name == state.Song)
				song = i;
		if (song >= 0 && song != _songIndex)
			SelectSong(song);
		_scale = ScaleFrom(state.ScaleRoot, state.ScaleName);
		foreach (var cue in ViewModel.Cues)
			if (state.Cues.TryGetValue(cue.Name, out var value))
			{
				Player.Set(cue.Cue, value);
				if (cue.IsChoice)
					cue.SelectedOption = CueViewModel.OptionIndex(cue.Cue, value);
				else
					cue.Value = value;
			}
		foreach (var (name, setting) in state.Tracks)
		{
			_muted[name] = setting.Muted;
			_soloed[name] = setting.Solo;
			_gainDb[name] = setting.GainDb;
		}
		foreach (var (key, value) in state.Forced)
		{
			var cut = key.IndexOf('/');
			if (cut > 0 && Enum.TryParse<PlacementState>(value, out var forced))
				_forced[(key[..cut], key[(cut + 1)..])] = forced;
		}
		_loop = state.LoopSection;
		ViewModel.ShowLoop(_loop);
		ViewModel.Octave = state.Octave;
		ViewModel.PartPanelHeight = state.PartPanelHeight;
		ViewModel.Velocity = state.Velocity;
		if (state.SelectedPart is { } part && part.IndexOf('/') is var slash && slash > 0)
			_selected = (part[..slash], part[(slash + 1)..]);
		if (_song is { } built)
			Fill(built);
		var patch = Index(_catalog.Patches, state.Patch);
		var kit = Index(_catalog.Kits, state.Kit);
		if (patch >= 0)
			SelectPatch(patch);
		else if (kit >= 0)
			SelectKit(kit);
		ViewModel.LibraryPinned = state.LibraryPinned;
		if (Enum.TryParse<WorkbenchMode>(state.Mode, out var mode))
			SetMode(mode);
		if (state.Playing)
		{
			Play();
			if (state.Section is { } section && _song?.Sections.FirstOrDefault(s => s.Name == section) is { } at && at.Index != 0)
				Player.Jump(at);
		}
	}

	private static int Index<T>(IReadOnlyList<(string Name, Func<T> Build)> list, string? name)
	{
		for (var i = 0; i < list.Count; i++)
			if (list[i].Name == name)
				return i;
		return -1;
	}

	private static Scale? ScaleFrom(string? root, string? name)
	{
		if (root is null || name is null || !Enum.TryParse<Note>(root, out var note))
			return null;
		return name switch
		{
			"major" => Scale.Major(note),
			"minor" => Scale.Minor(note),
			"dorian" => Scale.Dorian(note),
			"phrygian" => Scale.Phrygian(note),
			"lydian" => Scale.Lydian(note),
			"mixolydian" => Scale.Mixolydian(note),
			"harmonic minor" => Scale.HarmonicMinor(note),
			"pentatonic major" => Scale.PentatonicMajor(note),
			"pentatonic minor" => Scale.PentatonicMinor(note),
			"blues" => Scale.Blues(note),
			_ => null,
		};
	}

	/// <summary>Stops the song and the bench.</summary>
	public void Dispose()
	{
		_editor?.Save();
		SavePatch();
		Player.Dispose();
		_sketchPlayer?.Dispose();
		if (_bench is { } chip)
			_audio.Stop(chip);
	}

	/// <summary>Puts <paramref name="text"/> on the build pill, as a host says what it is doing before its first build.</summary>
	public void ReportStatus(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		ViewModel.Error = "";
		ViewModel.BuildText = text;
	}

	private SongEditor? _editor;
	private SongSource? _source;
	private int _selectedIndex = -1;

	// The song chosen's file, open for editing when it has one, and read again if something else changed it
	private void OpenEditor()
	{
		var name = (uint)_songIndex < (uint)_catalog.Songs.Count ? _catalog.Songs[_songIndex].Name : null;
		_source = name is null ? null : _catalog.SongSource(name);
		if (_source is null)
		{
			_editor = null;
			return;
		}
		if (_editor?.Path == _source.Path)
		{
			if (_editor.ReloadIfChanged())
			{
				_editorNote = $"{Path.GetFileName(_source.Path)} changed on disk and was reloaded. Undo history cleared.";
				Console.WriteLine($"[workbench] {_source.Path} changed on disk: read again");
			}
			return;
		}
		try
		{
			_editor?.Save();
			_editor = SongEditor.Open(_source.Path);
		}
		catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException)
		{
			_editor = null;
			Console.WriteLine($"[workbench] {_source.Path} couldn't be opened: {ex.Message}");
		}
	}

	// Where a placement stands among its section's, as its file's parts are numbered
	private static int IndexIn(Placement placement)
	{
		var placements = placement.Section.Placements;
		for (var i = 0; i < placements.Count; i++)
			if (placements[i] == placement)
				return i;
		return -1;
	}

	// The part chosen in the roll, when its song has a file and the part is data there; what it can do between data and code
	private void ShowRoll(Placement placement)
	{
		var roll = ViewModel.Roll;
		if (_editor is null)
		{
			roll.ShowNone(_source is null
				? "This song is defined in code. Only songs with a .song.json file can be edited here."
				: "The song file couldn't be read.", $"{placement.Name} · {placement.Section.Name}");
			roll.ShowCode(false, false, "");
			return;
		}
		var section = placement.Section.Index;
		var index = IndexIn(placement);
		if (_editor.Part(section, index) is not { } part)
		{
			var raw = _editor.RawPart(section, index);
			var member = raw?["code"]?.GetValue<string>() ?? raw?["part"]?["code"]?.GetValue<string>();
			var where = member is not null && _source?.Type is { } type && Code?.Find(type, member) is { } found
				? $"{member}, in {Path.GetFileName(found.File)} at line {found.Line}"
				: member ?? "";
			roll.ShowNone(member is null
				? "This part is added by a hook, so it is edited in C#."
				: $"This part is defined in C# ({where}). Convert it to notation to edit it here.",
				$"{placement.Name} · {placement.Section.Name}");
			roll.ShowCode(false, member is not null, where);
			return;
		}
		var step = (part["line"] ?? part["beat"] ?? part["strum"])?.GetValue<string>() switch
		{
			"quarters" => Ticks.Quarter,
			"eighths" => Ticks.Eighth,
			"sixteenths" => Ticks.Sixteenth,
			_ => Ticks.Triplet,
		};
		var editor = _editor;
		roll.KitPieces = placement.Track.Kit is { } kit ? [.. kit.Pieces.Select(p => p.Piece.Id)] : [];
		roll.Chords = ChordTones(placement, step, part["overChords"]?.GetValue<bool>() == true);
		roll.Show($"{placement.Name} · {placement.Section.Name}", (JsonObject)part.DeepClone(),
			placement.Section.Song.Meter.TicksPerBar / step, editor.IsEdited(section, index), editor.CanUndo, editor.CanRedo,
			change => EditPart(section, index, change),
			gesture =>
			{
				if (gesture == 1)
					editor.BeginGesture();
				else
					editor.EndGesture();
			});
		roll.ShowCode(Code is not null && _source?.Type is not null, false, "");
	}

	// For the roll: the chord over a step of a part (its first pass through the section), and whether the note a row plays
	// there is that chord's root (2), another of its notes (1) or neither, as the player plays it, following the chords or not
	private (Func<int, object> ChordAt, Func<int, int, int> Tone) ChordTones(Placement placement, int stepTicks, bool overChords)
	{
		var section = placement.Section;
		var scale = _scale ?? section.Song.Scale;
		var sectionTicks = Math.Max(1, section.Bars * section.Song.Meter.TicksPerBar);
		Chord At(int step) => section.ChordAt(step * stepTicks % sectionTicks);
		static int Class(Pitch pitch) => (((int)MathF.Round(pitch.Midi) % 12) + 12) % 12;
		int Tone(int step, int row)
		{
			var chord = At(step);
			var note = NoteToken.OnRow(row);
			var (degree, accidental) = overChords ? (chord.Degree + note.Degree - 1, chord.Accidental) : (note.Degree, 0);
			var played = Class(scale.Degree(degree, 4 + note.Octave, accidental));
			var root = chord.RootClass(scale);
			return played == root ? 2 : chord.Intervals(scale).Any(i => (root + i) % 12 == played) ? 1 : 0;
		}
		return (step => At(step), Tone);
	}

	// An edit of a part in the song's file: the song built again from the file as edited, heard at the next bar
	private void EditPart(int section, int index, Action<JsonObject> change)
	{
		if (_editor is null)
			return;
		_editor.Edit(root =>
		{
			if (root["sections"]?[section]?["parts"]?[index] is JsonObject part)
				change(part);
		});
		Rebuild(swap: true);
	}

	/// <summary>Undoes the last edit of the song's file.</summary>
	internal void UndoEdit()
	{
		if (_editor?.Undo() == true)
			Rebuild(swap: true);
	}

	/// <summary>Does again the last edit undone.</summary>
	internal void RedoEdit()
	{
		if (_editor?.Redo() == true)
			Rebuild(swap: true);
	}

	/// <summary>Puts the part chosen back as its file had it when opened.</summary>
	internal void RevertPart()
	{
		if (_editor is null || _selected is not { } selected || FindPlacement(selected) is not { } placement)
			return;
		if (_editor.RevertPart(placement.Section.Index, IndexIn(placement)))
			Rebuild(swap: true);
	}

	/// <summary>The song's file being edited, or null when the song chosen has none.</summary>
	public SongEditor? Editor => _editor;

	// What happened to the song's file, said on the build pill once the song is built again
	private string? _editorNote;

	private string? _patchFile;
	private string _patchAbout = "";
	private double _patchSince = -1;

	// The patch chosen's file, when it has one: its operators' names and its words, for the designer to save to; or that
	// the file says the patch is code
	private IReadOnlyList<string>? OpenPatchFile(string name)
	{
		SavePatch();
		if (_heard is { } heard && heard.Name != name && _patchCode is not null)
		{
			_heard = null;
			Rebuild(swap: true);
		}
		_patchFile = _catalog.PatchFile(name);
		_patchAbout = "";
		_patchCode = null;
		IReadOnlyList<string>? names = null;
		if (_patchFile is not null)
		{
			try
			{
				var text = File.ReadAllText(_patchFile);
				_patchCode = System.Text.Json.Nodes.JsonNode.Parse(text)?["code"]?.GetValue<string>();
				if (_patchCode is null)
				{
					var spec = PatchFile.Read(text, out var read);
					_patchAbout = spec.About;
					names = read;
				}
			}
			catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException or System.Text.Json.JsonException)
			{
				Console.WriteLine($"[workbench] {_patchFile} couldn't be read: {ex.Message}");
				_patchFile = null;
			}
		}
		ViewModel.Designer.ShowCode(_patchCode is not null, _patchFile is not null && _patchCode is null && Code is not null && _catalog.PatchesType is not null);
		return names;
	}

	// The designer's patch written to its file, once its edits have paused (or now, leaving it)
	private void SavePatch()
	{
		// A patch that is code is heard as edited, and its file left saying it is code
		if (_patchSince < 0 || _patchFile is null || _patchCode is not null)
			return;
		_patchSince = -1;
		var spec = ViewModel.Designer.Spec.Clone();
		spec.About = _patchAbout;
		var text = PatchFile.Write(spec, ViewModel.Designer.Names);
		try
		{
			if (!File.Exists(_patchFile) || File.ReadAllText(_patchFile) != text)
			{
				File.WriteAllText(_patchFile, text);
				ViewModel.BuildText = $"saved {Path.GetFileName(_patchFile)} {DateTime.Now:HH:mm:ss}";
			}
		}
		catch (IOException ex)
		{
			Console.WriteLine($"[workbench] {_patchFile} couldn't be written: {ex.Message}");
		}
	}

	/// <summary>The file the patch chosen is written to as the designer edits it, or null when it is code.</summary>
	public string? PatchFilePath => _patchFile;

	/// <summary>What changes the music's C# (eject, bring back, the Files view's pieces), from a project's host; null ejects nothing.</summary>
	public IMusicCode? Code { get; set; }

	private static readonly string[] PartKeys = ["line", "notes", "overChords", "octave", "beat", "rows", "strum", "voicing", "spread", "part"];

	// A name for a member made of a placement, unique in its class and among its file's code
	private string MemberName(Placement placement, string type, string suffix)
	{
		var wanted = PatchCode.Pascal(placement.Name == placement.Track.Name ? $"{placement.Section.Name} {placement.Track.Name}" : placement.Name) + suffix;
		wanted = new string([.. wanted.Where(char.IsLetterOrDigit)]);
		if (wanted.Length == 0 || char.IsDigit(wanted[0]))
			wanted = "Part" + wanted;
		var taken = new HashSet<string>(Code?.Members(type) ?? [], StringComparer.Ordinal);
		var text = _editor?.Text ?? "";
		var name = wanted;
		for (var n = 2; taken.Contains(name) || text.Contains($"\"code\": \"{name}\"", StringComparison.Ordinal); n++)
			name = wanted + n;
		return name;
	}

	/// <summary>
	/// The part chosen made code: its notation (<paramref name="placement"/> false) or its whole placement, written as a
	/// member of the song's own C#, its file pointing at it. Heard as it was once the project has compiled.
	/// </summary>
	internal void EjectSelected(bool placement)
	{
		if (Code is null || _editor is null || _source is not { Type: { } type } source || _selected is not { } selected
			|| FindPlacement(selected) is not { } chosen || _editor.Part(chosen.Section.Index, IndexIn(chosen)) is null)
			return;
		var (section, index) = (chosen.Section.Index, IndexIn(chosen));
		var member = MemberName(chosen, type, placement ? "Placement" : "");
		var code = placement
			? $"// Converted from {chosen.Section.Name} · {chosen.Name}: the whole part, with its condition and name\n"
				+ $"private static partial Placement {member}(Section section, SongTrack track) =>\n\t{PartCode.Write(chosen, "section", "track").Replace("\n", "\n\t", StringComparison.Ordinal)};"
			: $"// Converted from {chosen.Section.Name} · {chosen.Name}: its notes; its condition and name stay in the song file\n"
				+ $"private static partial Part {member}() =>\n\t{PartCode.Write(chosen.Part).Replace("\n", "\n\t", StringComparison.Ordinal)};";
		var (file, line) = Code.AddMember(type, Path.GetDirectoryName(source.Path)!, code);
		_editor.Edit(root =>
		{
			var parts = (JsonArray)root["sections"]![section]!["parts"]!;
			var old = (JsonObject)parts[index]!;
			var made = new JsonObject { ["track"] = old["track"]?.DeepClone() };
			if (placement)
				made["code"] = member;
			else
			{
				if (old["name"] is { } name)
					made["name"] = name.DeepClone();
				made["part"] = new JsonObject { ["code"] = member };
				foreach (var (key, value) in old)
					if (key is not ("track" or "name") && !PartKeys.Contains(key))
						made[key] = value?.DeepClone();
			}
			parts[index] = made;
		});
		_editor.Save();
		ViewModel.BuildText = $"Converted to C# in {Path.GetFileName(file)}:{line} · building";
		ShowRoll(chosen);
	}

	/// <summary>
	/// The part chosen, code, made data again: what its code builds written back to the song's file (if it is plain data:
	/// a line, a beat or a strum) and its member taken out of the C#.
	/// </summary>
	internal void BringBackSelected()
	{
		if (_editor is null || _source is not { Type: { } type } || _selected is not { } selected || FindPlacement(selected) is not { } chosen)
			return;
		var (section, index) = (chosen.Section.Index, IndexIn(chosen));
		if (_editor.RawPart(section, index) is not { } raw)
			return;
		string? member;
		JsonObject data;
		try
		{
			if (raw["code"]?.GetValue<string>() is { } whole)
			{
				member = whole;
				data = SongFile.ToJson(chosen);
			}
			else if (raw["part"]?["code"]?.GetValue<string>() is { } notation)
			{
				member = notation;
				data = new JsonObject { ["track"] = raw["track"]?.DeepClone() };
				if (raw["name"] is { } name)
					data["name"] = name.DeepClone();
				foreach (var (key, value) in SongFile.ToJson(chosen.Part))
					data[key] = value?.DeepClone();
				foreach (var (key, value) in raw)
					if (key is not ("track" or "name") && !PartKeys.Contains(key))
						data[key] = value?.DeepClone();
			}
			else
				return;
		}
		catch (NotSupportedException ex)
		{
			ViewModel.Roll.ShowNone($"This part can't be written as notation ({ex.Message}), so it stays in C#.");
			return;
		}
		_editor.Edit(root => ((JsonArray)root["sections"]![section]!["parts"]!)[index] = data);
		_editor.Save();
		// Its member goes too, unless another piece of the file still calls it
		if (Code is not null && !_editor.Text.Contains($"\"code\": \"{member}\"", StringComparison.Ordinal))
			Code.RemoveMember(type, member);
		ViewModel.BuildText = $"{member} converted to notation";
		Rebuild(swap: true);
	}

	// The member the patch chosen is, when its file says it is code
	private string? _patchCode;

	// The patch chosen made code: the C# for it written into the patches' own half, its file pointing at the property
	private void EjectPatch()
	{
		if (Code is null || _patchFile is null || _patchCode is not null || _catalog.PatchesType is not { } type)
			return;
		SavePatch();
		var designer = ViewModel.Designer;
		var stem = Path.GetFileName(_patchFile);
		stem = stem[..stem.IndexOf('.', StringComparison.Ordinal)];
		var property = PatchCode.Pascal(stem);
		var code = $"// Converted from {stem}.patch.json. \"Convert to patch file\" turns the patch this builds back into a file.\n"
			+ PatchCode.Write(designer.Spec, property, designer.Names, _catalog.MacroName)
				.Replace($"public static Patch {property} =>", $"public static partial Patch {property} =>", StringComparison.Ordinal);
		var (file, line) = Code.AddMember(type, Path.GetDirectoryName(_patchFile)!, code);
		File.WriteAllText(_patchFile, $"{{\n  \"format\": \"{PatchFile.Format}\",\n  \"name\": \"{designer.Spec.Name}\",\n  \"code\": \"{property}\"\n}}\n");
		_patchCode = property;
		designer.ShowCode(true, false);
		ViewModel.BuildText = $"Converted to C# in {Path.GetFileName(file)}:{line} · building";
	}

	// The code patch made data again: what it builds written to its file, its operators named as its variables are, and
	// its member taken out of the C#
	private void BringBackPatch()
	{
		if (_patchFile is null || _patchCode is not { } property || _patchChoice < 0)
			return;
		var patch = _catalog.Patches[_patchChoice].Build();
		var removed = _catalog.PatchesType is { } type ? Code?.RemoveMember(type, property) : null;
		var spec = PatchSpec.From(patch);
		var names = removed is null ? null : VariableNames(removed, spec.Operators.Count);
		File.WriteAllText(_patchFile, PatchFile.Write(spec, names));
		_patchCode = null;
		ViewModel.Designer.Load(patch, _catalog.MacroName, names: names);
		ViewModel.Designer.ShowCode(false, Code is not null && _catalog.PatchesType is not null);
		ViewModel.BuildText = $"{property} converted to a patch file";
	}

	// The variables a patch's C# makes its operators into, in order ("var tine = p.Sine(…)"), when each operator is one
	private static string[]? VariableNames(string code, int operators)
	{
		var made = System.Text.RegularExpressions.Regex.Matches(code, @"(?:var\s+(\w+)\s*=\s*)?\bp\.(?:Sine|Wave|Noise|Level|Sample|String|Tube|Modes)\(");
		return made.Count == operators ? [.. made.Select(m => m.Groups[1].Success ? m.Groups[1].Value : "")] : null;
	}

	// The Files view's list: every song's file, every patch's, and the C# that is theirs; the one shown kept
	private void RefreshFiles()
	{
		var files = ViewModel.Files;
		var shown = files.Files.FirstOrDefault(f => f.IsSelected)?.Path;
		files.Files.Clear();
		void Add(string path, string kind)
		{
			if (!files.Files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
				files.Files.Add(new FileEntryViewModel(Path.GetFileName(path), kind, path, ShowFile));
		}
		foreach (var (name, _) in _catalog.Songs)
			if (_catalog.SongSource(name) is { } source)
				Add(source.Path, "song");
		foreach (var (name, _) in _catalog.Patches)
			if (_catalog.PatchFile(name) is { } patch)
				Add(patch, "patch");
		foreach (var file in Code?.Files ?? [])
			Add(file, "C#");
		if ((files.Files.FirstOrDefault(f => f.Path == shown) ?? files.Files.FirstOrDefault()) is { } first)
			first.IsSelected = true;
		else
			files.Show("This project has no music files. Songs (.song.json) and patches (.patch.json) appear here.", "", []);
	}

	private void ShowFile(FileEntryViewModel entry)
	{
		foreach (var other in ViewModel.Files.Files)
			if (other != entry)
				other.IsSelected = false;
		ViewModel.Files.Shown(entry);
		string text;
		try
		{
			text = File.ReadAllText(entry.Path);
		}
		catch (IOException ex)
		{
			ViewModel.Files.Show(entry.Name, ex.Message, []);
			return;
		}
		var pieces = entry.Kind switch
		{
			"C#" => Code?.Pieces(entry.Path) ?? [],
			_ => DataPieces(text),
		};
		ViewModel.Files.Show($"{entry.Name} · {Path.GetDirectoryName(entry.Path)}", text, pieces);
	}

	// A music file's pieces, each written alone as its file writes it: a song's tracks, sections and parts; a patch's operators
	private static IReadOnlyList<(string Name, string Text)> DataPieces(string text)
	{
		JsonObject? root;
		try
		{
			root = JsonNode.Parse(text) as JsonObject;
		}
		catch (System.Text.Json.JsonException)
		{
			return [];
		}
		if (root is null)
			return [];
		static string Write(JsonNode? node) => node is JsonObject obj ? SongFile.Write((JsonObject)obj.DeepClone()).TrimEnd() : node?.ToJsonString() ?? "";
		string? Name(JsonNode? node) => node?["name"]?.GetValue<string>();
		var pieces = new List<(string, string)>();
		foreach (var track in root["tracks"] as JsonArray ?? [])
			pieces.Add(($"track {Name(track)}", Write(track)));
		foreach (var section in root["sections"] as JsonArray ?? [])
		{
			pieces.Add(($"section {Name(section)}", Write(section)));
			foreach (var part in section?["parts"] as JsonArray ?? [])
				pieces.Add(($"{Name(section)} · {Name(part) ?? part?["track"]?.GetValue<string>()}", Write(part)));
		}
		foreach (var op in root["operators"] as JsonArray ?? [])
			pieces.Add(($"operator {Name(op)}", Write(op)));
		return pieces;
	}
}
