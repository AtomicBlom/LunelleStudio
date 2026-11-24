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
		  "format": "lunelle-project/1",
		  "name": "Tunes",
		  "cues": [{ "name": "intensity", "initial": 0.5 }, { "name": "mood", "options": ["Calm", "Dark"] }],
		  "macros": { "bright": 0 }
		}
		""";

	private static string Song(int tempo) => $$"""
		{
		  "format": "lunelle-song/2",
		  "name": "tune",
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
		music.Name.ShouldBe("Data Tunes");
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
		files.ReadText(Path.Combine(root, "Songs", "fresh.song.json")).ShouldContain("\"name\": \"fresh\"");
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
}
