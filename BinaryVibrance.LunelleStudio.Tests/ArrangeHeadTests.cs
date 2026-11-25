using BinaryVibrance.LunelleStudio.Views;
using BinaryVibrance.Sirrow;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// Arrange's heads over the lanes: the sections' heads stay on the top edge of the board as it scrolls, with nothing of the
/// lanes showing above them, and the first lane starts at their foot. Laid out on whole pixels at a scale that is not whole,
/// as the browser and a scaled display are.
/// </summary>
public sealed class ArrangeHeadTests
{
	[Test]
	public void The_section_heads_keep_to_the_top_edge_as_the_board_scrolls()
	{
		using var session = new OfflineSession();
		var catalog = new WorkbenchCatalog
		{
			Songs = [("first", () =>
			{
				var song = new BinaryVibrance.Lunelle.Music.Song("first");
				var verse = song.Section("verse", 4);
				var chorus = song.Section("chorus", 4);
				for (var i = 0; i < 12; i++)
				{
					var track = song.Track("t" + i, MusicRig.Click);
					verse.Play(track, BinaryVibrance.Lunelle.Music.Line.Quarters("1 3 5 3"));
					chorus.Play(track, BinaryVibrance.Lunelle.Music.Line.Quarters("1 3 5 3"));
				}
				return song;
			})],
			Patches = [("click", () => MusicRig.Click)],
		};
		using var workbench = new Workbench(session.Engine, session.Engine.Master, catalog);
		workbench.ViewModel.IsArrange = true;
		var host = new UIHost();
		var root = host.CreateRoot(UILayer.Default).Root;
		root.UseLayoutRounding = true;
		root.LayoutScale = 1.3f;
		root.Content = new WorkbenchView(workbench.ViewModel);
		var surface = new System.Numerics.Vector2(1800f, 1000f);
		void Frame()
		{
			workbench.Update(0.016);
			host.Tick(0.016, 0f, surface);
		}
		Frame();
		Frame();
		var lanes = Find<ScrollViewer>(root.Content!).Single(s => s.Name == "Lanes");
		var heads = Find<SectionHeader>(root.Content!);
		var corner = Find<Border>(root.Content!).Single(b => b.Name == "Corner");
		var firstLane = Find<TrackHeader>(root.Content!).First();
		heads.Count.ShouldBe(2);
		lanes.ScrollableHeight.ShouldBeGreaterThan(130f);
		// The row the heads share is as tall as a head whatever the corner's text measures, and a head keeps to the top of a
		// taller one: centred, it left a strip above it where the lanes showed (the corner's text, in the app's font, made
		// the row 89 tall)
		corner.LayoutRect.Height.ShouldBe(SectionHeader.Tall, 1f / 1.3f);
		heads[0].VerticalAlignment.ShouldBe(VerticalAlignment.Top);
		var tall = new Border { Height = SectionHeader.Tall + 10f, Child = new SectionHeader() };
		var alone = new UIRoot(UISurface.Hud) { Content = tall };
		alone.LayoutPass(new Size(300f, 300f));
		((SectionHeader)tall.Child!).LayoutRect.Y.ShouldBe(0f);

		// The first lane starts where the heads end, so neither leaves a strip between them
		(Top(firstLane) - Top(lanes)).ShouldBe(Top(heads[0]) + heads[0].LayoutRect.Height - Top(lanes), 0.01f);
		foreach (var offset in new[] { 0f, 13.37f, 40f, 77.7f, 120.2f })
		{
			lanes.ScrollToVerticalOffset(offset);
			Frame();
			foreach (var head in heads)
				Top(head).ShouldBe(Top(lanes), 0.01f, $"a section's head at offset {lanes.VerticalOffset}");
			Top(corner).ShouldBe(Top(lanes), 0.01f, $"the corner at offset {lanes.VerticalOffset}");
		}
	}

	// Where an element's top edge is drawn, in surface pixels
	private static float Top(UIElement e) => e.TransformToVisual(null).TransformBounds(new Rect(0f, 0f, e.LayoutRect.Width, e.LayoutRect.Height)).Y;

	// Every element of a kind in a tree
	private static List<T> Find<T>(UIElement top) where T : UIElement
	{
		var found = new List<T>();
		void Walk(UIElement element)
		{
			if (element is T t)
				found.Add(t);
			for (var i = 0; i < BinaryVibrance.Vysara.Media.VisualTreeHelper.GetChildrenCount(element); i++)
				Walk(BinaryVibrance.Vysara.Media.VisualTreeHelper.GetChild(element, i));
		}
		Walk(top);
		return found;
	}
}
