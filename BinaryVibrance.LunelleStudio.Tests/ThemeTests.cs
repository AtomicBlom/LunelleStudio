using BinaryVibrance.Sirrow;
using BinaryVibrance.LunelleStudio.Views;
using BinaryVibrance.Vysara;
using Shouldly;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// The workbench's themes: each complete, applied to the same brushes in place, and chosen from the command palette. The
/// theme is the whole app's, so these run alone and leave the workbench in the theme it starts in.
/// </summary>
[NotInParallel("theme")]
public class ThemeTests
{
	[Test]
	public void Every_theme_has_a_name_of_its_own_and_a_hue_for_every_track()
	{
		ThemeDefinition.All[0].ShouldBeSameAs(ThemeDefinition.Slate);
		ThemeDefinition.All.Select(t => t.Name.ToLowerInvariant()).ShouldBeUnique();
		foreach (var theme in ThemeDefinition.All)
		{
			theme.TrackHues.Length.ShouldBe(Theme.TrackHueCount, theme.Name);
			ThemeDefinition.Find(theme.Name.ToUpperInvariant()).ShouldBeSameAs(theme);
		}
		ThemeDefinition.Find("no such theme").ShouldBeNull();
	}

	[Test]
	public void Slate_glows_nowhere_so_it_shows_as_it_always_has()
	{
		var slate = ThemeDefinition.Slate;
		slate.Glow.ShouldBe(0f);
		slate.Lamp.ShouldBe(slate.Meter);
		slate.Level.ShouldBe(slate.Meter);
		slate.TrackLampLight.ShouldBe(1f);
		// Its keys flat, a toggle that is on filled with the accent, no window behind a readout
		slate.KeyFace.Top.ShouldBe(slate.Raised);
		slate.KeyFace.Bottom.ShouldBe(slate.Raised);
		slate.KeyFaceOver.Top.ShouldBe(slate.RaisedOver);
		slate.KeyFaceOver.Bottom.ShouldBe(slate.RaisedOver);
		slate.KeyLit.ShouldBe(slate.Accent);
		slate.Window.A.ShouldBe(0f);
		slate.WindowInk.ShouldBe(slate.Ink);
	}

	[Test]
	public void Slate_names_no_fonts_and_every_font_a_theme_names_is_baked_by_the_app()
	{
		ThemeDefinition.Slate.LabelFont.ShouldBeNull();
		ThemeDefinition.Slate.ReadoutFont.ShouldBeNull();

		// The app bakes each Assets/Fonts/<name>.fontset.json into the atlas a theme names
		var assets = Path.Combine(RepositoryRoot(), "BinaryVibrance.LunelleStudio.App", "Assets", "Fonts");
		foreach (var theme in ThemeDefinition.All)
			foreach (var font in new[] { theme.LabelFont, theme.LabelBoldFont, theme.ReadoutFont })
				if (font is not null)
					File.Exists(Path.Combine(assets, $"{font}.fontset.json")).ShouldBeTrue($"{theme.Name} names '{font}'");
	}

	private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, ".."));

	[Test]
	public void A_lit_colour_gives_that_many_times_its_light()
	{
		var orange = HdrColor.FromRgb(255, 138, 61);
		var lit = ThemeDefinition.Lit(orange, 2.5f);

		lit.A.ShouldBe(orange.A);
		// In linear light, as Vysara draws it: half precision, so to a few parts in a thousand
		var (before, after) = (orange.ToPremultiplied().ToVector4(), lit.ToPremultiplied().ToVector4());
		after.X.ShouldBe(before.X * 2.5f, 0.005f);
		after.Y.ShouldBe(before.Y * 2.5f, 0.005f);
		after.Z.ShouldBe(before.Z * 2.5f, 0.005f);
		ThemeDefinition.Lit(orange, 1f).R.ShouldBe(orange.R, 1e-5f);
	}

	[Test]
	public void A_theme_recolours_the_brushes_the_views_already_hold()
	{
		var accent = Theme.Accent;
		var face = Theme.PanelFace;
		var hue = Theme.TrackHue(9);
		try
		{
			Theme.Apply(ThemeDefinition.Console);

			Theme.Accent.ShouldBeSameAs(accent);
			Theme.PanelFace.ShouldBeSameAs(face);
			Theme.TrackHue(9).ShouldBeSameAs(hue);
			accent.Color.ShouldBe(ThemeDefinition.Console.Accent);
			face.GradientStops[0].Color.ShouldBe(ThemeDefinition.Console.PanelFace.Top);
			face.GradientStops[1].Color.ShouldBe(ThemeDefinition.Console.PanelFace.Bottom);
			hue.Color.ShouldBe(ThemeDefinition.Console.TrackHues[1]);
			Theme.AccentSoft.Color.ShouldBe(ThemeDefinition.Console.Accent with { A = 41 * ThemeDefinition.Console.AccentWash / 255f });
			Theme.TrackHueSoft(1).Color.ShouldBe(ThemeDefinition.Console.TrackHues[1] with { A = 38 / 255f });
		}
		finally
		{
			Theme.Apply(ThemeDefinition.Slate);
		}

		// Back where it started: Slate's colours, as the workbench was made with
		accent.Color.ShouldBe(HdrColor.FromRgb(92, 170, 255));
		Theme.Panel.Color.ShouldBe(HdrColor.FromRgb(28, 31, 40));
		Theme.Hover.Color.ShouldBe(HdrColor.FromRgb(226, 230, 238, 9));
		Theme.Current.ShouldBeSameAs(ThemeDefinition.Slate);
	}

	[Test]
	public void The_palette_offers_the_other_themes_and_a_choice_is_reported()
	{
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog());
		var reported = new List<string>();
		workbench.ThemeChanged += reported.Add;
		try
		{
			var offered = workbench.PaletteCommands().Where(c => c.Category == "Theme").ToList();
			offered.Select(c => c.Text).ShouldBe(["Use the Console theme", "Use the Faceplate theme", "Use the Bench theme"]);

			offered[0].Run();
			Workbench.ThemeName.ShouldBe("Console");
			reported.ShouldBe(["Console"]);
			workbench.PaletteCommands().Where(c => c.Category == "Theme").Select(c => c.Text).ShouldBe(["Use the Slate theme", "Use the Faceplate theme", "Use the Bench theme"]);

			workbench.SetTheme("console").ShouldBeTrue();
			reported.Count.ShouldBe(1);
			workbench.SetTheme("no such theme").ShouldBeFalse();
			Workbench.ThemeName.ShouldBe("Console");
		}
		finally
		{
			workbench.SetTheme("Slate");
		}
		reported.ShouldBe(["Console", "Slate"]);
	}
}
