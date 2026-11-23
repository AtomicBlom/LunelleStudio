using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Media;

namespace BinaryVibrance.SirrowStudio.Views;

/// <summary>The workbench's colours, shared by its views: a dark page, light ink, a cool blue accent for what is on, a cyan
/// playhead, and a hue for each track that its lane, notes and lit keys share.</summary>
internal static class Theme
{
	public static SolidColorBrush Page { get; } = Brush(18, 20, 26);
	public static SolidColorBrush Panel { get; } = Brush(28, 31, 40);
	public static SolidColorBrush Ink { get; } = Brush(226, 230, 238);
	public static SolidColorBrush Dim { get; } = Brush(140, 148, 164);
	public static SolidColorBrush Accent { get; } = Brush(92, 170, 255);
	public static SolidColorBrush Off { get; } = Brush(60, 64, 78);
	public static SolidColorBrush Meter { get; } = Brush(96, 214, 128);
	public static SolidColorBrush Error { get; } = Brush(150, 40, 44);
	public static SolidColorBrush WhiteKey { get; } = Brush(232, 232, 236);
	public static SolidColorBrush BlackKey { get; } = Brush(30, 30, 36);
	public static SolidColorBrush Note { get; } = Brush(120, 180, 255);
	public static SolidColorBrush BarLine { get; } = Brush(70, 76, 92);
	public static SolidColorBrush Playhead { get; } = Brush(95, 240, 230, 230);
	public static SolidColorBrush Raised { get; } = Brush(37, 41, 54);
	public static SolidColorBrush Sunk { get; } = Brush(22, 24, 31);
	public static SolidColorBrush Line { get; } = Brush(226, 230, 238, 30);
	public static SolidColorBrush LineStrong { get; } = Brush(226, 230, 238, 46);
	public static SolidColorBrush RaisedOver { get; } = Brush(46, 51, 66);
	public static SolidColorBrush AccentOver { get; } = Brush(124, 188, 255);
	public static SolidColorBrush AccentPressed { get; } = Brush(156, 204, 255);
	public static SolidColorBrush Rail { get; } = Brush(70, 74, 86);

	/// <summary>
	/// The built-in controls in the workbench's colours, under WinUI's lightweight-styling keys, for a view's resources: a
	/// button raised off its panel, a toggle that is on filled with the accent and its text dark, a slider's fill and thumb
	/// in the accent.
	/// </summary>
	public static ResourceDictionary Controls()
	{
		var resources = new ResourceDictionary();
		resources.Add("ButtonBackground", Raised);
		resources.Add("ButtonBackgroundPointerOver", RaisedOver);
		resources.Add("ButtonBackgroundPressed", Sunk);
		resources.Add("ButtonForeground", Ink);
		resources.Add("ToggleButtonBackground", Raised);
		resources.Add("ToggleButtonBackgroundPointerOver", RaisedOver);
		resources.Add("ToggleButtonBackgroundPressed", Sunk);
		resources.Add("ToggleButtonForeground", Ink);
		resources.Add("ToggleButtonBackgroundChecked", Accent);
		resources.Add("ToggleButtonForegroundChecked", AccentInk);
		resources.Add("SliderTrackFill", Rail);
		resources.Add("SliderTrackValueFill", Accent);
		resources.Add("SliderThumbBackground", Accent);
		resources.Add("SliderThumbBackgroundPointerOver", AccentOver);
		resources.Add("SliderThumbBackgroundPressed", AccentPressed);
		return resources;
	}

	/// <summary>Toggles that mark where you are rather than switch something on (the rail's modes, the section playing):
	/// lit with the accent's tint and their text in the accent, on a clear ground, as the design's rail is.</summary>
	public static ResourceDictionary Markers()
	{
		var resources = new ResourceDictionary();
		resources.Add("ToggleButtonBackground", Clear);
		resources.Add("ToggleButtonForeground", Dim);
		resources.Add("ToggleButtonBackgroundChecked", AccentSoft);
		resources.Add("ToggleButtonForegroundChecked", Accent);
		return resources;
	}

	public static SolidColorBrush Faint { get; } = Brush(93, 100, 118);
	public static SolidColorBrush AccentSoft { get; } = Brush(92, 170, 255, 41);
	public static SolidColorBrush AccentInk { get; } = Brush(11, 22, 38);
	public static SolidColorBrush ErrorSoft { get; } = Brush(224, 88, 93, 46);
	public static SolidColorBrush Backdrop { get; } = Brush(0, 0, 0, 120);
	public static SolidColorBrush Clear { get; } = Brush(0, 0, 0, 0);
	// Driving edges, and the colours of the operator kinds in the patch designer's graph
	public static SolidColorBrush Drive { get; } = Brush(79, 209, 197);
	public static SolidColorBrush KindSine { get; } = Brush(120, 180, 255);
	public static SolidColorBrush KindWave { get; } = Brush(183, 155, 255);
	public static SolidColorBrush KindNoise { get; } = Brush(163, 171, 187);
	public static SolidColorBrush KindString { get; } = Brush(240, 163, 94);
	public static SolidColorBrush KindModes { get; } = Brush(79, 209, 197);
	public static SolidColorBrush KindTube { get; } = Brush(132, 217, 126);
	public static SolidColorBrush KindLevel { get; } = Brush(207, 212, 222);
	public static SolidColorBrush KindSample { get; } = Brush(255, 143, 177);
	public static SolidColorBrush StepHit { get; } = Brush(92, 170, 255, 190);
	public static SolidColorBrush StepGhost { get; } = Brush(92, 170, 255, 90);
	public static SolidColorBrush ChordTone { get; } = Brush(92, 170, 255, 26);
	public static SolidColorBrush ChordRoot { get; } = Brush(92, 170, 255, 58);

	// One hue per track, in the order a song makes its tracks, round again past the last: told apart on a dark panel
	private static readonly SolidColorBrush[] TrackHues =
	[
		Brush(255, 138, 122), Brush(79, 209, 197), Brush(181, 156, 255), Brush(127, 211, 255),
		Brush(132, 217, 126), Brush(242, 138, 208), Brush(240, 176, 104), Brush(158, 170, 255),
	];

	private static readonly SolidColorBrush[] TrackHuesSoft = [.. TrackHues.Select(h => Brush(Byte(h.Color.R), Byte(h.Color.G), Byte(h.Color.B), 38))];

	/// <summary>The hue of the track at <paramref name="index"/> in its song: its lane's stripe, its notes, its lit keys.</summary>
	public static SolidColorBrush TrackHue(int index) => TrackHues[((index % TrackHues.Length) + TrackHues.Length) % TrackHues.Length];

	/// <summary>The same hue, faint: behind the pass of a track's part that is playing.</summary>
	public static SolidColorBrush TrackHueSoft(int index) => TrackHuesSoft[((index % TrackHues.Length) + TrackHues.Length) % TrackHues.Length];

	private static byte Byte(float channel) => (byte)Math.Clamp(MathF.Round(channel * 255f), 0f, 255f);

	public static SolidColorBrush Brush(byte r, byte g, byte b, byte a = 255) => new(HdrColor.FromRgb(r, g, b, a));
}
