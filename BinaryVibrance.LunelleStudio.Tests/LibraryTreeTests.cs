using BinaryVibrance.Sirrow;
using BinaryVibrance.LunelleStudio;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>The library as a tree (docs/spec/ui-library.md): folders that head their rows and fold away, and the kinds shown.</summary>
public class LibraryTreeTests
{
	private static Workbench Bench(OfflineSession session) => new(session.Engine, session.Engine.Master, new WorkbenchCatalog
	{
		Patches = [("bass/sub", () => MusicRig.Click), ("bass/wobble", () => MusicRig.Click), ("lead", () => MusicRig.Click), ("pads/warm", () => MusicRig.Click)],
	});

	[Test]
	public void A_folders_first_row_heads_it_and_folding_hides_its_rows()
	{
		using var session = new OfflineSession();
		using var bench = Bench(session);
		var patches = bench.ViewModel.Patches;

		patches.Select(p => p.ShowsFolder).ShouldBe([true, false, false, true]);
		patches.Select(p => p.IsVisible).ShouldBe([true, true, true, true]);

		patches[1].FoldCommand.Execute(null);
		patches.Select(p => p.IsVisible).ShouldBe([false, false, true, true]);
		// The header stays, to open it again, marked folded
		(patches[0].ShowsFolder, patches[0].FolderGlyph).ShouldBe((true, "▸"));

		// Words find a row inside a folded folder
		bench.ViewModel.LibraryQuery = "wob";
		patches.Select(p => p.IsVisible).ShouldBe([false, true, false, false]);
		patches.Select(p => p.ShowsFolder).ShouldBe([false, true, false, false]);

		bench.ViewModel.LibraryQuery = "";
		patches[0].FoldCommand.Execute(null);
		patches.Select(p => p.IsVisible).ShouldBe([true, true, true, true]);
	}

	[Test]
	public void No_kind_ticked_shows_every_kind_and_ticking_shows_those_alone()
	{
		using var session = new OfflineSession();
		using var bench = Bench(session);
		var model = bench.ViewModel;
		(model.SongsShown, model.PatchesShown, model.KitsShown).ShouldBe((true, true, true));

		model.FilterPatches = true;
		(model.SongsShown, model.PatchesShown, model.KitsShown).ShouldBe((false, true, false));
		model.FilterKits = true;
		(model.SongsShown, model.PatchesShown, model.KitsShown).ShouldBe((false, true, true));
		// Patterns show only where the project has some
		model.FilterPatterns = true;
		model.PatternsShown.ShouldBeFalse();
	}
}
