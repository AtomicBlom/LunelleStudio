using System.ComponentModel;
using SUIsei;
using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// A part as its lane shows it across its section: every pass side by side under the ruler's bars, its notes in its
/// track's hue (each pass as it sounds over the chords under it), the fill in the last bar marked, the pass playing lit in
/// the hue and those still to come dimmed, a playhead in the pass, and the whole faded while its condition doesn't hold.
/// As the song moves only the lit pass and the playhead move; nothing is made.
/// </summary>
internal sealed class LanePasses : ContentControl
{
	public const float DrawnHeight = 30f;

	private readonly Canvas _canvas = new() { Height = DrawnHeight };
	private readonly List<Border> _passes = [];
	private readonly Border _playhead = new() { Width = 2f, Height = DrawnHeight, Background = Theme.Playhead, IsHitTestVisible = false };
	private float _scale;

	public LanePasses()
	{
		IsTabStop = false;
		IsHitTestVisible = false;
		Content = _canvas;
	}

	/// <summary>The part whose passes it draws.</summary>
	public PlacementViewModel? Part
	{
		get;
		set
		{
			if (field is not null)
				field.PropertyChanged -= OnPartChanged;
			field = value;
			if (value is not null)
				value.PropertyChanged += OnPartChanged;
			Draw();
		}
	}

	/// <summary>How wide it draws the section.</summary>
	public float DrawWidth
	{
		get;
		set
		{
			field = value;
			Draw();
		}
	}

	private void OnPartChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(PlacementViewModel.PassPlaying) or nameof(PlacementViewModel.SectionTick) or nameof(PlacementViewModel.ConditionMet))
			ShowPosition();
	}

	private void Draw()
	{
		_canvas.Children.Clear();
		_passes.Clear();
		_canvas.Width = DrawWidth;
		if (Part is not { LengthTicks: > 0, LoopTicks: > 0 } part || DrawWidth <= 0f)
			return;
		_scale = DrawWidth / part.LengthTicks;
		var hue = Theme.TrackHue(part.TrackIndex);
		var end = part.FillTick >= 0 ? part.FillTick : part.LengthTicks;
		for (var start = 0; start < end; start += part.LoopTicks)
		{
			var pass = new Border
			{
				Width = MathF.Max(1f, MathF.Min(part.LoopTicks, end - start) * _scale - 2f),
				Height = DrawnHeight,
				CornerRadius = new CornerRadius(3f),
				Background = Theme.Sunk,
				BorderBrush = Theme.Line,
				BorderThickness = new Thickness(1f),
			};
			Canvas.SetLeft(pass, start * _scale + 1f);
			_passes.Add(pass);
			_canvas.Children.Add(pass);
		}
		if (part.FillTick >= 0)
		{
			var fill = new Border
			{
				Width = MathF.Max(1f, (part.LengthTicks - part.FillTick) * _scale - 2f),
				Height = DrawnHeight,
				CornerRadius = new CornerRadius(3f),
				Background = Theme.Raised,
				BorderBrush = Theme.LineStrong,
				BorderThickness = new Thickness(1f),
			};
			Canvas.SetLeft(fill, part.FillTick * _scale + 1f);
			_canvas.Children.Add(fill);
		}
		var rowHeight = (DrawnHeight - 6f) / Math.Max(1, part.Rows);
		foreach (var note in part.Notes)
		{
			var bar = new Border
			{
				Width = MathF.Max(1.5f, note.Length * _scale - 0.5f),
				Height = MathF.Max(1.5f, rowHeight - 1f),
				Background = hue,
				Opacity = 0.35f + 0.65f * note.Velocity,
				CornerRadius = new CornerRadius(1f),
			};
			Canvas.SetLeft(bar, note.Tick * _scale);
			Canvas.SetTop(bar, 3f + note.Row * rowHeight);
			_canvas.Children.Add(bar);
		}
		_canvas.Children.Add(_playhead);
		ShowPosition();
	}

	// The pass playing lit, those to come dimmed, the playhead where the song is; all faded while the condition doesn't hold
	private void ShowPosition()
	{
		if (Part is not { } part)
			return;
		Opacity = part.ConditionMet ? 1f : 0.4f;
		var playing = part.PassPlaying;
		for (var i = 0; i < _passes.Count; i++)
		{
			_passes[i].Background = i == playing ? Theme.TrackHueSoft(part.TrackIndex) : Theme.Sunk;
			_passes[i].Opacity = playing >= 0 && i > playing ? 0.55f : 1f;
		}
		var tick = part.SectionTick;
		_playhead.Visibility = tick >= 0 ? Visibility.Visible : Visibility.Collapsed;
		if (tick >= 0)
			Canvas.SetLeft(_playhead, tick * _scale);
	}
}
