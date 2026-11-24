using System.ComponentModel;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// A part as its lane shows it across its section: one strip, every pass a stretch of it under the ruler's bars, a line
/// between each and the next, its notes in its
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

	// How far a pass's notes keep in from the strip's top and bottom, and from a line between passes
	private const float NoteInset = 3f, NoteGap = 1f;

	/// <summary>
	/// Where a point of the section, as a share of it from 0 to 1, is drawn across a strip <paramref name="width"/> wide:
	/// where the section's ruler draws it, less the strip's inset from the section's sides (<see cref="LaneCell.Inset"/>),
	/// and inside the strip. So a pass's bars stand under the ruler's bars, and every lane's playhead under the ruler's.
	/// </summary>
	public static float Across(float share, float width) =>
		Math.Clamp(share * (width + 2f * LaneCell.Inset) - LaneCell.Inset, 0f, width);

	/// <summary>The share of the section at <paramref name="x"/> across a strip <paramref name="width"/> wide (<see cref="Across"/>).</summary>
	public static float ShareAt(float x, float width) => (x + LaneCell.Inset) / (width + 2f * LaneCell.Inset);

	// The strip: one box, rounded at its two ends, each pass and the fill a stretch of it with a line between them
	private void Draw()
	{
		_canvas.Children.Clear();
		_passes.Clear();
		_canvas.Width = DrawWidth;
		if (Part is not { LengthTicks: > 0, LoopTicks: > 0 } part || DrawWidth <= 0f)
			return;
		_length = part.LengthTicks;
		var hue = Theme.TrackHue(part.TrackIndex);
		var end = part.FillTick >= 0 ? part.FillTick : part.LengthTicks;
		var boxes = _boxes;
		boxes.Clear();
		for (var start = 0; start < end; start += part.LoopTicks)
			boxes.Add((start, Math.Min(start + part.LoopTicks, end)));
		if (part.FillTick >= 0)
			boxes.Add((part.FillTick, part.LengthTicks));
		for (var i = 0; i < boxes.Count; i++)
		{
			var (from, to) = boxes[i];
			var (left, right) = (X(from), X(to));
			var fill = part.FillTick >= 0 && i == boxes.Count - 1;
			var stretch = new Border
			{
				Width = MathF.Max(0f, right - left),
				Height = DrawnHeight,
				CornerRadius = new CornerRadius(i == 0 ? 3f : 0f, i == boxes.Count - 1 ? 3f : 0f, i == boxes.Count - 1 ? 3f : 0f, i == 0 ? 3f : 0f),
				Background = fill ? Theme.Raised : Theme.Sunk,
			};
			Canvas.SetLeft(stretch, left);
			_canvas.Children.Add(stretch);
			if (!fill)
				_passes.Add(stretch);
		}
		var rowHeight = (DrawnHeight - 2f * NoteInset) / Math.Max(1, part.Rows);
		foreach (var note in part.Notes)
		{
			// Where the ruler has it, clear of the line before it and inside the stretch it plays in
			var tick = Math.Clamp(note.Tick, 0, part.LengthTicks - 1);
			if (BoxAt(tick) is not { } box)
				continue;
			var left = X(tick) + (tick == box.From ? NoteGap + 1f : 0f);
			var right = X(box.To) - NoteGap;
			var bar = new Border
			{
				Width = MathF.Max(1.5f, MathF.Min(X(tick + note.Length) - X(tick) - 0.5f, right - left)),
				Height = MathF.Max(1.5f, rowHeight - 1f),
				Background = hue,
				Opacity = 0.35f + 0.65f * note.Velocity,
				CornerRadius = new CornerRadius(1f),
			};
			Canvas.SetLeft(bar, left);
			Canvas.SetTop(bar, NoteInset + note.Row * rowHeight);
			_canvas.Children.Add(bar);
		}
		// A line between each pass and the next, and the strip's edge over all of it
		for (var i = 1; i < boxes.Count; i++)
		{
			var line = new Border { Width = 1f, Height = DrawnHeight, Background = Theme.Line };
			Canvas.SetLeft(line, MathF.Round(X(boxes[i].From)) - 0.5f);
			_canvas.Children.Add(line);
		}
		_canvas.Children.Add(new Border
		{
			Width = DrawWidth,
			Height = DrawnHeight,
			CornerRadius = new CornerRadius(3f),
			BorderBrush = Theme.Line,
			BorderThickness = new Thickness(1f),
		});
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

	// The passes' spans and the fill's, in ticks, as last drawn; the part's length
	private readonly List<(int From, int To)> _boxes = [];
	private int _length;

	private (int From, int To)? BoxAt(int tick)
	{
		for (var i = _boxes.Count - 1; i >= 0; i--)
			if (_boxes[i].From <= tick)
				return _boxes[i];
		return null;
	}

	// Where a tick is drawn: where the ruler has it (Across)
	private float X(int tick) => Across(_length > 0 ? (float)tick / _length : 0f, DrawWidth);
}
