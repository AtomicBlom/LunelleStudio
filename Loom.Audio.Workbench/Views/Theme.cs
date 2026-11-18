using SUIsei;
using SUIsei.Media;

namespace Loom.Audio.Workbench.Views;

/// <summary>The workbench's colours, shared by its views: a dark page, light ink, a warm accent for what is on.</summary>
internal static class Theme
{
	public static SolidColorBrush Page { get; } = Brush(18, 20, 26);
	public static SolidColorBrush Panel { get; } = Brush(28, 31, 40);
	public static SolidColorBrush Ink { get; } = Brush(226, 230, 238);
	public static SolidColorBrush Dim { get; } = Brush(140, 148, 164);
	public static SolidColorBrush Accent { get; } = Brush(255, 196, 64);
	public static SolidColorBrush Off { get; } = Brush(60, 64, 78);
	public static SolidColorBrush Meter { get; } = Brush(96, 214, 128);
	public static SolidColorBrush Error { get; } = Brush(150, 40, 44);
	public static SolidColorBrush WhiteKey { get; } = Brush(232, 232, 236);
	public static SolidColorBrush BlackKey { get; } = Brush(30, 30, 36);
	public static SolidColorBrush Note { get; } = Brush(120, 180, 255);
	public static SolidColorBrush BarLine { get; } = Brush(70, 76, 92);
	public static SolidColorBrush Playhead { get; } = Brush(255, 196, 64, 200);

	public static SolidColorBrush Brush(byte r, byte g, byte b, byte a = 255) => new(HdrColor.FromRgb(r, g, b, a));
}
