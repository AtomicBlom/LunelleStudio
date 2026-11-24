using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Media;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// The workbench's colours, shared by its views: one brush per role (a page, panels, ink, an accent for what is on, a
/// playhead, a hue for each track that its lane, notes and lit keys share). The brushes are made once and never replaced:
/// <see cref="Apply"/> puts a <see cref="ThemeDefinition"/>'s colours into them in place, and Vysara repaints every element
/// showing a brush when its colour changes, so a theme changes the whole workbench at once with no view rebuilt. A view
/// takes a role's brush and keeps it; it never copies a brush's colour into a brush of its own.
/// </summary>
internal static class Theme
{
	/// <summary>How many track hues a theme has; a song's tracks go round them.</summary>
	public const int TrackHueCount = 8;

	// The accent's faint uses, as shares of opaque
	private const byte AccentSoftAlpha = 41, StepHitAlpha = 190, StepGhostAlpha = 90, ChordToneAlpha = 26, ChordRootAlpha = 58;
	private const byte TrackHueSoftAlpha = 38, HoverAlpha = 9;

	public static SolidColorBrush Page { get; } = new();
	public static SolidColorBrush Panel { get; } = new();

	/// <summary>A panel's face, shaded from its top to its bottom: what the workbench's panels are filled with.</summary>
	public static LinearGradientBrush PanelFace { get; } = Vertical();

	public static SolidColorBrush Ink { get; } = new();
	public static SolidColorBrush Dim { get; } = new();
	public static SolidColorBrush Faint { get; } = new();
	public static SolidColorBrush Accent { get; } = new();
	public static SolidColorBrush Off { get; } = new();
	public static SolidColorBrush Meter { get; } = new();
	public static SolidColorBrush Error { get; } = new();
	public static SolidColorBrush WhiteKey { get; } = new();
	public static SolidColorBrush BlackKey { get; } = new();
	public static SolidColorBrush Note { get; } = new();
	public static SolidColorBrush BarLine { get; } = new();
	public static SolidColorBrush Playhead { get; } = new();
	public static SolidColorBrush Raised { get; } = new();
	public static SolidColorBrush Sunk { get; } = new();
	public static SolidColorBrush Line { get; } = new();
	public static SolidColorBrush LineStrong { get; } = new();
	public static SolidColorBrush RaisedOver { get; } = new();
	public static SolidColorBrush AccentOver { get; } = new();
	public static SolidColorBrush AccentPressed { get; } = new();
	public static SolidColorBrush Rail { get; } = new();
	public static SolidColorBrush AccentSoft { get; } = new();
	public static SolidColorBrush AccentInk { get; } = new();
	public static SolidColorBrush ErrorSoft { get; } = new();
	public static SolidColorBrush Backdrop { get; } = new();

	/// <summary>Under the pointer: the ink, barely there.</summary>
	public static SolidColorBrush Hover { get; } = new();

	public static SolidColorBrush Clear { get; } = new(HdrColor.Transparent);

	// Driving edges, and the colours of the operator kinds in the patch designer's graph
	public static SolidColorBrush Drive { get; } = new();
	public static SolidColorBrush KindSine { get; } = new();
	public static SolidColorBrush KindWave { get; } = new();
	public static SolidColorBrush KindNoise { get; } = new();
	public static SolidColorBrush KindString { get; } = new();
	public static SolidColorBrush KindModes { get; } = new();
	public static SolidColorBrush KindTube { get; } = new();
	public static SolidColorBrush KindLevel { get; } = new();
	public static SolidColorBrush KindSample { get; } = new();
	public static SolidColorBrush StepHit { get; } = new();
	public static SolidColorBrush StepGhost { get; } = new();
	public static SolidColorBrush ChordTone { get; } = new();
	public static SolidColorBrush ChordRoot { get; } = new();

	private static readonly SolidColorBrush[] TrackHues = Brushes(TrackHueCount);
	private static readonly SolidColorBrush[] TrackHuesSoft = Brushes(TrackHueCount);

	/// <summary>The theme the brushes show.</summary>
	public static ThemeDefinition Current { get; private set; } = ThemeDefinition.Slate;

	/// <summary>Raised after <see cref="Apply"/> changed the theme, for what a theme sets other than a brush.</summary>
	public static event Action<ThemeDefinition>? Changed;

	static Theme() => Paint(ThemeDefinition.Slate);

	/// <summary>Show <paramref name="theme"/>: its colours put into every brush, in place. Must be called on the UI thread.</summary>
	public static void Apply(ThemeDefinition theme)
	{
		ArgumentNullException.ThrowIfNull(theme);
		if (ReferenceEquals(theme, Current))
			return;
		Paint(theme);
		Changed?.Invoke(theme);
	}

	private static void Paint(ThemeDefinition t)
	{
		if (t.TrackHues.Length != TrackHueCount)
			throw new ArgumentException($"Theme '{t.Name}' has {t.TrackHues.Length} track hues, not {TrackHueCount}.", nameof(t));
		Current = t;
		Page.Color = t.Page;
		Panel.Color = t.Panel;
		PanelFace.GradientStops[0].Color = t.PanelFace.Top;
		PanelFace.GradientStops[1].Color = t.PanelFace.Bottom;
		Ink.Color = t.Ink;
		Dim.Color = t.Dim;
		Faint.Color = t.Faint;
		Accent.Color = t.Accent;
		Off.Color = t.Off;
		Meter.Color = t.Meter;
		Error.Color = t.Error;
		WhiteKey.Color = t.WhiteKey;
		BlackKey.Color = t.BlackKey;
		Note.Color = t.Note;
		BarLine.Color = t.BarLine;
		Playhead.Color = t.Playhead;
		Raised.Color = t.Raised;
		Sunk.Color = t.Sunk;
		Line.Color = t.Line;
		LineStrong.Color = t.LineStrong;
		RaisedOver.Color = t.RaisedOver;
		AccentOver.Color = t.AccentOver;
		AccentPressed.Color = t.AccentPressed;
		Rail.Color = t.Rail;
		AccentInk.Color = t.AccentInk;
		ErrorSoft.Color = t.ErrorSoft;
		Backdrop.Color = t.Backdrop;
		Hover.Color = WithAlpha(t.Ink, HoverAlpha);
		Drive.Color = t.Drive;
		KindSine.Color = t.KindSine;
		KindWave.Color = t.KindWave;
		KindNoise.Color = t.KindNoise;
		KindString.Color = t.KindString;
		KindModes.Color = t.KindModes;
		KindTube.Color = t.KindTube;
		KindLevel.Color = t.KindLevel;
		KindSample.Color = t.KindSample;
		AccentSoft.Color = WithAlpha(t.Accent, AccentSoftAlpha * t.AccentWash);
		StepHit.Color = WithAlpha(t.Accent, StepHitAlpha);
		StepGhost.Color = WithAlpha(t.Accent, StepGhostAlpha * t.AccentWash);
		ChordTone.Color = WithAlpha(t.Accent, ChordToneAlpha * t.AccentWash);
		ChordRoot.Color = WithAlpha(t.Accent, ChordRootAlpha * t.AccentWash);
		for (var i = 0; i < TrackHueCount; i++)
		{
			TrackHues[i].Color = t.TrackHues[i];
			TrackHuesSoft[i].Color = WithAlpha(t.TrackHues[i], TrackHueSoftAlpha);
		}
	}

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

	/// <summary>The hue of the track at <paramref name="index"/> in its song: its lane's stripe, its notes, its lit keys.</summary>
	public static SolidColorBrush TrackHue(int index) => TrackHues[Wrap(index)];

	/// <summary>The same hue, faint: behind the pass of a track's part that is playing.</summary>
	public static SolidColorBrush TrackHueSoft(int index) => TrackHuesSoft[Wrap(index)];

	private static int Wrap(int index) => ((index % TrackHueCount) + TrackHueCount) % TrackHueCount;

	private static HdrColor WithAlpha(HdrColor colour, float alpha) => colour with { A = alpha / 255f };

	private static SolidColorBrush[] Brushes(int count)
	{
		var brushes = new SolidColorBrush[count];
		for (var i = 0; i < count; i++)
			brushes[i] = new SolidColorBrush();
		return brushes;
	}

	// A face lit from above: two stops, top and bottom, that a theme colours
	private static LinearGradientBrush Vertical()
	{
		var brush = new LinearGradientBrush { StartPoint = new Point(0.5f, 0f), EndPoint = new Point(0.5f, 1f) };
		brush.GradientStops.Add(new GradientStop { Offset = 0f });
		brush.GradientStops.Add(new GradientStop { Offset = 1f });
		return brush;
	}
}
