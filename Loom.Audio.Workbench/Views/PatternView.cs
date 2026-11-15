using System.ComponentModel;

using SUIsei;
using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>The chosen part as a grid (<c>PatternView.xaml</c>), drawn in code: rows, notes, bar lines and a playhead.</summary>
internal sealed partial class PatternView : ContentControl
{
	private const float LabelWidth = 64f;
	private const float Width_ = 1180f;
	private const float RowHeight = 12f;

	private readonly PatternViewModel _pattern;
	private Border? _playhead;

	/// <summary>A view of <paramref name="pattern"/>.</summary>
	public PatternView(PatternViewModel pattern)
	{
		_pattern = pattern;
		InitializeComponent();
		Draw();
		pattern.PropertyChanged += OnChanged;
	}

	partial void OnComponentRebuilt() => Draw();

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PatternViewModel.PlayheadTick))
			MovePlayhead();
		else if (e.PropertyName is nameof(PatternViewModel.LengthTicks) or nameof(PatternViewModel.Title))
			Draw();
	}

	private float Scale => _pattern.LengthTicks > 0 ? (Width_ - LabelWidth) / _pattern.LengthTicks : 0f;

	private void Draw()
	{
		TitleLabel.Text = _pattern.Title;
		TitleLabel.Foreground = Theme.Dim;
		Notes.Children.Clear();
		_playhead = null;
		if (_pattern.LengthTicks <= 0)
			return;
		var rows = Math.Max(1, _pattern.RowLabels.Count);
		var rowHeight = MathF.Min(RowHeight, 150f / rows);
		var scale = Scale;
		for (var bar = 0; bar * _pattern.TicksPerBar <= _pattern.LengthTicks; bar++)
			Place(new Border { Width = 1f, Height = rows * rowHeight, Background = Theme.BarLine }, LabelWidth + bar * _pattern.TicksPerBar * scale, 0f);
		for (var r = 0; r < _pattern.RowLabels.Count; r++)
			Place(new TextBlock { Text = _pattern.RowLabels[r], FontSize = MathF.Min(11f, rowHeight), Foreground = Theme.Dim }, 0f, r * rowHeight);
		foreach (var note in _pattern.Notes)
		{
			var block = new Border
			{
				Width = MathF.Max(2f, note.Length * scale - 1f),
				Height = rowHeight - 2f,
				Background = Theme.Note,
				Opacity = 0.35f + 0.65f * note.Velocity,
				CornerRadius = new CornerRadius(2),
			};
			Place(block, LabelWidth + note.Tick * scale, note.Row * rowHeight + 1f);
		}
		_playhead = new Border { Width = 2f, Height = rows * rowHeight, Background = Theme.Playhead, Visibility = Visibility.Collapsed };
		Place(_playhead, LabelWidth, 0f);
		MovePlayhead();
	}

	private void Place(UIElement element, float x, float y)
	{
		Canvas.SetLeft(element, x);
		Canvas.SetTop(element, y);
		Notes.Children.Add(element);
	}

	private void MovePlayhead()
	{
		if (_playhead is null)
			return;
		var tick = _pattern.PlayheadTick;
		_playhead.Visibility = tick >= 0 ? Visibility.Visible : Visibility.Collapsed;
		if (tick >= 0)
			Canvas.SetLeft(_playhead, LabelWidth + tick * Scale);
	}
}
