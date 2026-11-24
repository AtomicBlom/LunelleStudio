using BinaryVibrance.Sirrow;

using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Shapes;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// What the workbench's bus is heard playing, every frame while the patch designer shows: a scope (held still on a rising zero crossing) and a
/// spectrum from 20 Hz to 20 kHz on a log scale, 100 dB tall.
/// </summary>
internal sealed class ScopeView : ContentControl
{
	private const float ScopeWidth = 240f;
	private const float SpectrumWidth = 380f;
	private const float DrawnHeight = 110f;
	private const int Samples = 4096;
	private const int ScopePoints = 240;
	private const int SpectrumPoints = 190;

	private readonly float[] _heard = new float[Samples];
	// The spectrum's working space and its bins, kept, so drawing it every frame allocates nothing
	private readonly double[] _real = new double[Samples];
	private readonly double[] _imaginary = new double[Samples];
	private readonly float[] _bins = new float[Samples / 2 + 1];
	private readonly Point[] _scopePoints = new Point[ScopePoints];
	private readonly Point[] _spectrumPoints = new Point[SpectrumPoints];
	private readonly Polyline _scope = new() { Stroke = Theme.Level, StrokeThickness = 1.3f };
	private readonly Polyline _spectrum = new() { Stroke = Theme.Level, StrokeThickness = 1f };

	public ScopeView()
	{
		IsTabStop = false;
		IsHitTestVisible = false;
		var row = new StackPanel { Orientation = Orientation.Horizontal };
		row.Children.Add(Panel(_scope, ScopeWidth, "scope", []));
		row.Children.Add(Panel(_spectrum, SpectrumWidth, "spectrum", [100f, 1000f, 10000f]));
		Content = row;
	}

	/// <summary>The workbench whose bus it draws, every frame while the patch designer shows.</summary>
	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field is { } old)
				old.Frame -= Draw;
			field = value;
			if (value is not null)
				value.Frame += Draw;
		}
	}

	private static Border Panel(Polyline line, float width, string title, float[] marks)
	{
		var canvas = new Canvas { Width = width, Height = DrawnHeight };
		canvas.Children.Add(new Line { X1 = 0f, Y1 = DrawnHeight / 2f, X2 = width, Y2 = DrawnHeight / 2f, Stroke = Theme.Line, StrokeThickness = 1f });
		foreach (var hz in marks)
		{
			var x = SpectrumX(hz, width);
			canvas.Children.Add(new Line { X1 = x, Y1 = 0f, X2 = x, Y2 = DrawnHeight, Stroke = Theme.Line, StrokeThickness = 1f });
			var label = new TextBlock { Text = hz >= 1000f ? $"{hz / 1000f:0}k" : $"{hz:0}", FontSize = 9f, Foreground = Theme.Faint };
			Canvas.SetLeft(label, x + 3f);
			Canvas.SetTop(label, DrawnHeight - 14f);
			canvas.Children.Add(label);
		}
		var name = new TextBlock { Text = title, FontSize = 9f, Foreground = Theme.Faint };
		Canvas.SetLeft(name, 6f);
		Canvas.SetTop(name, 3f);
		canvas.Children.Add(name);
		canvas.Children.Add(line);
		var panel = new Border { Child = canvas, Background = Theme.Sunk, CornerRadius = new CornerRadius(6), Margin = new Thickness(0f, 0f, 8f, 0f) };
		BinaryVibrance.Vysara.Extensions.Clipping.SetClipToBounds(panel, true);
		return panel;
	}

	private static float SpectrumX(float hz, float width) => MathF.Log(hz / 20f) / MathF.Log(1000f) * width;

	private void Draw()
	{
		// Only while it can be seen: the designer it sits in is hidden in the other modes, not the scope itself
		if (ViewModel is not { IsPatch: true } model || Visibility != Visibility.Visible)
			return;
		var count = model.ReadScope(_heard);
		if (count < Samples)
			return;
		// The scope from the first rising zero crossing in the first half, so a steady note holds still
		var start = 0;
		for (var i = 1; i < Samples / 2; i++)
			if (_heard[i - 1] < 0f && _heard[i] >= 0f)
			{
				start = i;
				break;
			}
		var span = Math.Min(1024, Samples - start);
		for (var p = 0; p < ScopePoints; p++)
		{
			var sample = _heard[start + p * span / ScopePoints];
			_scopePoints[p] = new Point(p * ScopeWidth / (ScopePoints - 1), DrawnHeight / 2f - Math.Clamp(sample, -1f, 1f) * DrawnHeight * 0.45f);
		}
		_scope.Points.SetAll(_scopePoints);

		var bins = _bins;
		AudioAnalysis.Spectrum(_heard, _real, _imaginary, bins);
		var nyquist = model.SampleRate / 2f;
		for (var p = 0; p < SpectrumPoints; p++)
		{
			var x = p * SpectrumWidth / (SpectrumPoints - 1);
			var hz = 20f * MathF.Pow(1000f, x / SpectrumWidth);
			var bin = Math.Clamp((int)MathF.Round(hz / nyquist * (bins.Length - 1)), 0, bins.Length - 1);
			var db = MathF.Max(-100f, AudioAnalysis.ToDb(MathF.Max(bins[bin], 1e-6f)));
			_spectrumPoints[p] = new Point(x, DrawnHeight * (-db / 100f));
		}
		_spectrum.Points.SetAll(_spectrumPoints);
	}
}
