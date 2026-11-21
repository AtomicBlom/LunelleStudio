namespace Loom.Audio.Workbench;

/// <summary>How wide Arrange draws a section, shared by the timeline and the lanes' columns so they line up.</summary>
internal static class ArrangeLayout
{
	/// <summary>The width of each bar of a section.</summary>
	public const float PixelsPerBar = 34f;

	/// <summary>The narrowest a section is drawn, so a short one still holds its parts' clips.</summary>
	public const float MinSectionWidth = 220f;

	public static float SectionWidth(int bars) => MathF.Max(MinSectionWidth, bars * PixelsPerBar);

	/// <summary>How tall one part's clip is in a lane: its title row and its passes.</summary>
	public const float ClipHeight = 58f;

	/// <summary>The row under a cell's parts with a button to add another.</summary>
	public const float AddRowHeight = 24f;

	/// <summary>A lane tall enough for the most parts its track plays in one section, and a row under them to add another.</summary>
	public static float LaneHeight(int parts) => 12f + Math.Max(1, parts) * ClipHeight + (parts > 0 ? AddRowHeight : 0f);
}
