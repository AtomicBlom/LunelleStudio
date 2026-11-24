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
			offered.Select(c => c.Text).ShouldBe(["Use the Console theme"]);

			offered[0].Run();
			Workbench.ThemeName.ShouldBe("Console");
			reported.ShouldBe(["Console"]);
			workbench.PaletteCommands().Where(c => c.Category == "Theme").Select(c => c.Text).ShouldBe(["Use the Slate theme"]);

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
