using System.Diagnostics;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Sirrow;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// The presets: Lunelle's own library, as a project of files alone is offered it. Every one reads, builds and sounds; a
/// project tries one on the keyboard, adds one as an entry of its own based on it (the library listed, its macros
/// named), and opens it once it has.
/// </summary>
public class PresetLibraryTests
{
	private const string Manifest = """
		{
		  "format": "lunelle/2",
		  "name": "tunes",
		  "macros": { "bright": 0 }
		}
		""";

	private const string Song = """
		{
		  "format": "lunelle-song/3",
		  "tracks": [{ "name": "lead", "patch": "tone" }],
		  "sections": [{ "name": "verse", "bars": 1, "parts": [{ "track": "lead", "line": "quarters", "notes": "1 3 5 8" }] }]
		}
		""";

	private static PresetLibrary Shipped() => PresetLibrary.Lunelle;

	[Test]
	public void The_library_reads_whole_and_lists_each_kit_s_patches_with_it_not_alone()
	{
		var library = Shipped();
		library.Problems.ShouldBeEmpty();
		library.Version.ShouldBe("1.0.0");
		var patches = library.Presets.Where(p => !p.IsKit).ToList();
		var kits = library.Presets.Where(p => p.IsKit).ToList();
		patches.Count.ShouldBe(47);
		kits.Select(k => (k.Name, k.Category)).ShouldBe(
			[("breaks", "synthesised"), ("breaks-deep", "synthesised"), ("general", "sampled"), ("grind", "synthesised"), ("rock", "sampled"), ("synth", "synthesised")]);
		patches.Select(p => p.Category).Distinct().ShouldBe(["bass", "guitars", "keys", "leads", "mallets", "orchestra", "percussion", "synths"]);
		patches.ShouldNotContain(p => p.Name.StartsWith("kits/", StringComparison.Ordinal));
		patches.ShouldContain(p => p.Name == "bass/synth-bass" && p.Reference == "lunelle:bass/synth-bass");
		library.Presets.ShouldAllBe(p => p.About.Length > 0);
		library.Presets.ShouldNotContain(p => p.About.Contains("user picked") || p.About.Contains(@"D:\"));
	}

	[Test]
	public void Every_preset_builds_and_sounds()
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
	public void A_preset_names_the_macros_its_patches_turn()
	{
		var library = Shipped();
		library.MacrosFor(library.Presets.Single(p => p.Name == "synths/supersaw")).ShouldBe(new Dictionary<string, int> { ["bright"] = 0, ["sweep"] = 2 });
	}

	// A project of files alone in memory, as the browser keeps one, its workbench offered the library
	private sealed class Bench : IDisposable
	{
		public readonly string Root = Path.Combine(Path.GetTempPath(), "lunelle-studio-presets-" + Guid.NewGuid().ToString("N")[..8]);
		public readonly MemoryFiles Files = new();
		public readonly OfflineSession Session = new();
		public readonly Workbench Workbench;
		public readonly WorkbenchFolder Host;

		public Bench()
		{
			Files.WriteText(Path.Combine(Root, MusicFolder.ProjectFileName), Manifest);
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
		model.Presets.Count.ShouldBe(53);
		bench.Preset("orchestra/harp").InProject.ShouldBeFalse();

		bench.Preset("orchestra/harp").TryCommand.Execute(null);
		model.DockLabel.ShouldBe("preset orchestra/harp");
		model.TryingPreset.ShouldBe(model.Presets.IndexOf(bench.Preset("orchestra/harp")));
		var note = bench.Workbench.AuditionOn(60f, 0.8f);
		bench.Session.Render(48_000 / 4);
		bench.Session.Left().Max(MathF.Abs).ShouldBeGreaterThan(0.01f);
		bench.Workbench.AuditionOff(note);
		bench.Files.Exists(Path.Combine(bench.Root, "Patches", "orchestra", "harp.patch.json")).ShouldBeFalse();

		// A kit tried puts its pieces on the pads too; the project's own patch chosen takes the keyboard back
		bench.Preset("rock").TryCommand.Execute(null);
		model.Pads.Select(p => p.Piece).ShouldContain("kick");
		bench.Workbench.ChoosePatch(0);
		model.TryingPreset.ShouldBe(-1);
		model.PresetAbout.ShouldBe("");

		// The filter finds a preset by its kind as well as its name
		model.LibraryQuery = "bass";
		bench.Preset("bass/upright-bass").IsVisible.ShouldBeTrue();
		bench.Preset("orchestra/harp").IsVisible.ShouldBeFalse();
	}

	[Test]
	public void An_added_patch_is_the_project_s_own_based_on_the_library_s_and_opens_once_the_project_has_it()
	{
		using var bench = new Bench();
		bench.Workbench.AddPreset(bench.Preset("synths/supersaw").Preset).ShouldBeNull();
		var added = JReader.Parse(bench.Files.ReadText(Path.Combine(bench.Root, "Patches", "synths", "supersaw.patch.json")));
		added["base"]!.Str.ShouldBe("lunelle:synths/supersaw");
		added["operators"].ShouldBeNull();
		var manifest = JReader.Parse(bench.Files.ReadText(Path.Combine(bench.Root, MusicFolder.ProjectFileName)));
		manifest["libraries"]!["lunelle"]!["version"]!.Str.ShouldBe("1");
		manifest["macros"]!["bright"]!.Num.ShouldBe("0");
		manifest["macros"]!["sweep"]!.Num.ShouldBe("2");
		manifest["macros"]!["drive"].ShouldBeNull();

		bench.ReadAgain();
		bench.Host.Current!.Problems.ShouldBeEmpty();
		bench.Host.Current.Patches.Select(p => p.Name).ShouldBe(["synths/supersaw", "tone"], ignoreOrder: true);
		bench.Host.Current.MacroName(2).ShouldBe("sweep");
		var model = bench.Workbench.ViewModel;
		model.SelectedPatch.ShouldBe(model.Patches.Single(p => p.Name == "synths/supersaw").Index);
		bench.Preset("synths/supersaw").InProject.ShouldBeTrue();
		bench.Preset("synths/supersaw").AddText.ShouldBe("open");

		// Edited in the designer, it is saved as what it changes over the library's, still based on it
		model.Designer.AddButtons[0].Command!.Execute(null);
		for (var seconds = 0.0; seconds < SongEditor.SaveAfterSeconds + 0.1; seconds += 256.0 / 48_000)
		{
			bench.Workbench.Update(256.0 / 48_000);
			bench.Session.Render(256);
		}
		var saved = JReader.Parse(bench.Files.ReadText(Path.Combine(bench.Root, "Patches", "synths", "supersaw.patch.json")));
		saved["base"]!.Str.ShouldBe("lunelle:synths/supersaw");
		saved["insert"]!.Items.Select(i => i["into"]!.Str).ShouldContain("operators");
		saved["operators"].ShouldBeNull();
		bench.ReadAgain();
		bench.Host.Current!.Problems.ShouldBeEmpty();

		// The designer says what it is based on and each change, and a change reverted is the base's again
		bench.Workbench.ChoosePatch(model.Patches.Single(p => p.Name == "synths/supersaw").Index);
		model.Designer.IsDerived.ShouldBeTrue();
		model.Designer.BasedOn.ShouldBe("lunelle:synths/supersaw");
		model.Designer.Overrides.Select(o => o.Kind).ShouldContain("insert");
		foreach (var change in model.Designer.Overrides.Reverse().ToList())
			bench.Workbench.RevertOverride(change.Kind, change.Path).ShouldBeNull();
		model.Designer.Overrides.ShouldBeEmpty();
		var reverted = JReader.Parse(bench.Files.ReadText(Path.Combine(bench.Root, "Patches", "synths", "supersaw.patch.json")));
		reverted["insert"].ShouldBeNull();
		reverted["base"]!.Str.ShouldBe("lunelle:synths/supersaw");

		// Added again, it opens the project's own: nothing is written
		bench.Workbench.ChoosePatch(model.Patches.Single(p => p.Name == "tone").Index);
		bench.Workbench.AddPreset(bench.Preset("synths/supersaw").Preset).ShouldBeNull();
		model.SelectedPatch.ShouldBe(model.Patches.Single(p => p.Name == "synths/supersaw").Index);
	}

	[Test]
	public void An_added_kit_is_the_project_s_own_based_on_the_library_s_and_plays_its_samples()
	{
		using var bench = new Bench();
		bench.Workbench.AddPreset(bench.Preset("general").Preset).ShouldBeNull();
		JReader.Parse(bench.Files.ReadText(Path.Combine(bench.Root, "Kits", "general.kit.json")))["base"]!.Str.ShouldBe("lunelle:general");
		bench.ReadAgain();
		bench.Host.Current!.Kits.Select(k => k.Name).ShouldContain("general");
		var model = bench.Workbench.ViewModel;
		model.SelectedKit.ShouldBe(model.Kits.Single(k => k.Name == "general").Index);
		bench.Host.Current.Kits.Single(k => k.Name == "general").Build().Pieces.Count.ShouldBeGreaterThan(8);
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
