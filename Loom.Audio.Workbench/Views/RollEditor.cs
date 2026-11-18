using System.ComponentModel;

using SUIsei;
using SUIsei.Controls;
using SUIsei.Input;
using SUIsei.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The roll, drawn: a grid of steps across and rows down (a line's degrees, a beat's pieces, a strum's strokes), bar
/// lines heavier, the notes as bars and the hits as cells, fitted to the space it has: its steps widen and its rows grow
/// to fill it, and a part too long to fit at the narrowest scrolls sideways. A press, a drag and a release go to the roll's
/// view model, which makes the edit; a right-click or a second click on the same note deletes it.
/// </summary>
internal sealed class RollEditor : ContentControl
{
	private const float MinStepWidth = 18f;
	private const float MaxStepWidth = 56f;
	// Room for the scroll bars' gutters, so a roll fitted to its space needs neither.
	private const float Gutter = 16f;

	private readonly Canvas _grid = new()
	{
		Background = Theme.Sunk,
		HorizontalAlignment = HorizontalAlignment.Left,
		VerticalAlignment = VerticalAlignment.Top,
	};
	private readonly ScrollViewer _scroll = new()
	{
		HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
		VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
	};
	// The labels' width, a step's width and a row's height as last fitted, and the space they were fitted to
	private float _label = 44f;
	private float _stepWidth = MinStepWidth;
	private float _rowHeight = 14f;
	private Size _fittedTo;
	private bool _dragging;
	private long _lastPress;
	private (int Step, int Row) _lastCell = (-1, -1);

	public RollEditor()
	{
		IsTabStop = false;
		_scroll.Content = _grid;
		Content = _scroll;
		_grid.PointerPressed += OnPressed;
		_grid.PointerMoved += OnMoved;
		_grid.PointerReleased += OnReleased;
		_grid.PointerCaptureLost += (_, _) => Release();
	}

	/// <summary>The roll it draws and edits through.</summary>
	public RollViewModel? Roll
	{
		get;
		set
		{
			if (field is not null)
				field.PropertyChanged -= OnRollChanged;
			field = value;
			if (field is not null)
				field.PropertyChanged += OnRollChanged;
			Fit(_fittedTo);
			Draw();
		}
	}

	private void OnRollChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(RollViewModel.Version))
			return;
		Fit(_fittedTo);
		Draw();
	}

	protected override Size MeasureOverride(Size available)
	{
		if (Fit(available))
			Draw();
		return base.MeasureOverride(available);
	}

	// The size it is given is only certain once it is arranged: a grid measures a star cell's content unbounded first. Fitted
	// to another size, it draws again and is measured again, the next layout pass drawing it to fill what it has.
	protected override Size ArrangeOverride(Size finalSize)
	{
		if (Fit(finalSize))
		{
			Draw();
			InvalidateMeasure();
		}
		return base.ArrangeOverride(finalSize);
	}

	// Fit the labels, the steps and the rows to `space`: the labels as wide as the longest row's name, the steps sharing
	// what is left across, the rows what there is down, each within its bounds. Returns whether any changed.
	private bool Fit(Size space)
	{
		// Unbounded says nothing of the space it will have: keep what it was last fitted to
		if (!float.IsFinite(space.Width) || !float.IsFinite(space.Height))
			return false;
		_fittedTo = space;
		if (Roll is not { IsEditable: true } roll || roll.Steps == 0 || roll.Rows.Count == 0)
			return false;
		var longest = 0;
		for (var r = 0; r < roll.Rows.Count; r++)
			longest = Math.Max(longest, roll.Rows[r].Length);
		var label = Math.Clamp(longest * 6.5f + 12f, 36f, 110f);
		var width = float.IsFinite(space.Width) ? space.Width - label - Gutter : 0f;
		var height = float.IsFinite(space.Height) ? space.Height - Gutter : 0f;
		var stepWidth = Math.Clamp(width / roll.Steps, MinStepWidth, MaxStepWidth);
		var (least, most) = roll.Kind == RollKind.Line ? (14f, 26f) : (22f, 34f);
		var rowHeight = Math.Clamp(height / roll.Rows.Count, least, most);
		if (label == _label && stepWidth == _stepWidth && rowHeight == _rowHeight)
			return false;
		(_label, _stepWidth, _rowHeight) = (label, stepWidth, rowHeight);
		return true;
	}

	private void Draw()
	{
		_grid.Children.Clear();
		if (Roll is not { IsEditable: true } roll || roll.Steps == 0)
		{
			_grid.Width = 0;
			_grid.Height = 0;
			return;
		}
		var (label, stepWidth, rowHeight) = (_label, _stepWidth, _rowHeight);
		_grid.Width = label + roll.Steps * stepWidth;
		_grid.Height = roll.Rows.Count * rowHeight;
		var text = MathF.Min(12f, rowHeight - 4f);

		for (var r = 0; r < roll.Rows.Count; r++)
		{
			// A line's octave rows (degree 1) lighter, so the eye finds its place
			if (roll.Kind == RollKind.Line && roll.Rows[r].TrimStart('#', 'b', '^').StartsWith('1'))
				Add(new Border { Width = roll.Steps * stepWidth, Height = rowHeight, Background = Theme.Raised }, label, r * rowHeight);
			Add(new TextBlock { Text = roll.Rows[r], FontSize = 11, Foreground = Theme.Dim, Width = label - 8, TextTrimming = TextTrimming.CharacterEllipsis },
				4, r * rowHeight + (rowHeight - 14) / 2);
		}
		var perBar = Math.Max(1, roll.StepsPerBar);
		var beat = Math.Max(1, perBar / 4);
		for (var s = 0; s <= roll.Steps; s++)
			if (s % beat == 0)
				Add(new Border { Width = s % perBar == 0 ? 1.5f : 1f, Height = _grid.Height, Background = s % perBar == 0 ? Theme.BarLine : Theme.Line },
					label + s * stepWidth, 0);

		if (roll.Kind == RollKind.Line)
			foreach (var (step, length, row, token) in roll.Notes)
			{
				var chosen = step == roll.Selected;
				var note = new Border
				{
					Width = length * stepWidth - 2,
					Height = rowHeight - 2,
					Background = chosen ? Theme.Accent : Theme.Note,
					CornerRadius = new CornerRadius(3),
					Child = new TextBlock
					{
						Text = token,
						FontSize = text - 1f,
						Foreground = Theme.AccentInk,
						Margin = new Thickness(4, 0, 0, 0),
						VerticalAlignment = VerticalAlignment.Center,
					},
				};
				Add(note, label + step * stepWidth + 1, row * rowHeight + 1);
			}
		else
			for (var r = 0; r < roll.Cells.Count; r++)
				for (var s = 0; s < roll.Cells[r].Count; s++)
				{
					var token = roll.Cells[r][s];
					if (token == ".")
						continue;
					var strong = token is "X" or "D" or "U" || (token.Length == 1 && char.IsDigit(token[0]) && token[0] >= '7');
					Add(new Border
					{
						Width = stepWidth - 3,
						Height = rowHeight - 4,
						Background = strong ? Theme.Accent : Theme.Note,
						Opacity = token is "o" or "d" or "u" or "_" ? 0.55f : 1f,
						CornerRadius = new CornerRadius(3),
						Child = new TextBlock
						{
							Text = token,
							FontSize = text,
							Foreground = Theme.AccentInk,
							HorizontalAlignment = HorizontalAlignment.Center,
							VerticalAlignment = VerticalAlignment.Center,
						},
					}, label + s * stepWidth + 1.5f, r * rowHeight + 2);
				}
	}

	private void Add(UIElement element, float x, float y)
	{
		element.IsHitTestVisible = false;
		Canvas.SetLeft(element, x);
		Canvas.SetTop(element, y);
		_grid.Children.Add(element);
	}

	// The step and row under the pointer, or null in the labels
	private (int Step, int Row)? Cell(PointerRoutedEventArgs e)
	{
		var at = e.GetCurrentPoint(_grid).Position;
		if (Roll is null || at.X < _label)
			return null;
		return ((int)MathF.Floor((at.X - _label) / _stepWidth), (int)MathF.Floor(at.Y / _rowHeight));
	}

	private void OnPressed(object sender, RoutedEventArgs e)
	{
		if (e is not PointerRoutedEventArgs p || Roll is not { IsEditable: true } roll || Cell(p) is not { } cell)
			return;
		var now = Environment.TickCount64;
		var twice = cell == _lastCell && now - _lastPress < 400;
		_lastPress = now;
		_lastCell = cell;
		var secondary = (p.Pointer.Buttons & PointerButtons.Right) != 0;
		roll.Press(cell.Step, cell.Row, secondary, twice);
		if (!secondary && roll.Kind == RollKind.Line)
		{
			_dragging = true;
			_grid.CapturePointer(p.PointerId);
		}
		e.Handled = true;
	}

	private void OnMoved(object sender, RoutedEventArgs e)
	{
		if (!_dragging || e is not PointerRoutedEventArgs p || Roll is null)
			return;
		var at = p.GetCurrentPoint(_grid).Position;
		Roll.DragTo((int)MathF.Floor((at.X - _label) / _stepWidth), (int)MathF.Floor(at.Y / _rowHeight));
		e.Handled = true;
	}

	private void OnReleased(object sender, RoutedEventArgs e)
	{
		if (!_dragging || e is not PointerRoutedEventArgs p)
			return;
		_grid.ReleasePointerCapture(p.PointerId);
		Release();
		e.Handled = true;
	}

	private void Release()
	{
		if (!_dragging)
			return;
		_dragging = false;
		Roll?.Release();
	}
}
