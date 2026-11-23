using System.ComponentModel;

using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The chosen part as a grid (<c>PatternView.xaml</c>), its title bound and its notes drawn in code from
/// <see cref="Pattern"/>: rows, notes, bar lines and a playhead.
/// </summary>
internal sealed partial class PatternView : ContentControl
{
	private const float LabelWidth = 64f;
	private const float GridWidth = 1180f;
	private const float RowHeight = 12f;

	private Border? _playhead;

	/// <summary>A view with nothing to draw until it is given a <see cref="Pattern"/>.</summary>
	public PatternView() => InitializeComponent();

	/// <summary>What it draws: drawn again when it is set and when it changes.</summary>
	public PatternViewModel? Pattern
	{
		get;
		set
		{
			if (field == value)
				return;
			if (field is not null)
				field.PropertyChanged -= OnChanged;
			field = value;
			if (value is not null)
				value.PropertyChanged += OnChanged;
			Draw();
		}
	}

	partial void OnComponentRebuilt() => Draw();

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PatternViewModel.PlayheadTick))
			MovePlayhead();
		else if (e.PropertyName is nameof(PatternViewModel.LengthTicks) or nameof(PatternViewModel.Title))
			Draw();
	}

	private float Scale => Pattern is { LengthTicks: > 0 } pattern ? (GridWidth - LabelWidth) / pattern.LengthTicks : 0f;

	private void Draw()
	{
		TitleLabel.Text = Pattern?.Title ?? "";
		Notes.Children.Clear();
		_playhead = null;
		if (Pattern is not { LengthTicks: > 0 } pattern)
			return;
		var rows = Math.Max(1, pattern.RowLabels.Count);
		var rowHeight = MathF.Min(RowHeight, 150f / rows);
		var scale = Scale;
		for (var bar = 0; bar * pattern.TicksPerBar <= pattern.LengthTicks; bar++)
			Place(new Border { Width = 1f, Height = rows * rowHeight, Background = Theme.BarLine }, LabelWidth + bar * pattern.TicksPerBar * scale, 0f);
		for (var r = 0; r < pattern.RowLabels.Count; r++)
			Place(new TextBlock { Text = pattern.RowLabels[r], FontSize = MathF.Min(11f, rowHeight), Foreground = Theme.Dim }, 0f, r * rowHeight);
		foreach (var note in pattern.Notes)
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
		if (_playhead is null || Pattern is null)
			return;
		var tick = Pattern.PlayheadTick;
		_playhead.Visibility = tick >= 0 ? Visibility.Visible : Visibility.Collapsed;
		if (tick >= 0)
			Canvas.SetLeft(_playhead, LabelWidth + tick * Scale);
	}
}
