using Vysara;
using Vysara.Media;
using Path = Vysara.Shapes.Path;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The workbench's icons, stroked with round ends in a 20 square as the rail's are, made at any size: what a library row,
/// a lane's header, a file and the inspector's title are, and the small marks on buttons (play, add, up and down, close, delete) and a handle to drag by.
/// The patch, kit and files icons are the rail's own.
/// </summary>
internal static class Glyphs
{
	public const string SongData = "M8,15.5 A2.5,2.5 0 1 1 3,15.5 A2.5,2.5 0 1 1 8,15.5 Z M17,13.5 A2.5,2.5 0 1 1 12,13.5 A2.5,2.5 0 1 1 17,13.5 Z M8,15.5 L8,4.5 L17,2.5 L17,13.5 M8,8 L17,6";
	public const string PatchData = "M6.7,5 A2.2,2.2 0 1 1 2.3,5 A2.2,2.2 0 1 1 6.7,5 Z M6.7,15 A2.2,2.2 0 1 1 2.3,15 A2.2,2.2 0 1 1 6.7,15 Z M17.7,10 A2.2,2.2 0 1 1 13.3,10 A2.2,2.2 0 1 1 17.7,10 Z M6.7,5.6 L13.3,9.2 M6.7,14.4 L13.3,10.8";
	public const string KitData = "M3.7,2.5 L7.8,2.5 A1.2,1.2 0 0 1 9,3.7 L9,7.8 A1.2,1.2 0 0 1 7.8,9 L3.7,9 A1.2,1.2 0 0 1 2.5,7.8 L2.5,3.7 A1.2,1.2 0 0 1 3.7,2.5 Z M12.2,2.5 L16.3,2.5 A1.2,1.2 0 0 1 17.5,3.7 L17.5,7.8 A1.2,1.2 0 0 1 16.3,9 L12.2,9 A1.2,1.2 0 0 1 11,7.8 L11,3.7 A1.2,1.2 0 0 1 12.2,2.5 Z M3.7,11 L7.8,11 A1.2,1.2 0 0 1 9,12.2 L9,16.3 A1.2,1.2 0 0 1 7.8,17.5 L3.7,17.5 A1.2,1.2 0 0 1 2.5,16.3 L2.5,12.2 A1.2,1.2 0 0 1 3.7,11 Z M12.2,11 L16.3,11 A1.2,1.2 0 0 1 17.5,12.2 L17.5,16.3 A1.2,1.2 0 0 1 16.3,17.5 L12.2,17.5 A1.2,1.2 0 0 1 11,16.3 L11,12.2 A1.2,1.2 0 0 1 12.2,11 Z";
	public const string CodeData = "M7,4 C5,4 5,5.5 5,7 C5,8.5 4,10 3,10 C4,10 5,11.5 5,13 C5,14.5 5,16 7,16 M13,4 C15,4 15,5.5 15,7 C15,8.5 16,10 17,10 C16,10 15,11.5 15,13 C15,14.5 15,16 13,16";
	public const string CueData = "M3,15 L3,11 M7.5,15 L7.5,7 M12,15 L12,9 M16.5,15 L16.5,4 M2,17.5 L18,17.5";
	public const string TrackData = "M2,6 L18,6 M2,10 L18,10 M2,14 L18,14 M6,4 L6,8 M13,8 L13,12 M9,12 L9,16";
	public const string SectionData = "M3,3 L3,17 M17,3 L17,17 M3,10 L17,10 M7,6.5 L13,6.5 M7,13.5 L11,13.5";
	public const string FileData = "M5,2.5 L12,2.5 L16,6.5 L16,17.5 L5,17.5 Z M12,2.5 L12,6.5 L16,6.5";
	private const string PlayData = "M5,3 L16,10 L5,17 Z";
	private const string PlusData = "M10,3 L10,17 M3,10 L17,10";
	private const string UpData = "M4,13 L10,7 L16,13";
	private const string DownData = "M4,7 L10,13 L16,7";
	private const string CloseData = "M4,4 L16,16 M16,4 L4,16";
	private const string TrashData = "M3.5,5.5 L16.5,5.5 M8,5.5 L8,3 L12,3 L12,5.5 M5.5,5.5 L6.5,17 L13.5,17 L14.5,5.5 M8.5,8.5 L8.8,14 M11.5,8.5 L11.2,14";
	private const string GripData = "M6,5 L14,5 M6,10 L14,10 M6,15 L14,15";

	public static Path Song(float size, Brush? stroke = null) => Make(SongData, size, stroke);
	public static Path Patch(float size, Brush? stroke = null) => Make(PatchData, size, stroke);
	public static Path Kit(float size, Brush? stroke = null) => Make(KitData, size, stroke);
	public static Path Code(float size, Brush? stroke = null) => Make(CodeData, size, stroke);
	public static Path Cue(float size, Brush? stroke = null) => Make(CueData, size, stroke);
	public static Path Track(float size, Brush? stroke = null) => Make(TrackData, size, stroke);
	public static Path Section(float size, Brush? stroke = null) => Make(SectionData, size, stroke);
	public static Path File(float size, Brush? stroke = null) => Make(FileData, size, stroke);
	public static Path Play(float size, Brush? stroke = null) => Make(PlayData, size, stroke, fill: true);
	public static Path Plus(float size, Brush? stroke = null) => Make(PlusData, size, stroke);
	public static Path Up(float size, Brush? stroke = null) => Make(UpData, size, stroke);
	public static Path Down(float size, Brush? stroke = null) => Make(DownData, size, stroke);
	public static Path Close(float size, Brush? stroke = null) => Make(CloseData, size, stroke);
	public static Path Trash(float size, Brush? stroke = null) => Make(TrashData, size, stroke);
	public static Path Grip(float size, Brush? stroke = null) => Make(GripData, size, stroke);

	private static Path Make(string data, float size, Brush? stroke, bool fill = false) => new()
	{
		Data = Geometry.Parse(data),
		Width = size,
		Height = size,
		Stretch = Stretch.Uniform,
		Stroke = stroke ?? Theme.Ink,
		Fill = fill ? stroke ?? Theme.Ink : null,
		StrokeThickness = 1.6f,
		StrokeStartLineCap = PenLineCap.Round,
		StrokeEndLineCap = PenLineCap.Round,
		StrokeLineJoin = PenLineJoin.Round,
		IsHitTestVisible = false,
		VerticalAlignment = VerticalAlignment.Center,
	};
}
