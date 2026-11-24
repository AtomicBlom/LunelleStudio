using BinaryVibrance.Vysara;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// One look for the workbench: a colour for every role <see cref="Theme"/> paints with. <see cref="Theme.Apply"/> puts a
/// definition's colours into <see cref="Theme"/>'s brushes in place, so every view showing them changes at once. The
/// accent's faint uses (a lit column, a chord's tones, a step's hit) are drawn from <see cref="Accent"/> by
/// <see cref="Theme"/>, so a definition gives only the accent.
/// </summary>
internal sealed record ThemeDefinition
{
	/// <summary>The name the command palette and the settings know it by.</summary>
	public required string Name { get; init; }

	/// <summary>Behind everything.</summary>
	public required HdrColor Page { get; init; }

	/// <summary>A panel's flat colour, for what sits on one and must match it (a track's head, a section's).</summary>
	public required HdrColor Panel { get; init; }

	/// <summary>A panel's face, top to bottom: the same twice for a flat panel.</summary>
	public required (HdrColor Top, HdrColor Bottom) PanelFace { get; init; }

	public required HdrColor Raised { get; init; }
	public required HdrColor RaisedOver { get; init; }
	public required HdrColor Sunk { get; init; }
	public required HdrColor Rail { get; init; }

	/// <summary>A button's face, top to bottom (a key moulded and lit from above, or the same twice for a flat one), and
	/// under the pointer.</summary>
	public required (HdrColor Top, HdrColor Bottom) KeyFace { get; init; }
	public required (HdrColor Top, HdrColor Bottom) KeyFaceOver { get; init; }

	/// <summary>A toggle that is on: in a theme that glows, a push-button lit from inside, brighter than white.</summary>
	public required HdrColor KeyLit { get; init; }

	/// <summary>Behind a readout (the transport's position): a display's dark glass, or clear.</summary>
	public required HdrColor Window { get; init; }

	/// <summary>A readout's figures in their <see cref="Window"/>.</summary>
	public required HdrColor WindowInk { get; init; }

	/// <summary>How many times its light a track's hue has where it marks the track's head: past white, a lamp that
	/// glows in the track's colour; 1 for the hue as it is.</summary>
	public float TrackLampLight { get; init; } = 1f;

	public required HdrColor Ink { get; init; }
	public required HdrColor Dim { get; init; }
	public required HdrColor Faint { get; init; }
	public required HdrColor Line { get; init; }
	public required HdrColor LineStrong { get; init; }

	public required HdrColor Accent { get; init; }
	public required HdrColor AccentOver { get; init; }
	public required HdrColor AccentPressed { get; init; }
	public required HdrColor AccentInk { get; init; }

	/// <summary>
	/// How strongly the accent's faint uses (a lit column, a chord's rows in the roll, a step's hit) show it, as a share of
	/// how they show Slate's: a warm accent reads stronger on a dark page than a cool one, so it is laid on thinner.
	/// </summary>
	public float AccentWash { get; init; } = 1f;
	public required HdrColor Off { get; init; }

	public required HdrColor Meter { get; init; }

	/// <summary>A part's lamp while it plays: in a theme that glows, brighter than white (<see cref="Lit"/>).</summary>
	public required HdrColor Lamp { get; init; }

	/// <summary>A level shown as it moves (a track's meter, the scope's traces): in a theme that glows, brighter than white.</summary>
	public required HdrColor Level { get; init; }

	/// <summary>How much of the glow round what is drawn brighter than white is added to the window: 0 for none.</summary>
	public float Glow { get; init; }

	/// <summary>The font of the workbench's words (<see cref="Theme.Label"/>), by the name the app bakes it under, or
	/// null for Vysara's own.</summary>
	public string? LabelFont { get; init; }

	/// <summary>The font of its changing numbers (<see cref="Theme.Readout"/>), or null for Vysara's own.</summary>
	public string? ReadoutFont { get; init; }
	public required HdrColor Error { get; init; }
	public required HdrColor ErrorSoft { get; init; }
	public required HdrColor Backdrop { get; init; }

	public required HdrColor Note { get; init; }
	public required HdrColor BarLine { get; init; }
	public required HdrColor Playhead { get; init; }
	public required HdrColor WhiteKey { get; init; }
	public required HdrColor BlackKey { get; init; }

	/// <summary>Driving edges in the patch designer's graph.</summary>
	public required HdrColor Drive { get; init; }

	// The operator kinds' colours in the patch designer's graph
	public required HdrColor KindSine { get; init; }
	public required HdrColor KindWave { get; init; }
	public required HdrColor KindNoise { get; init; }
	public required HdrColor KindString { get; init; }
	public required HdrColor KindModes { get; init; }
	public required HdrColor KindTube { get; init; }
	public required HdrColor KindLevel { get; init; }
	public required HdrColor KindSample { get; init; }

	/// <summary>One hue per track, in the order a song makes its tracks: exactly <see cref="Theme.TrackHueCount"/>.</summary>
	public required HdrColor[] TrackHues { get; init; }

	/// <summary>The look the workbench was made with: a dark slate page, light ink, a cool blue accent.</summary>
	public static ThemeDefinition Slate { get; } = new()
	{
		Name = "Slate",
		Page = Rgb(18, 20, 26),
		Panel = Rgb(28, 31, 40),
		PanelFace = (Rgb(28, 31, 40), Rgb(28, 31, 40)),
		Raised = Rgb(37, 41, 54),
		RaisedOver = Rgb(46, 51, 66),
		Sunk = Rgb(22, 24, 31),
		Rail = Rgb(70, 74, 86),
		KeyFace = (Rgb(37, 41, 54), Rgb(37, 41, 54)),
		KeyFaceOver = (Rgb(46, 51, 66), Rgb(46, 51, 66)),
		KeyLit = Rgb(92, 170, 255),
		Window = Rgb(0, 0, 0, 0),
		WindowInk = Rgb(226, 230, 238),
		Ink = Rgb(226, 230, 238),
		Dim = Rgb(140, 148, 164),
		Faint = Rgb(93, 100, 118),
		Line = Rgb(226, 230, 238, 30),
		LineStrong = Rgb(226, 230, 238, 46),
		Accent = Rgb(92, 170, 255),
		AccentOver = Rgb(124, 188, 255),
		AccentPressed = Rgb(156, 204, 255),
		AccentInk = Rgb(11, 22, 38),
		Off = Rgb(60, 64, 78),
		Meter = Rgb(96, 214, 128),
		Lamp = Rgb(96, 214, 128),
		Level = Rgb(96, 214, 128),
		Error = Rgb(150, 40, 44),
		ErrorSoft = Rgb(224, 88, 93, 46),
		Backdrop = Rgb(0, 0, 0, 120),
		Note = Rgb(120, 180, 255),
		BarLine = Rgb(70, 76, 92),
		Playhead = Rgb(95, 240, 230, 230),
		WhiteKey = Rgb(232, 232, 236),
		BlackKey = Rgb(30, 30, 36),
		Drive = Rgb(79, 209, 197),
		KindSine = Rgb(120, 180, 255),
		KindWave = Rgb(183, 155, 255),
		KindNoise = Rgb(163, 171, 187),
		KindString = Rgb(240, 163, 94),
		KindModes = Rgb(79, 209, 197),
		KindTube = Rgb(132, 217, 126),
		KindLevel = Rgb(207, 212, 222),
		KindSample = Rgb(255, 143, 177),
		TrackHues =
		[
			Rgb(255, 138, 122), Rgb(79, 209, 197), Rgb(181, 156, 255), Rgb(127, 211, 255),
			Rgb(132, 217, 126), Rgb(242, 138, 208), Rgb(240, 176, 104), Rgb(158, 170, 255),
		],
	};

	/// <summary>
	/// A mixing desk late at night: warm charcoal panels shaded from the light above, dark seams between them, cream ink,
	/// an orange lamp for what is on and green for levels.
	/// </summary>
	public static ThemeDefinition Console { get; } = new()
	{
		Name = "Console",
		Page = Rgb(25, 24, 22),
		Panel = Rgb(39, 37, 34),
		PanelFace = (Rgb(45, 43, 40), Rgb(35, 33, 31)),
		Raised = Rgb(58, 55, 51),
		RaisedOver = Rgb(70, 66, 61),
		Sunk = Rgb(29, 28, 26),
		Rail = Rgb(22, 21, 18),
		KeyFace = (Rgb(66, 62, 57), Rgb(46, 43, 40)),
		KeyFaceOver = (Rgb(78, 74, 68), Rgb(56, 53, 49)),
		KeyLit = Lit(Rgb(255, 138, 61), 1.25f),
		Window = Rgb(16, 20, 14),
		WindowInk = Lit(Rgb(201, 240, 178), 1.2f),
		TrackLampLight = 2.2f,
		Ink = Rgb(236, 230, 216),
		Dim = Rgb(163, 155, 138),
		Faint = Rgb(112, 105, 92),
		Line = Rgb(8, 7, 6, 150),
		LineStrong = Rgb(0, 0, 0, 190),
		Accent = Rgb(255, 138, 61),
		AccentOver = Rgb(255, 160, 96),
		AccentPressed = Rgb(255, 182, 131),
		AccentInk = Rgb(27, 18, 6),
		AccentWash = 0.5f,
		Off = Rgb(62, 58, 52),
		Meter = Rgb(127, 220, 106),
		Lamp = Lit(Rgb(127, 220, 106), 3f),
		Level = Lit(Rgb(127, 220, 106), 1.6f),
		Glow = 0.35f,
		LabelFont = "archivo-narrow",
		ReadoutFont = "b612-mono",
		Error = Rgb(196, 64, 46),
		ErrorSoft = Rgb(227, 93, 74, 46),
		Backdrop = Rgb(0, 0, 0, 140),
		Note = Rgb(255, 176, 112),
		BarLine = Rgb(74, 70, 64),
		Playhead = Lit(Rgb(255, 138, 61), 2.5f),
		WhiteKey = Rgb(233, 227, 214),
		BlackKey = Rgb(20, 19, 18),
		Drive = Rgb(63, 201, 182),
		KindSine = Rgb(110, 176, 240),
		KindWave = Rgb(170, 145, 240),
		KindNoise = Rgb(170, 164, 150),
		KindString = Rgb(240, 163, 94),
		KindModes = Rgb(63, 201, 182),
		KindTube = Rgb(143, 214, 94),
		KindLevel = Rgb(214, 207, 192),
		KindSample = Rgb(242, 126, 168),
		TrackHues =
		[
			Rgb(255, 122, 92), Rgb(56, 201, 182), Rgb(164, 140, 255), Rgb(79, 176, 240),
			Rgb(143, 214, 94), Rgb(242, 126, 192), Rgb(240, 176, 74), Rgb(154, 166, 255),
		],
	};

	/// <summary>Every theme, the first the one a workbench starts in.</summary>
	public static IReadOnlyList<ThemeDefinition> All { get; } = [Slate, Console];

	/// <summary>The theme named <paramref name="name"/> (any case), or null.</summary>
	public static ThemeDefinition? Find(string? name)
	{
		foreach (var theme in All)
			if (string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase))
				return theme;
		return null;
	}

	private static HdrColor Rgb(byte r, byte g, byte b, byte a = 255) => HdrColor.FromRgb(r, g, b, a);

	/// <summary>
	/// <paramref name="colour"/> giving <paramref name="times"/> its light: authored colours are sRGB, which Vysara decodes
	/// to linear light along a curve that runs on past 1, so this scales the linear light and encodes it back. Past white,
	/// it glows in a theme with <see cref="Glow"/>.
	/// </summary>
	public static HdrColor Lit(HdrColor colour, float times) =>
		colour with { R = Encode(Decode(colour.R) * times), G = Encode(Decode(colour.G) * times), B = Encode(Decode(colour.B) * times) };

	// The sRGB curve each way, as Vysara's HdrColor decodes it, continued past 1
	private static float Decode(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

	private static float Encode(float l) => l <= 0.0031308f ? l * 12.92f : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;
}
