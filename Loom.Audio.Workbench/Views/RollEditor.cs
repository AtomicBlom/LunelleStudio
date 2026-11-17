using System.ComponentModel;

using SUIsei;
using SUIsei.Controls;
using SUIsei.Input;
using SUIsei.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The roll, drawn: a grid of steps across and rows down (a line's degrees, a beat's pieces, a strum's strokes), bar
/// lines heavier, the notes as bars and the hits as cells. A press, a drag and a release go to the roll's view model,
/// which makes the edit; a right-click or a second click on the same note deletes it.
/// </summary>
internal sealed class RollEditor : ContentControl
{
	private const float Label = 44f;
	private const float StepWidth = 20f;

	private readonly Canvas _grid = new() { Background = Theme.Sunk };
	private bool _dragging;
	private long _lastPress;
	private (int Step, int Row) _lastCell = (-1, -1);

	public RollEditor()
	{
		IsTabStop = false;
		Content = _grid;
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
			Draw();
		}
	}

	private float RowHeight => Roll?.Kind == RollKind.Line ? 13f : 20f;

	private void OnRollChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(RollViewModel.Version))
			Draw();
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
		var rowHeight = RowHeight;
		_grid.Width = Label + roll.Steps * StepWidth;
		_grid.Height = roll.Rows.Count * rowHeight;

		for (var r = 0; r < roll.Rows.Count; r++)
		{
			// A line's octave rows (degree 1) lighter, so the eye finds its place
			if (roll.Kind == RollKind.Line && roll.Rows[r].TrimStart('#', 'b', '^').StartsWith('1'))
				Add(new Border { Width = roll.Steps * StepWidth, Height = rowHeight, Background = Theme.Raised }, Label, r * rowHeight);
			Add(new TextBlock { Text = roll.Rows[r], FontSize = 10, Foreground = Theme.Dim, Width = Label - 6 }, 2, r * rowHeight + (rowHeight - 12) / 2);
		}
		var perBar = Math.Max(1, roll.StepsPerBar);
		var beat = Math.Max(1, perBar / 4);
		for (var s = 0; s <= roll.Steps; s++)
			if (s % beat == 0)
				Add(new Border { Width = s % perBar == 0 ? 1.5f : 1f, Height = _grid.Height, Background = s % perBar == 0 ? Theme.BarLine : Theme.Line },
					Label + s * StepWidth, 0);

		if (roll.Kind == RollKind.Line)
			foreach (var (step, length, row, token) in roll.Notes)
			{
				var chosen = step == roll.Selected;
				var note = new Border
				{
					Width = length * StepWidth - 2,
					Height = rowHeight - 2,
					Background = chosen ? Theme.Accent : Theme.Note,
					CornerRadius = new CornerRadius(2),
					Child = new TextBlock { Text = token, FontSize = 9, Foreground = Theme.AccentInk, Margin = new Thickness(3, 0, 0, 0) },
				};
				Add(note, Label + step * StepWidth + 1, row * rowHeight + 1);
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
						Width = StepWidth - 3,
						Height = rowHeight - 4,
						Background = strong ? Theme.Accent : Theme.Note,
						Opacity = token is "o" or "d" or "u" or "_" ? 0.55f : 1f,
						CornerRadius = new CornerRadius(3),
						Child = new TextBlock { Text = token, FontSize = 10, Foreground = Theme.AccentInk, HorizontalAlignment = HorizontalAlignment.Center },
					}, Label + s * StepWidth + 1.5f, r * rowHeight + 2);
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
		if (Roll is null || at.X < Label)
			return null;
		return ((int)MathF.Floor((at.X - Label) / StepWidth), (int)MathF.Floor(at.Y / RowHeight));
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
		Roll.DragTo((int)MathF.Floor((at.X - Label) / StepWidth), (int)MathF.Floor(at.Y / RowHeight));
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
