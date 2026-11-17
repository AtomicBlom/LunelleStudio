using Loom.Audio.Synth.Chip;
using Loom.Audio.Synth.Music;
using Loom.Input;

namespace Loom.Audio.Workbench;

/// <summary>
/// The music workbench's model: plays a catalog's songs, rebuilds the one chosen whenever hot reload changes code and
/// swaps it in on the next bar, steers it (sections, mutes, solos, gains, forced parts, cues), plays patches and kits
/// from the keyboard, and fills a <see cref="WorkbenchViewModel"/> the views bind to. It needs no window, so it is tested
/// headless; an app hosts it with <c>WorkbenchView</c> over its view model.
/// </summary>
/// <remarks>Every call is for the thread that made the engine: the app's frame thread.</remarks>
public sealed class Workbench : IDisposable
{
	// The keyboard's keys, an octave from C, as a piano's white and black keys lie on a QWERTY board
	private static readonly Key[] Keys = [Key.A, Key.W, Key.S, Key.E, Key.D, Key.F, Key.T, Key.G, Key.Y, Key.H, Key.U, Key.J, Key.K];

	private static int _changes;

	private readonly AudioEngine _audio;
	private readonly AudioBus _bus;
	private readonly WorkbenchCatalog _catalog;
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
	}

	private void OnCopyRequested(string text) =>
		ViewModel.CopyStatus = CopyText is null ? "This host has no clipboard: select the code to copy it."
			: CopyText(text) ? $"C# copied at {DateTime.Now:HH:mm:ss}" : "The clipboard refused it.";

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

	/// <summary>Chooses a song and builds it; if one was playing, it plays.</summary>
	public void SelectSong(int index)
	{
		if ((uint)index >= (uint)_catalog.Songs.Count)
			throw new ArgumentOutOfRangeException(nameof(index), index, "No such song in the catalog.");
		var playing = Player.IsPlaying;
		_songIndex = index;
		_song = null;
		_selected = null;
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
			song = build() ?? throw new InvalidOperationException("Its factory returned no song.");
		}
		catch (Exception ex)
		{
			ViewModel.Error = $"{name}: {ex.Message}";
			ViewModel.BuildText = _song is null ? "didn't build" : "didn't build · the last that built plays on";
			Console.WriteLine($"[workbench] {name} didn't build: {ex.Message}");
			return;
		}
		ViewModel.Error = "";
		ViewModel.BuildText = $"built {DateTime.Now:HH:mm:ss}";
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
		ViewModel.TempoText = $"{song.Tempo:0} bpm";
		ViewModel.ScaleText = Describe.Scale(_scale ?? song.Scale);
		ViewModel.Sections.Clear();
		foreach (var section in song.Sections)
			ViewModel.Sections.Add(new SectionViewModel(this, section.Index, section.Name, section.Bars, section.ChordText));
		ViewModel.Tracks.Clear();
		foreach (var track in song.Tracks)
		{
			var row = new TrackViewModel(this, track.Name, _muted.GetValueOrDefault(track.Name), _soloed.GetValueOrDefault(track.Name),
				_gainDb.GetValueOrDefault(track.Name))
			{
				Instrument = track.Patch is { } patch ? $"patch {patch.Name}" : track.Kit is { } kit ? $"kit {kit.Name}" : "",
			};
			foreach (var section in song.Sections)
			{
				var cell = new LaneCellViewModel(section.Index, ArrangeLayout.SectionWidth(section.Bars));
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
		foreach (var cue in song.Cues)
			if (!ViewModel.Cues.Any(c => c.Cue == cue))
				ViewModel.Cues.Add(new CueViewModel(this, cue, Player.Get(cue)));
		var used = song.Cues;
		foreach (var cue in ViewModel.Cues)
		{
			cue.IsUsed = used.Contains(cue.Cue);
			cue.Readers.Clear();
			foreach (var section in song.Sections)
				foreach (var placement in section.Placements)
					if (placement.Condition is { } condition)
						foreach (var (level, above) in Levels(condition, cue.Cue))
							cue.Readers.Add(new CueReaderViewModel(level, above, $"{section.Name} · {placement.Name}"));
		}
		if (_selected is { } selected && FindPlacement(selected) is { } again)
			ShowPattern(again);
		RefreshConditions();
	}

	// A part as a clip in Arrange: one pass of it drawn small, what it plays, whether it is the one chosen
	private PlacementViewModel Clip(Placement placement)
	{
		PlacementPattern pattern;
		try
		{
			pattern = Player.Pattern(placement, _scale);
		}
		catch (ArgumentException)
		{
			pattern = new PlacementPattern(0, 0, null, []);
		}
		var events = pattern.Events;
		var kit = pattern.Kit;
		List<float> rows = kit is not null
			? [.. events.Select(e => (float)e.Piece).Distinct().Order()]
			: [.. events.Select(e => MathF.Round(e.Note)).Distinct().OrderDescending()];
		var key = (placement.Section.Name, placement.Name);
		return new PlacementViewModel(this, placement, Describe.Placement(placement), _forced.GetValueOrDefault(key))
		{
			KindText = (kit is not null ? "hits" : "notes") + (placement.IsOnPhrase ? " · on its phrase" : ""),
			Notes = [.. events.Select(e => new PatternNote(e.Tick, e.Length, rows.IndexOf(kit is not null ? e.Piece : MathF.Round(e.Note)), e.Velocity))],
			LoopTicks = pattern.LoopTicks,
			Rows = rows.Count,
			IsSelected = _selected == key,
		};
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
				reader.IsMet = reader.Above ? value > reader.Level : value < reader.Level;
		}
	}

	// Where the song is in Arrange: how far through each section, which column the playhead crosses, a jump waiting, and
	// what plays next
	private void RefreshArrange(Song song, MusicPosition position)
	{
		var progress = Math.Clamp(Player.SectionTick / (float)Math.Max(1, song.Sections[position.SectionIndex].Bars * song.Meter.TicksPerBar), 0f, 1f);
		if (_queued == position.SectionIndex)
			_queued = -1;
		foreach (var section in ViewModel.Sections)
		{
			var current = section.Index == position.SectionIndex;
			section.Progress = current ? progress : 0f;
			section.IsQueued = section.Index == _queued;
		}
		foreach (var row in ViewModel.Tracks)
			foreach (var cell in row.Cells)
			{
				cell.IsCurrent = cell.Section == position.SectionIndex;
				cell.Progress = cell.IsCurrent ? progress : 0f;
			}
		ViewModel.NextText = _queued >= 0 && _queued < song.Sections.Count
			? $"Next: {song.Sections[_queued].Name}, at the next bar"
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
		}
		catch (InvalidOperationException ex)
		{
			ViewModel.Error = ex.Message;
			Console.WriteLine($"[workbench] {ex.Message}");
		}
		_sinceRefresh += deltaSeconds;
		if (_sinceRefresh >= 1.0 / 30.0)
		{
			_sinceRefresh = 0;
			Refresh();
			ViewModel.RaiseRefreshed();
		}
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
				placement.IsActive = placement.Placement.Section.Song == song && Player.IsActive(placement.Placement);
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
				foreach (var placement in section.Placements)
					if (placement.Name == key.Placement)
						return placement;
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
		foreach (var track in ViewModel.Tracks)
			foreach (var clip in track.Placements)
				clip.IsSelected = clip == row;
		ShowPattern(row.Placement);
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
		ViewModel.Designer.Load(patch, _catalog.MacroName, keepEdits: true);
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
		yield return new PaletteItemViewModel("Command", _loop ? "Stop looping the section" : "Loop the section playing", () => ViewModel.LoopSection = !_loop);
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
		Player.Dispose();
		if (_bench is { } chip)
			_audio.Stop(chip);
	}
}
