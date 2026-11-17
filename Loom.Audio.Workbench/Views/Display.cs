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

	public static bool NoneChosen(int index) => index < 0;

	/// <summary>Whether there is text to show.</summary>
	public static bool Any(string text) => text.Length > 0;

	public static bool Not(bool value) => !value;

	// A part's condition, green while it holds
	public static SolidColorBrush Met(bool met) => met ? Theme.Meter : Theme.Faint;

	// A clip's edge, blue while it is the part chosen
	public static SolidColorBrush Chosen(bool chosen) => chosen ? Theme.Note : Theme.Line;

	// A section on the timeline, outlined while it plays
	public static SolidColorBrush Playing(bool playing) => playing ? Theme.Accent : Theme.Line;

	// A column of the lanes, lit faintly while its section plays
	public static SolidColorBrush Column(bool current) => current ? Theme.AccentSoft : Theme.Clear;

	// The build's pill: red while the last build failed
	public static SolidColorBrush Pill(bool error) => error ? Theme.ErrorSoft : Theme.Raised;

	public static SolidColorBrush PillInk(bool error) => error ? Theme.Error : Theme.Dim;

	// A palette item, lit while Enter would run it
	public static SolidColorBrush Highlight(bool selected) => selected ? Theme.AccentSoft : Theme.Clear;

	// A cue the song never reads, dimmed
	public static float Used(bool used) => used ? 1f : 0.45f;

	// Where the playhead crosses a column, as a left margin
	public static SUIsei.Thickness Playhead(float x) => new(x, 0f, 0f, 0f);

	public static string Pin(bool pinned) => pinned ? "Unpin" : "Pin";

	public static string Percent(float value) => $"{value * 100f:0}%";

	// An operator's colour by its kind
	public static SolidColorBrush Kind(Loom.Audio.Synth.Chip.OperatorKind kind) => kind switch
	{
		Loom.Audio.Synth.Chip.OperatorKind.Sine => Theme.KindSine,
		Loom.Audio.Synth.Chip.OperatorKind.Wave => Theme.KindWave,
		Loom.Audio.Synth.Chip.OperatorKind.Noise => Theme.KindNoise,
		Loom.Audio.Synth.Chip.OperatorKind.String => Theme.KindString,
		Loom.Audio.Synth.Chip.OperatorKind.Modes => Theme.KindModes,
		Loom.Audio.Synth.Chip.OperatorKind.Tube => Theme.KindTube,
		Loom.Audio.Synth.Chip.OperatorKind.Sample => Theme.KindSample,
		_ => Theme.KindLevel,
	};

	// A stage of the voice: dim when it does nothing
	public static SolidColorBrush StageInk(bool off) => off ? Theme.Faint : Theme.Ink;

	// The designer's problems, red
	public static SolidColorBrush Problem(bool problems) => problems ? Theme.Error : Theme.Meter;

	// A beat sketcher's step: dark when off, the accent's strength for a hit, an accent, a ghost
	public static SolidColorBrush Step(char value) => value switch
	{
		'X' => Theme.Accent,
		'x' => Theme.StepHit,
		'o' => Theme.StepGhost,
		_ => Theme.Sunk,
	};

	// The step playing, outlined
	public static SolidColorBrush Now(bool now) => now ? Theme.Ink : Theme.Line;

	// A beat's first step, set a little apart
	public static SUIsei.Thickness StepGap(bool beat) => new(beat ? 6f : 1f, 0f, 1f, 0f);
}
