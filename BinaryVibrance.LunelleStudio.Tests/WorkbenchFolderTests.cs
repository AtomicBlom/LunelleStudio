using System.Diagnostics;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.LunelleStudio.Projects;
using BinaryVibrance.Sirrow;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// The workbench over music files alone, with nothing compiled: the folder read and its song played, its code said, a
/// save read again, and a song's file as the source its edits go back to.
/// </summary>
public class WorkbenchFolderTests
{
	private const string Project = """
		{
		  "format": "lunelle/2",
		  "name": "tunes",
		  "macros": { "bright": 0 }
		}
		""";

	private const string Cues = """
		{ "format": "lunelle-cues/1", "cues": [{ "name": "intensity", "initial": 0.5 }, { "name": "mood", "options": ["Calm", "Dark"] }] }
		""";

	private static string Song(int tempo) => $$"""
		{
		  "format": "lunelle-song/3",
		  "cues": { "intensity": null, "mood": null },
		  "tempo": {{tempo}},
		  "tracks": [{ "name": "lead", "patch": "tone" }],
		  "sections": [
		    {
		      "name": "verse", "bars": 1,
		      "parts": [
		        { "track": "lead", "line": "quarters", "notes": "1 3 5 8", "when": "not mood is Dark" },
		        { "track": "lead", "part": { "code": "Riff" } }
		      ],
		      "exits": [{ "then": "verse" }]
		    }
		  ]
		}
		""";

	private static string MakeFolder()
	{
		var folder = Path.Combine(Path.GetTempPath(), "lunelle-studio-folder-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(Path.Combine(folder, "Songs"));
		Directory.CreateDirectory(Path.Combine(folder, "Patches"));
		File.WriteAllText(Path.Combine(folder, MusicFolder.ProjectFileName), Project);
		Directory.CreateDirectory(Path.Combine(folder, "Cues"));
		File.WriteAllText(Path.Combine(folder, "Cues", "tunes.cues.json"), Cues);
		File.WriteAllText(Path.Combine(folder, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		File.WriteAllText(Path.Combine(folder, "Songs", "tune.song.json"), Song(120));
		return folder;
	}

	[Test]
	public void A_folder_s_catalog_is_its_music_with_its_files_as_their_sources_and_no_code()
	{
		var folder = MakeFolder();
		var music = MusicFolder.FromDirectory(folder);
		var catalog = FolderCatalog.From(music, folder);

		catalog.Songs.Select(s => s.Name).ShouldBe(["tune"]);
		catalog.Patches.Select(p => p.Name).ShouldBe(["tone"]);
		catalog.Cues.Select(c => c.Name).ShouldBe(["intensity", "mood"]);
		catalog.SongSource("tune").ShouldNotBeNull().Path.ShouldBe(Path.Combine(folder, "Songs", "tune.song.json"));
		catalog.SongSource("tune")!.Type.ShouldBeNull();
		catalog.PatchFile("tone").ShouldBe(Path.Combine(folder, "Patches", "tone.patch.json"));
		catalog.PatchesType.ShouldBeNull();
		catalog.MacroName!(0).ShouldBe("Macros.Bright");
		catalog.MacroName(1).ShouldBeNull();
		FolderCatalog.Problems(music).ShouldBe(["tune: its code plays nothing without C# (part Riff)"]);
		WorkbenchFolder.IsFilesAlone(folder).ShouldBeTrue();
	}

	[Test]
	public void A_workbench_on_a_folder_plays_its_song_and_takes_up_a_save()
	{
		var folder = MakeFolder();
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog());
		using var host = WorkbenchFolder.Open(folder, workbench);
		host.Update();
		var first = host.Current.ShouldNotBeNull();
		workbench.ViewModel.SongName.ShouldBe("tune");
		workbench.ViewModel.BuildText.ShouldStartWith("read ");
		workbench.ViewModel.BuildText.ShouldEndWith(" · 1 to look at");
		workbench.ViewModel.Error.ShouldBe("tune: its code plays nothing without C# (part Riff)");
		workbench.Play();
		for (var i = 0; i < 30; i++)
		{
			workbench.Update(1.0 / 60.0);
			session.Render(800);
		}
		session.Left().Max(MathF.Abs).ShouldBeGreaterThan(0.01f);

		File.WriteAllText(Path.Combine(folder, "Songs", "tune.song.json"), Song(90));
		var deadline = Stopwatch.StartNew();
		while (ReferenceEquals(host.Current, first) && deadline.Elapsed < TimeSpan.FromSeconds(10))
		{
			host.Update();
			workbench.Update(1.0 / 60.0);
			session.Render(800);
			Thread.Sleep(20);
		}
		host.Current.ShouldNotBeSameAs(first);
		host.Current!.Songs.Single().Build().Tempo.ShouldBe(90f);
	}

	[Test]
	public void A_new_project_of_files_alone_reads_whole_and_plays_its_kit()
	{
		var folder = Path.Combine(Path.GetTempPath(), "lunelle-studio-new-" + Guid.NewGuid().ToString("N")[..8], "Data Tunes");
		ProjectTemplate.CreateData(folder).ShouldBe(folder);
		Should.Throw<InvalidOperationException>(() => ProjectTemplate.CreateData(folder));
		Directory.EnumerateFiles(folder, "*.cs*", SearchOption.AllDirectories).ShouldBeEmpty();
		WorkbenchFolder.IsFilesAlone(folder).ShouldBeTrue();

		var music = MusicFolder.FromDirectory(folder);
		music.Name.ShouldBe("data-tunes");
		music.Title.ShouldBe("Data Tunes");
		FolderCatalog.Problems(music).ShouldBeEmpty();
		music.Songs.ShouldHaveSingleItem().Build().Sections.Select(s => s.Name).ShouldBe(["verse", "chorus"]);
		music.Cues.Select(c => c.Name).ShouldBe(["intensity", "mood"]);
		var kit = music.Kits.ShouldHaveSingleItem().Build();
		kit.Name.ShouldBe("demo-kit");
		kit.Pieces.Select(p => (p.Piece.Id, p.Patch!.Name, p.ChokeGroup)).ShouldBe(
			[("kick", "kick", 0), ("snare", "snare", 0), ("hat-closed", "closed-hat", 1), ("hat-open", "open-hat", 1)]);

		using var session = new OfflineSession();
		var chip = new SynthChip(new ChipOptions { SpaceEffects = [], EchoEffects = [] });
		var drums = chip.AddKit(kit);
		session.Engine.Play(chip);
		chip.Hit(drums, Kit.Kick, 0.8f);
		session.Render(48_000 / 4);
		session.Left().Max(MathF.Abs).ShouldBeGreaterThan(0.01f);
	}

	[Test]
	public void A_workbench_over_files_kept_elsewhere_reads_and_writes_them_there()
	{
		var root = Path.Combine(Path.GetTempPath(), "lunelle-studio-nowhere-" + Guid.NewGuid().ToString("N")[..8]);
		var files = new MemoryFiles();
		files.WriteText(Path.Combine(root, MusicFolder.ProjectFileName), Project);
		files.WriteText(Path.Combine(root, "Cues", "tunes.cues.json"), Cues);
		files.WriteText(Path.Combine(root, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		files.WriteText(Path.Combine(root, "Songs", "tune.song.json"), Song(120));

		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Files = files };
		using var host = WorkbenchFolder.Open(root, workbench);
		host.Update();
		var first = host.Current.ShouldNotBeNull();
		workbench.ViewModel.SongName.ShouldBe("tune");
		workbench.NewSong("fresh").ShouldBeNull();
		files.ReadText(Path.Combine(root, "Songs", "fresh.song.json")).ShouldContain("\"format\": \"lunelle-song/3\"");
		workbench.NewPatch("bell", copyOf: "tone").ShouldBeNull();
		files.Exists(Path.Combine(root, "Patches", "bell.patch.json")).ShouldBeTrue();

		// The new song and patch heard, and the folder read again with them
		var deadline = Stopwatch.StartNew();
		while (ReferenceEquals(host.Current, first) && deadline.Elapsed < TimeSpan.FromSeconds(10))
		{
			host.Update();
			Thread.Sleep(20);
		}
		host.Current.ShouldNotBeSameAs(first);
		host.Current!.Songs.Select(s => s.Name).ShouldBe(["fresh", "tune"], ignoreOrder: true);
		host.Current.Patches.Select(p => p.Name).ShouldBe(["bell", "tone"], ignoreOrder: true);

		var editor = SongEditor.Open(Path.Combine(root, "Songs", "tune.song.json"), files);
		editor.Edit(song => song["tempo"] = 96);
		editor.Save();
		files.ReadText(Path.Combine(root, "Songs", "tune.song.json")).ShouldContain("\"tempo\": 96");

		var state = new WorkbenchStateFile(Path.Combine(root, ".lunelle-studio", "workbench.json"), files);
		state.Save(workbench.CaptureState());
		state.Load().ShouldNotBeNull().Song.ShouldBe("tune");
		Directory.Exists(root).ShouldBeFalse();
	}

	[Test]
	public void A_workbench_over_a_document_undoes_and_saves_its_edits_and_the_music_follows()
	{
		var root = Path.Combine(Path.GetTempPath(), "lunelle-studio-document-" + Guid.NewGuid().ToString("N")[..8]);
		var disk = new MemoryFiles();
		disk.WriteText(Path.Combine(root, MusicFolder.ProjectFileName), Project);
		disk.WriteText(Path.Combine(root, "Cues", "tunes.cues.json"), Cues);
		disk.WriteText(Path.Combine(root, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		var song = Path.Combine(root, "Songs", "tune.song.json");
		disk.WriteText(song, Song(120));
		var document = ProjectDocument.Open(disk, root);

		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Files = document };
		using var host = WorkbenchFolder.Open(root, workbench);
		host.Update();
		workbench.ViewModel.SongName.ShouldBe("tune");
		workbench.ViewModel.IsDirty.ShouldBeFalse();

		workbench.Editor!.Edit(root => root["tempo"] = 96);
		workbench.Editor.Save();
		workbench.ViewModel.IsDirty.ShouldBeTrue();
		document.ReadText(song).ShouldContain("\"tempo\": 96");
		disk.ReadText(song).ShouldContain("\"tempo\": 120");

		workbench.UndoEdit();
		document.ReadText(song).ShouldContain("\"tempo\": 120");
		workbench.ViewModel.IsDirty.ShouldBeFalse();
		workbench.RedoEdit();
		document.ReadText(song).ShouldContain("\"tempo\": 96");
		workbench.Editor.Text.ShouldContain("\"tempo\": 96");

		workbench.Save();
		disk.ReadText(song).ShouldContain("\"tempo\": 96");
		workbench.ViewModel.IsDirty.ShouldBeFalse();
		document.CanUndo.ShouldBeFalse();
	}

	[Test]
	public void Cues_are_made_imported_renamed_and_deleted_across_the_songs_as_one_step_each()
	{
		var root = Path.Combine(Path.GetTempPath(), "lunelle-studio-cues-" + Guid.NewGuid().ToString("N")[..8]);
		var disk = new MemoryFiles();
		disk.WriteText(Path.Combine(root, MusicFolder.ProjectFileName), Project);
		disk.WriteText(Path.Combine(root, "Cues", "tunes.cues.json"), Cues);
		disk.WriteText(Path.Combine(root, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		var song = Path.Combine(root, "Songs", "tune.song.json");
		disk.WriteText(song, Song(120).Replace("\"intensity\": null", "\"intensity\": 0.8", StringComparison.Ordinal));
		var document = ProjectDocument.Open(disk, root);
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Files = document };
		using var host = WorkbenchFolder.Open(root, workbench);
		host.Update();
		var model = workbench.ViewModel;

		// The panel lists the project's cues, the strip the song's, each starting at its preview
		model.ProjectCues.Select(c => (c.Id, c.IsImported)).ShouldBe([("intensity", true), ("mood", true)]);
		model.Cues.Select(c => c.Name).ShouldBe(["intensity", "mood"]);
		workbench.Player.Get(workbench.CueNamed("intensity")!).ShouldBe(0.8f);

		// A cue the song reads can't be taken out of its imports
		workbench.UnimportCue("mood").ShouldNotBeNull();

		// A new cue goes into its namespace's file and the song's imports, one step
		workbench.NewCue("lava/phase", ["Countdown", "Race"], "The lava round").ShouldBeNull();
		document.ReadText(Path.Combine(root, "Cues", "lava.cues.json")).ShouldContain("\"namespace\": \"lava\"");
		document.ReadText(song).ShouldContain("\"lava/phase\": null");
		document.Undo();
		document.Exists(Path.Combine(root, "Cues", "lava.cues.json")).ShouldBeFalse();
		document.ReadText(song).ShouldNotContain("lava/phase");

		// A rename reaches every place a song reads it
		workbench.RenameCue("mood", "feeling").ShouldBeNull();
		var renamed = document.ReadText(song);
		renamed.ShouldContain("\"feeling\": null");
		renamed.ShouldContain("not feeling is Dark");
		renamed.ShouldNotContain("mood");
		document.ReadText(Path.Combine(root, "Cues", "tunes.cues.json")).ShouldContain("\"name\": \"feeling\"");

		// A cue a song reads can't be deleted; one none reads can
		workbench.DeleteCue("feeling").ShouldNotBeNull();
		workbench.ReadProjectCuesForTest();
		workbench.UnimportCue("intensity").ShouldBeNull();
		var deleted = workbench.DeleteCue("intensity");
		deleted.ShouldBeNull(deleted);
		document.ReadText(Path.Combine(root, "Cues", "tunes.cues.json")).ShouldNotContain("intensity");
	}

	[Test]
	public void A_cue_a_condition_names_is_made_as_it_reads_and_a_cue_s_file_is_edited_from_its_menu()
	{
		var root = Path.Combine(Path.GetTempPath(), "lunelle-studio-cue-edits-" + Guid.NewGuid().ToString("N")[..8]);
		var disk = new MemoryFiles();
		disk.WriteText(Path.Combine(root, MusicFolder.ProjectFileName), Project);
		disk.WriteText(Path.Combine(root, "Cues", "tunes.cues.json"), Cues);
		disk.WriteText(Path.Combine(root, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		var song = Path.Combine(root, "Songs", "tune.song.json");
		disk.WriteText(song, Song(120));
		var document = ProjectDocument.Open(disk, root);
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Files = document };
		using var host = WorkbenchFolder.Open(root, workbench);
		host.Update();
		workbench.HasCode.ShouldBeFalse();
		workbench.CodeWarning("lava/heat").ShouldBe("");

		// What a condition names that is no cue, as it reads it
		const string condition = "lava/heat > 0.5 and biome is Forest and mood is Dark and pass < 2";
		workbench.UnknownCuesIn(condition).Select(u => (u.Id, string.Join(",", u.Options))).ShouldBe([("lava/heat", ""), ("biome", "Forest")]);
		workbench.CheckCondition(condition).ShouldNotBeNull();
		workbench.MakeCuesIn(condition).ShouldBeNull();
		workbench.ProjectCues.Select(c => c.Id).ShouldBe(["biome", "intensity", "lava/heat", "mood"]);
		workbench.ProjectCues.Single(c => c.Id == "biome").Options.Select(o => o.Name).ShouldBe(["Forest", "Other"]);
		document.ReadText(song).ShouldContain("\"lava/heat\": null");
		// The condition holds now, before the project is read again
		workbench.CheckCondition(condition).ShouldBeNull();
		// Both made as one step
		document.Undo();
		document.Exists(Path.Combine(root, "Cues", "lava.cues.json")).ShouldBeFalse();
		document.Redo();

		// The menu edits a choice's options (one a song names stays), where it starts, and what it is for
		var cues = Path.Combine(root, "Cues", "tunes.cues.json");
		workbench.EditCue("mood", ["Calm", "Bright"], null, null).ShouldNotBeNull();
		workbench.EditCue("mood", ["Bright", "Calm", "Dark"], "Bright", "How the scene feels").ShouldBeNull();
		var edited = document.ReadText(cues);
		edited.ShouldContain("\"options\": [\"Bright\", \"Calm\", \"Dark\"]");
		edited.ShouldContain("\"initial\": \"Bright\"");
		edited.ShouldContain("\"about\": \"How the scene feels\"");
		workbench.EditCue("intensity", ["A", "B"], null, null).ShouldNotBeNull();
		workbench.EditCue("intensity", null, "1.5", null).ShouldNotBeNull();
		workbench.EditCue("intensity", null, "0.25", null).ShouldBeNull();
		document.ReadText(cues).ShouldContain("\"initial\": 0.25");

		// The strip's value kept as the song's preview
		workbench.Player.Set(workbench.CueNamed("intensity")!, 0.7f);
		workbench.PreviewCueHere("intensity").ShouldBeNull();
		document.ReadText(song).ShouldContain("\"intensity\": 0.7");
	}

	[Test]
	public void A_part_s_notes_are_extracted_into_a_pattern_detached_again_and_a_pattern_used_in_a_part()
	{
		var root = Path.Combine(Path.GetTempPath(), "lunelle-studio-patterns-" + Guid.NewGuid().ToString("N")[..8]);
		var disk = new MemoryFiles();
		disk.WriteText(Path.Combine(root, MusicFolder.ProjectFileName), Project);
		disk.WriteText(Path.Combine(root, "Cues", "tunes.cues.json"), Cues);
		disk.WriteText(Path.Combine(root, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		var song = Path.Combine(root, "Songs", "tune.song.json");
		disk.WriteText(song, Song(120));
		var document = ProjectDocument.Open(disk, root);
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Files = document };
		using var host = WorkbenchFolder.Open(root, workbench);
		host.Update();
		var model = workbench.ViewModel;
		void ReadAgain()
		{
			var first = host.Current;
			var deadline = Stopwatch.StartNew();
			while (ReferenceEquals(host.Current, first) && deadline.Elapsed < TimeSpan.FromSeconds(10))
			{
				host.Update();
				Thread.Sleep(20);
			}
		}

		model.Tracks[0].Placements[0].Select();
		model.Roll.CanExtract.ShouldBeTrue();
		workbench.ExtractSelected("melody").ShouldBeNull();
		var pattern = document.ReadText(Path.Combine(root, "Patterns", "melody.pattern.json"));
		pattern.ShouldContain("\"kind\": \"line\"");
		pattern.ShouldContain("\"notes\": \"1 3 5 8\"");
		var extracted = document.ReadText(song);
		extracted.ShouldContain("\"pattern\": \"melody\"");
		extracted.ShouldNotContain("\"notes\": \"1 3 5 8\"");
		ReadAgain();
		host.Current!.Problems.ShouldBeEmpty();
		model.LibraryPatterns.Select(p => p.Id).ShouldBe(["melody"]);
		workbench.PatternUses("melody").ShouldBe(1);

		model.Tracks[0].Placements[0].Select();
		model.Roll.IsPattern.ShouldBeTrue();
		model.Roll.PatternName.ShouldBe("melody");
		workbench.DetachSelected().ShouldBeNull();
		var detached = document.ReadText(song);
		detached.ShouldContain("\"notes\": \"1 3 5 8\"");
		detached.ShouldNotContain("\"pattern\"");

		workbench.UsePattern("melody").ShouldBeNull();
		document.ReadText(song).ShouldContain("\"pattern\": \"melody\"");
		// Each was one step
		document.Undo();
		document.ReadText(song).ShouldBe(detached);
	}

	[Test]
	public void A_pattern_dropped_on_a_lane_is_a_new_part_on_a_part_its_content_and_a_beat_fits_no_patch()
	{
		var root = Path.Combine(Path.GetTempPath(), "lunelle-studio-drops-" + Guid.NewGuid().ToString("N")[..8]);
		var disk = new MemoryFiles();
		disk.WriteText(Path.Combine(root, MusicFolder.ProjectFileName), Project);
		disk.WriteText(Path.Combine(root, "Cues", "tunes.cues.json"), Cues);
		disk.WriteText(Path.Combine(root, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		disk.WriteText(Path.Combine(root, "Patterns", "riffs", "climb.pattern.json"),
			"""{ "format": "lunelle-pattern/1", "kind": "line", "step": "quarters", "notes": "1 2 3 4" }""");
		disk.WriteText(Path.Combine(root, "Patterns", "groove.pattern.json"),
			"""{ "format": "lunelle-pattern/1", "kind": "beat", "step": "eighths", "rows": { "kick": "x...x..." } }""");
		var song = Path.Combine(root, "Songs", "tune.song.json");
		disk.WriteText(song, Song(120));
		var document = ProjectDocument.Open(disk, root);
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Files = document };
		using var host = WorkbenchFolder.Open(root, workbench);
		host.Update();
		var model = workbench.ViewModel;
		model.LibraryPatterns.Select(p => p.Id).ShouldBe(["groove", "riffs/climb"]);
		var before = document.ReadText(song);

		// A beat fits a kit's track alone: dimmed in the library while a patch's part is chosen
		model.Tracks[0].Placements[0].Select();
		model.LibraryPatterns.Select(p => p.Fit).ShouldBe([0.45f, 1f]);
		workbench.PatternMisfit("groove", "lead").ShouldNotBeNull();
		workbench.DropPattern("groove", "lead", 0, null, inline: false).ShouldNotBeNull();
		document.ReadText(song).ShouldBe(before);

		// On the lane's ground: a new part of the track, playing it
		workbench.DropPattern("riffs/climb", "lead", 0, null, inline: false).ShouldBeNull();
		var added = document.ReadText(song);
		added.ShouldContain("\"pattern\": \"riffs/climb\"");
		model.Tracks[0].Placements.Count.ShouldBe(3);

		// On a part, with Shift: the pattern's content in its place, its own to edit
		var first = model.Tracks[0].Placements[0];
		workbench.DropPattern("riffs/climb", "lead", 0, first, inline: true).ShouldBeNull();
		var copied = document.ReadText(song);
		copied.ShouldContain("\"notes\": \"1 2 3 4\"");
		copied.ShouldNotContain("\"notes\": \"1 3 5 8\"");
		// Each drop was one step
		document.Undo();
		document.ReadText(song).ShouldBe(added);
		document.Undo();
		document.ReadText(song).ShouldBe(before);
	}

	[Test]
	public void A_song_s_exports_are_listed_rendered_and_a_new_one_recorded_from_the_cues_moved()
	{
		var root = Path.Combine(Path.GetTempPath(), "lunelle-studio-exports-" + Guid.NewGuid().ToString("N")[..8]);
		var disk = new MemoryFiles();
		disk.WriteText(Path.Combine(root, MusicFolder.ProjectFileName), Project);
		disk.WriteText(Path.Combine(root, "Cues", "tunes.cues.json"), Cues);
		disk.WriteText(Path.Combine(root, "Patches", "tone.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
		var song = Path.Combine(root, "Songs", "tune.song.json");
		disk.WriteText(song, Song(120).Replace("\"tempo\": 120,", "\"tempo\": 120, \"exports\": [{ \"name\": \"short\", \"title\": \"A short one\", \"end\": { \"at\": \"3:1\", \"tail\": \"0s\" } }],", StringComparison.Ordinal));
		var document = ProjectDocument.Open(disk, root);
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Files = document };
		using var host = WorkbenchFolder.Open(root, workbench);
		host.Update();
		host.Current!.Problems.ShouldBeEmpty();

		workbench.SongExports.ShouldBe([("short", "A short one")]);
		using (var wav = new MemoryStream())
		{
			workbench.RenderExport("short", wav).ShouldBeNull();
			System.Text.Encoding.ASCII.GetString(wav.ToArray(), 0, 4).ShouldBe("RIFF");
			wav.Length.ShouldBeGreaterThan(2 * 48_000 * 3 * 2);
		}

		// A recording: the song from its start, a cue moved in its second bar, an event there
		workbench.StartRecording().ShouldBeNull();
		workbench.ViewModel.IsRecording.ShouldBeTrue();
		void Bars(double bars)
		{
			for (var frames = 0; frames < bars * 96_000; frames += 256)
			{
				workbench.Update(256 / 48_000.0);
				session.Render(256);
			}
		}
		Bars(1.25);
		workbench.ViewModel.Cues.Single(c => c.Name == "mood").SelectedOption = 1;
		Bars(1);
		workbench.StopRecording("take").ShouldBeNull();
		var recorded = document.ReadText(song);
		recorded.ShouldContain("\"name\": \"take\"");
		recorded.ShouldContain("\"cue\": \"mood\"");
		recorded.ShouldContain("\"to\": \"Dark\"");
		recorded.ShouldContain("\"at\": \"2:");
	}
}
