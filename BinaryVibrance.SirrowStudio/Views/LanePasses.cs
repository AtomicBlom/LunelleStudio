using System.ComponentModel;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.SirrowStudio.Views;

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

	// How far a pass's notes keep in from its sides: its border and the curve of its corners
	private const float NoteInset = 3f;

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
		// Each pass's box (and the fill's) by the ticks it spans, for its notes to be laid inside
		var boxes = _boxes;
		boxes.Clear();
		for (var start = 0; start < end; start += part.LoopTicks)
		{
			var to = Math.Min(start + part.LoopTicks, end);
			var pass = new Border
			{
				Width = MathF.Max(1f, (to - start) * _scale - 2f),
				Height = DrawnHeight,
				CornerRadius = new CornerRadius(3f),
				Background = Theme.Sunk,
				BorderBrush = Theme.Line,
				BorderThickness = new Thickness(1f),
			};
			Canvas.SetLeft(pass, start * _scale + 1f);
			boxes.Add((start, to, start * _scale + 1f, (float)pass.Width));
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
			boxes.Add((part.FillTick, part.LengthTicks, part.FillTick * _scale + 1f, (float)fill.Width));
			_canvas.Children.Add(fill);
		}
		var rowHeight = (DrawnHeight - 6f) / Math.Max(1, part.Rows);
		foreach (var note in part.Notes)
		{
			// Inside the box of the pass it plays in, clear of its border and rounded corners
			var tick = Math.Clamp(note.Tick, 0, part.LengthTicks - 1);
			if (BoxAt(tick) is not { } box)
				continue;
			var perTick = PerTick(box);
			var left = X(tick);
			var right = box.X + box.Width - NoteInset;
			var bar = new Border
			{
				Width = MathF.Max(1.5f, MathF.Min(note.Length * perTick - 0.5f, right - left)),
				Height = MathF.Max(1.5f, rowHeight - 1f),
				Background = hue,
				Opacity = 0.35f + 0.65f * note.Velocity,
				CornerRadius = new CornerRadius(1f),
			};
			Canvas.SetLeft(bar, left);
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
			Canvas.SetLeft(_playhead, X(tick));
	}

	// The passes' boxes and the fill's as last drawn: the ticks each spans, and where it is
	private readonly List<(int From, int To, float X, float Width)> _boxes = [];

	private (int From, int To, float X, float Width)? BoxAt(int tick)
	{
		for (var i = _boxes.Count - 1; i >= 0; i--)
			if (_boxes[i].From <= tick)
				return _boxes[i];
		return null;
	}

	private static float PerTick((int From, int To, float X, float Width) box) =>
		MathF.Max(0f, box.Width - 2f * NoteInset) / Math.Max(1, box.To - box.From);

	// Where a tick is drawn: inside the box of the pass it falls in, as its notes are
	private float X(int tick) => BoxAt(tick) is { } box ? box.X + NoteInset + (tick - box.From) * PerTick(box) : tick * _scale;
}
