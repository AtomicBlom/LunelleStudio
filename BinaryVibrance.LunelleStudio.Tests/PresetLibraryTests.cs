using System.Diagnostics;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Sirrow;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// The presets the studio ships (presets/): every one reads, builds and sounds; and a project of files alone tries one on
/// the keyboard, takes one with what it needs, and opens it once it has.
/// </summary>
public class PresetLibraryTests
{
	private const string Project = """
		{
		  "format": "lunelle-project/1",
		  "name": "Tunes",
		  "cues": [{ "name": "intensity", "initial": 0.5 }],
		  "macros": { "bright": 0 }
		}
		""";

	private const string Song = """
		{
		  "format": "lunelle-song/2",
		  "name": "tune",
		  "tracks": [{ "name": "lead", "patch": "tone" }],
		  "sections": [{ "name": "verse", "bars": 1, "parts": [{ "track": "lead", "line": "quarters", "notes": "1 3 5 8" }] }]
		}
		""";

	private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, ".."));

	private static PresetLibrary Shipped() => PresetLibrary.FromDirectory(Path.Combine(RepositoryRoot(), "presets"));

	[Test]
	public void The_shipped_presets_read_whole_and_list_each_kit_s_patches_with_it_not_alone()
	{
		var library = Shipped();
		library.Problems.ShouldBeEmpty();
		var patches = library.Presets.Where(p => !p.IsKit).ToList();
		var kits = library.Presets.Where(p => p.IsKit).ToList();
		patches.Count.ShouldBe(37);
		kits.Select(k => (k.Name, k.Category)).ShouldBe(
			[("breaks", "synthesised"), ("breaks-deep", "synthesised"), ("general", "sampled"), ("rock", "sampled"), ("synth", "synthesised")]);
		patches.Select(p => p.Category).Distinct().ShouldBe(["bass", "guitars", "keys", "leads", "mallets", "orchestra", "percussion", "synths"]);
		patches.ShouldNotContain(p => p.Name == "synth-kick" || p.Name == "breaks-snare" || p.Name == "breaks-deep-ride");
		patches.ShouldContain(p => p.Name == "synth-bass");
		library.Presets.ShouldAllBe(p => p.About.Length > 0);
		library.Presets.ShouldNotContain(p => p.About.Contains("user picked") || p.About.Contains(@"D:\"));
	}

	[Test]
	public void Every_shipped_preset_builds_and_sounds()
	{
		var library = Shipped();
		foreach (var preset in library.Presets)
		{
			using var session = new OfflineSession();
			var chip = new SynthChip(new ChipOptions { SpaceEffects = [], EchoEffects = [] });
			session.Engine.Play(chip);
			if (preset.IsKit)
			{
				var kit = library.BuildKit(preset.Name);
				var drums = chip.AddKit(kit);
				chip.Hit(drums, kit.Pieces[0].Piece, 0.8f);
			}
			else
			{
				var instrument = chip.AddInstrument(library.BuildPatch(preset.Name));
				chip.NoteOn(instrument, 60f, 0.8f);
			}
			session.Render(48_000 / 2);
			var peak = session.Left().Max(MathF.Abs);
			peak.ShouldBeGreaterThan(0.001f, preset.Name);
			float.IsFinite(peak).ShouldBeTrue(preset.Name);
		}
	}

	[Test]
	public void A_kit_brings_its_samples_folder_or_its_pieces_patches_and_a_patch_the_macros_it_names()
	{
		var library = Shipped();
		var general = library.FilesFor(library.Presets.Single(p => p.Name == "general"));
		general.ShouldContain(f => f.Path == "Kits/general/kit.json");
		general.ShouldContain(f => f.Path == "Kits/general/LICENSE.txt");
		general.Count(f => f.Path.EndsWith(".ogg", StringComparison.Ordinal)).ShouldBeGreaterThan(20);
		general.ShouldAllBe(f => f.Path.StartsWith("Kits/general/", StringComparison.Ordinal));

		var synth = library.FilesFor(library.Presets.Single(p => p.Name == "synth"));
		synth.Select(f => f.Path).ShouldContain("Kits/synth.kit.json");
		synth.Where(f => f.Patch is not null).Select(f => f.Path).ShouldContain("Patches/synth-kick.patch.json");
		synth.Count(f => f.Patch is not null).ShouldBe(9);

		var supersaw = library.Presets.Single(p => p.Name == "supersaw");
		library.FilesFor(supersaw).ShouldHaveSingleItem().Path.ShouldBe("Patches/supersaw.patch.json");
		library.MacrosFor(supersaw).ShouldBe(new Dictionary<string, int> { ["bright"] = 0, ["sweep"] = 2 });
	}

	// A project of files alone in memory, as the browser keeps one, its workbench offered the shipped presets
	private sealed class Bench : IDisposable
	{
		public readonly string Root = Path.Combine(Path.GetTempPath(), "lunelle-studio-presets-" + Guid.NewGuid().ToString("N")[..8]);
		public readonly MemoryFiles Files = new();
		public readonly OfflineSession Session = new();
		public readonly Workbench Workbench;
		public readonly WorkbenchFolder Host;

		public Bench()
		{
			Files.WriteText(Path.Combine(Root, MusicFolder.ProjectFileName), Project);
			Files.WriteText(Path.Combine(Root, "Patches", "tone.patch.json"),
				PatchFile.Write(PatchSpec.From(Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Adsr(0.005f, 0.1f, 0.6f, 0.1f)))))));
			Files.WriteText(Path.Combine(Root, "Songs", "tune.song.json"), Song);
			Workbench = new Workbench(Session.Engine, Session.Engine.Master, new WorkbenchCatalog()) { Files = Files, Presets = Shipped() };
			Host = WorkbenchFolder.Open(Root, Workbench);
			Host.Update();
		}

		public PresetViewModel Preset(string name) => Workbench.ViewModel.Presets.Single(p => p.Name == name);

		// Until the folder has been read again after a write
		public void ReadAgain()
		{
			var first = Host.Current;
			var deadline = Stopwatch.StartNew();
			while (ReferenceEquals(Host.Current, first) && deadline.Elapsed < TimeSpan.FromSeconds(10))
			{
				Host.Update();
				Thread.Sleep(20);
			}
			Host.Current.ShouldNotBeSameAs(first);
		}

		public void Dispose()
		{
			Host.Dispose();
			Workbench.Dispose();
			Session.Dispose();
		}
	}

	[Test]
	public void A_project_of_files_alone_tries_a_preset_on_the_keys_before_it_has_it()
	{
		using var bench = new Bench();
		var model = bench.Workbench.ViewModel;
		model.HasPresets.ShouldBeTrue();
		model.Presets.Count.ShouldBe(42);
		bench.Preset("harp").InProject.ShouldBeFalse();

		bench.Preset("harp").TryCommand.Execute(null);
		model.DockLabel.ShouldBe("preset harp");
		model.TryingPreset.ShouldBe(model.Presets.IndexOf(bench.Preset("harp")));
		model.PresetAbout.ShouldStartWith("harp on the keys. A harp");
		var note = bench.Workbench.AuditionOn(60f, 0.8f);
		bench.Session.Render(48_000 / 4);
		bench.Session.Left().Max(MathF.Abs).ShouldBeGreaterThan(0.01f);
		bench.Workbench.AuditionOff(note);
		bench.Files.Exists(Path.Combine(bench.Root, "Patches", "harp.patch.json")).ShouldBeFalse();

		// A kit tried puts its pieces on the pads too; the project's own patch chosen takes the keyboard back
		bench.Preset("rock").TryCommand.Execute(null);
		model.Pads.Select(p => p.Piece).ShouldContain("kick");
		bench.Workbench.ChoosePatch(0);
		model.TryingPreset.ShouldBe(-1);
		model.PresetAbout.ShouldBe("");

		// The filter finds a preset by its kind as well as its name
		model.LibraryQuery = "bass";
		bench.Preset("upright-bass").IsVisible.ShouldBeTrue();
		bench.Preset("harp").IsVisible.ShouldBeFalse();
	}

	[Test]
	public void An_added_patch_is_copied_with_its_macros_named_and_opens_once_the_project_has_it()
	{
		using var bench = new Bench();
		bench.Workbench.AddPreset(bench.Preset("supersaw").Preset).ShouldBeNull();
		var added = bench.Files.ReadText(Path.Combine(bench.Root, "Patches", "supersaw.patch.json"));
		PatchFile.Read(added).About.ShouldStartWith("A supersaw");
		var project = bench.Files.ReadText(Path.Combine(bench.Root, MusicFolder.ProjectFileName));
		project.ShouldContain("\"bright\": 0");
		project.ShouldContain("\"sweep\": 2");
		project.ShouldNotContain("drive");
		project.ShouldContain("\"intensity\"");

		bench.ReadAgain();
		bench.Host.Current!.Patches.Select(p => p.Name).ShouldBe(["supersaw", "tone"], ignoreOrder: true);
		bench.Host.Current.MacroName(2).ShouldBe("sweep");
		var model = bench.Workbench.ViewModel;
		model.SelectedPatch.ShouldBe(model.Patches.Single(p => p.Name == "supersaw").Index);
		bench.Preset("supersaw").InProject.ShouldBeTrue();
		bench.Preset("supersaw").AddText.ShouldBe("open");

		// Added again, it opens the project's own: nothing is written
		bench.Workbench.ChoosePatch(model.Patches.Single(p => p.Name == "tone").Index);
		bench.Workbench.AddPreset(bench.Preset("supersaw").Preset).ShouldBeNull();
		model.SelectedPatch.ShouldBe(model.Patches.Single(p => p.Name == "supersaw").Index);
	}

	[Test]
	public void An_added_kit_brings_what_it_plays_and_stops_at_a_file_of_the_project_s_that_differs()
	{
		using var bench = new Bench();
		bench.Workbench.AddPreset(bench.Preset("general").Preset).ShouldBeNull();
		bench.Files.Exists(Path.Combine(bench.Root, "Kits", "general", "kit.json")).ShouldBeTrue();
		bench.Files.Exists(Path.Combine(bench.Root, "Kits", "general", "LICENSE.txt")).ShouldBeTrue();
		bench.ReadAgain();
		bench.Host.Current!.Kits.Select(k => k.Name).ShouldContain("general");
		var model = bench.Workbench.ViewModel;
		model.SelectedKit.ShouldBe(model.Kits.Single(k => k.Name == "general").Index);
		bench.Host.Current.Kits.Single(k => k.Name == "general").Build().Pieces.Count.ShouldBeGreaterThan(8);

		// A piece's patch the project has, but its own: the kit isn't added, and nothing of it is written
		bench.Files.WriteText(Path.Combine(bench.Root, "Patches", "synth-kick.patch.json"),
			PatchFile.Write(PatchSpec.From(Patch.Build("synth-kick", p => p.Out(p.Sine(1f, Env.Decay(0.2f)))))));
		bench.ReadAgain();
		bench.Workbench.AddPreset(bench.Preset("synth").Preset).ShouldBe(
			"The project has Patches/synth-kick.patch.json already, and it isn't the preset's. Rename it to add synth.");
		bench.Files.Exists(Path.Combine(bench.Root, "Kits", "synth.kit.json")).ShouldBeFalse();
		bench.Files.Exists(Path.Combine(bench.Root, "Patches", "synth-snare.patch.json")).ShouldBeFalse();

		// The same patch, though, is taken as it is
		bench.Files.WriteBytes(Path.Combine(bench.Root, "Patches", "synth-kick.patch.json"),
			Shipped().FilesFor(bench.Preset("synth").Preset).Single(f => f.Patch == "synth-kick").Bytes);
		bench.ReadAgain();
		bench.Workbench.AddPreset(bench.Preset("synth").Preset).ShouldBeNull();
		bench.ReadAgain();
		bench.Host.Current!.Kits.Select(k => k.Name).ShouldContain("synth");
		bench.Host.Current.Kits.Single(k => k.Name == "synth").Build().Pieces.Count.ShouldBe(9);
	}

	[Test]
	public void A_project_with_code_is_offered_no_presets()
	{
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog()) { Presets = Shipped() };
		workbench.ViewModel.HasPresets.ShouldBeFalse();
		workbench.ViewModel.Presets.ShouldBeEmpty();
		workbench.AddPreset(Shipped().Presets[0]).ShouldBe("Presets go into a project of music files alone, and this one's music is C#.");
	}
}
