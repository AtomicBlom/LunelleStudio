using System.Numerics;
using BinaryVibrance.Kaldra.Core.Graph;
using BinaryVibrance.Kaldra.Core.Hosting;
using BinaryVibrance.Kaldra.Core.Materials;

namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// The workbench's light: it is drawn in linear light into a half-float target, and what a theme draws brighter than
/// white (a lamp that is on, a level, the playhead) glows round itself. The bright part at half the window's size,
/// halved again four times, then each smaller step spread and added back onto the larger, so the glow gathers every
/// size; the display pass adds it, scaled by the theme's glow (<see cref="Workbench.ThemeGlow"/>), and the screen's
/// sRGB format encodes the result. Nothing at or under white glows, so text and panels stay sharp, and a theme with no
/// glow shows exactly as drawn.
/// </summary>
internal sealed class Glow
{
	private const int Steps = 5;            // 1/2 to 1/32 of the window
	private const float Threshold = 1.0f;   // white
	private const float Knee = 0.1f;        // eased in only just under white, so white text never glows

	private readonly float[] _prefilter = new float[4];
	private readonly float[][] _sizes = [.. Enumerable.Range(0, Steps).Select(_ => new float[4])];
	private readonly float[] _display = new float[4];

	public Glow(IEngine engine)
	{
		engine.DefineTechnique("bloom.prefilter", new TechniqueDescriptor("bloom_prefilter", LinearFilter: true, HasFragmentPush: true));
		engine.DefineTechnique("bloom.down", new TechniqueDescriptor("bloom_down", LinearFilter: true, HasFragmentPush: true));
		engine.DefineTechnique("bloom.up", new TechniqueDescriptor("bloom_up", LinearFilter: true, HasFragmentPush: true, Blend: BlendMode.Additive));
		engine.DefineTechnique("display", new TechniqueDescriptor("display", LinearFilter: true, HasFragmentPush: true));
	}

	/// <summary>The target the workbench is drawn into, in linear light, for <see cref="Declare"/> to show.</summary>
	public static ColorHandle Target(GraphBuilder g) => g.Screen.Color("workbench", Format.Rgba16F);

	/// <summary>The glow's passes over <paramref name="workbench"/>, then the display pass onto <paramref name="screen"/>.</summary>
	public void Declare(GraphBuilder g, ColorHandle workbench, ColorHandle screen)
	{
		var chain = new ColorHandle[Steps];
		for (var i = 0; i < Steps; i++)
			chain[i] = g.Screen.Color($"glow{i}", Format.Rgba16F, scale: 1f / (1 << (i + 1)));

		g.Pass("glow.prefilter")
			.Samples(workbench)
			.Writes(chain[0])
			.Draw((this, workbench), static (ctx, s) => ctx.FullScreen("bloom.prefilter", s.Item1._prefilter, s.workbench));
		for (var i = 1; i < Steps; i++)
		{
			var (from, to) = (chain[i - 1], chain[i]);
			var size = _sizes[i - 1];
			g.Pass($"glow.down{i}")
				.Samples(from)
				.Writes(to)
				.Draw((size, from), static (ctx, s) => ctx.FullScreen("bloom.down", s.size, s.from));
		}
		for (var i = Steps - 2; i >= 0; i--)
		{
			var (smaller, larger) = (chain[i + 1], chain[i]);
			var size = _sizes[i + 1];
			g.Pass($"glow.up{i}")
				.Samples(smaller)
				.Blends(larger)
				.Draw((size, smaller), static (ctx, s) => ctx.FullScreen("bloom.up", s.size, s.smaller));
		}

		g.Pass("display")
			.Samples(workbench)
			.Samples(chain[0])
			.Writes(screen)
			.Draw((this, workbench, chain[0]), static (ctx, s) => ctx.FullScreen("display", s.Item1._display, s.workbench, s.Item3));
	}

	/// <summary>Once a frame: the window's size in pixels, for the steps' texel sizes, and how strongly the theme glows.</summary>
	public void Prepare(Vector2 surface, float strength)
	{
		(_prefilter[0], _prefilter[1], _prefilter[2], _prefilter[3]) = (Threshold, Knee, surface.X, surface.Y);
		for (var i = 0; i < Steps; i++)
		{
			var scale = 1f / (1 << (i + 1));
			(_sizes[i][0], _sizes[i][1]) = (MathF.Max(1f, MathF.Floor(surface.X * scale)), MathF.Max(1f, MathF.Floor(surface.Y * scale)));
		}
		_display[0] = strength;
	}
}
