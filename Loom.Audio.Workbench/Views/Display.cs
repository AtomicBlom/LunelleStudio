using System.Globalization;

using Loom.Audio.Synth;

using SUIsei.Media;

namespace Loom.Audio.Workbench.Views;

/// <summary>How the views show a view model's values, for their function bindings (<c>{x:Bind views:Display.Fixed(Value)}</c>).</summary>
internal static class Display
{
	/// <summary>How wide a track's meter is: <see cref="MeterWidth"/> across 48 dB, silence at the left.</summary>
	public const float MeterWidth = 80f;

	/// <summary>A level to two places: "0.35".</summary>
	public static string Fixed(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

	/// <summary>How far a meter's bar reaches for a linear <paramref name="peak"/>.</summary>
	public static float Meter(float peak) => MeterWidth * MathF.Max(0f, 1f + AudioAnalysis.ToDb(MathF.Max(peak, 1e-6f)) / 48f);

	/// <summary>A light's colour: the accent while on.</summary>
	public static SolidColorBrush Lit(bool on) => on ? Theme.Accent : Theme.Off;

	/// <summary>The transport's button: what it does next.</summary>
	public static string PlayLabel(bool playing) => playing ? "Stop" : "Play";

	/// <summary>The keyboard's octave, and the keys that move it.</summary>
	public static string Octave(int octave) => $"octave {octave} (Z and X move it)";

	/// <summary>A kit's button.</summary>
	public static string Kit(string name) => $"kit: {name}";

	/// <summary>Whether an index chooses something (not −1).</summary>
	public static bool Chosen(int index) => index >= 0;

	/// <summary>Whether there is text to show.</summary>
	public static bool Any(string text) => text.Length > 0;
}
