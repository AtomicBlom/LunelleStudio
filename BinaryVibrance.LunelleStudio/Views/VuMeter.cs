using System.ComponentModel;
using System.Globalization;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Media;
using BinaryVibrance.Vysara.Shapes;
using Path = BinaryVibrance.Vysara.Shapes.Path;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// The master's level on a VU meter by the transport, in a theme that has one (<see cref="WorkbenchViewModel.HasVuMeter"/>):
/// a face lit from behind, a scale from −20 to +3 VU with the red past 0, and a needle that swings from its pivot under
/// the face as <see cref="WorkbenchViewModel.MasterLevel"/> moves. Its colours are the theme's.
/// </summary>
internal sealed class VuMeter : ContentControl
{
	// The pivot is under the face, as a VU meter's is, so the scale is a wide arc and only the needle's far part shows
	private const float FaceWidth = 100f, FaceHeight = 40f;
	private const float PivotX = FaceWidth / 2f, PivotY = FaceHeight + 16f;
	private const float ScaleRadius = 40f, NeedleFrom = 22f, NeedleTo = 46f, Swing = 45f;
	private const float Floor = -20f, Ceiling = 3f;
	private static readonly float[] Ticks = [-20f, -10f, -7f, -5f, -3f, -2f, -1f, 0f, 1f, 2f, 3f];

	private readonly RotateTransform _swing = new() { CenterX = PivotX, CenterY = PivotY, Angle = -Swing };

	public VuMeter()
	{
		IsTabStop = false;
		IsHitTestVisible = false;
		ToolTipService.SetToolTip(this, "The master's level");

		var dial = new Canvas { Width = FaceWidth, Height = FaceHeight };
		dial.Children.Add(Arc(Floor, Ceiling, Theme.VuInk, 1.2f));
		dial.Children.Add(Arc(0f, Ceiling, Theme.VuHot, 3f));
		foreach (var tick in Ticks)
		{
			var (x0, y0) = At(tick, ScaleRadius);
			var (x1, y1) = At(tick, ScaleRadius + (tick % 5f == 0f || tick == 3f ? 6f : 4f));
			dial.Children.Add(new Line { X1 = x0, Y1 = y0, X2 = x1, Y2 = y1, Stroke = tick > 0f ? Theme.VuHot : Theme.VuInk, StrokeThickness = 1.2f });
		}
		var name = new TextBlock { Text = "VU", FontSize = 9f, Foreground = Theme.VuInk };
		Canvas.SetLeft(name, PivotX - 6f);
		Canvas.SetTop(name, 25f);
		dial.Children.Add(name);
		dial.Children.Add(new Line
		{
			X1 = PivotX, Y1 = PivotY - NeedleFrom, X2 = PivotX, Y2 = PivotY - NeedleTo,
			Stroke = Theme.VuNeedle, StrokeThickness = 1.4f, RenderTransform = _swing,
		});

		Content = new Border
		{
			Width = FaceWidth,
			Height = FaceHeight,
			CornerRadius = new CornerRadius(3f),
			Background = Theme.VuFace,
			BorderBrush = Theme.LineStrong,
			BorderThickness = new Thickness(1f),
			Child = dial,
		};
	}

	/// <summary>What it shows the level of.</summary>
	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field is not null)
				field.PropertyChanged -= OnChanged;
			field = value;
			if (value is not null)
				value.PropertyChanged += OnChanged;
			Show();
		}
	}

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(WorkbenchViewModel.MasterLevel) or nameof(WorkbenchViewModel.HasVuMeter))
			Show();
	}

	private void Show()
	{
		var model = ViewModel;
		Visibility = model is { HasVuMeter: true } ? Visibility.Visible : Visibility.Collapsed;
		_swing.Angle = -Swing + 2f * Swing * (model?.MasterLevel ?? 0f);
	}

	// A point on the scale at `vu`, `radius` from the pivot
	private static (float X, float Y) At(float vu, float radius)
	{
		var angle = (-Swing + 2f * Swing * (vu - Floor) / (Ceiling - Floor)) * MathF.PI / 180f;
		return (PivotX + radius * MathF.Sin(angle), PivotY - radius * MathF.Cos(angle));
	}

	// The scale from one VU to another, as an arc round the pivot
	private static Path Arc(float from, float to, Brush brush, float thickness)
	{
		var (x0, y0) = At(from, ScaleRadius);
		var (x1, y1) = At(to, ScaleRadius);
		var data = string.Create(CultureInfo.InvariantCulture, $"M {x0:0.##},{y0:0.##} A {ScaleRadius},{ScaleRadius} 0 0 1 {x1:0.##},{y1:0.##}");
		return new Path { Data = Geometry.Parse(data), Stroke = brush, StrokeThickness = thickness };
	}
}
