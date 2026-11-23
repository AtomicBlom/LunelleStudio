using System.Numerics;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Input;
using BinaryVibrance.Vysara.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// Where something can be put in, shown only while the pointer is near it: a line in the accent along the gap, and a round
/// + on it under the pointer. A click on the + asks what to put there (<see cref="Chosen"/>, with the + to anchor a menu
/// to). It lies over its host, which says where the gaps are (<see cref="Follow"/>); while the menu it opened is up
/// (<see cref="IsHeld"/>) it stays where it was.
/// </summary>
internal sealed class InsertLine : ContentControl
{
	private const float Size = 18f;

	private readonly Canvas _canvas = new();
	private readonly Border _line = new() { Height = 2f, CornerRadius = new CornerRadius(1f), Background = Theme.Accent, IsHitTestVisible = false };
	private readonly Border _plus = new() { Width = Size, Height = Size, CornerRadius = new CornerRadius(Size / 2f), Background = Theme.Accent };
	private bool _pressed;

	public InsertLine(string tip)
	{
		IsTabStop = false;
		var glyph = Glyphs.Plus(10f, Theme.AccentInk);
		glyph.HorizontalAlignment = HorizontalAlignment.Center;
		glyph.VerticalAlignment = VerticalAlignment.Center;
		_plus.Child = glyph;
		ToolTipService.SetToolTip(_plus, tip);
		_canvas.Children.Add(_line);
		_canvas.Children.Add(_plus);
		Content = _canvas;
		// Pressed and released on the +: the host's own press handling never sees it
		_plus.AddHandler(PointerPressedEvent, (_, e) =>
		{
			if (e is PointerRoutedEventArgs pointer && pointer.Pointer.Buttons.HasFlag(PointerButtons.Left))
			{
				_pressed = true;
				e.Handled = true;
			}
		});
		_plus.AddHandler(PointerReleasedEvent, (_, e) =>
		{
			if (!_pressed)
				return;
			_pressed = false;
			e.Handled = true;
			Chosen?.Invoke(_plus);
		});
		_plus.PointerExited += (_, _) => _pressed = false;
		Visibility = Visibility.Collapsed;
	}

	/// <summary>The + was clicked: what to put in is asked, the + given to anchor the question to.</summary>
	public event Action<FrameworkElement>? Chosen;

	/// <summary>Kept where it is, as it is, while what it asked is open; <see cref="Release"/> lets it go.</summary>
	public bool IsHeld { get; set; }

	/// <summary>
	/// Shows it while the pointer is over <paramref name="host"/> and <paramref name="gap"/> finds a gap near the pointer
	/// (in the host's space: the line's height, its left and its width), and hides it otherwise.
	/// </summary>
	public void Follow(UIElement host, Func<Vector2, (float Y, float Left, float Width)?> gap)
	{
		host.AddHandler(PointerMovedEvent, (_, e) =>
		{
			if (IsHeld || e is not PointerRoutedEventArgs pointer)
				return;
			var at = pointer.GetCurrentPoint(host).Position;
			if (gap(at) is { } found)
				Show(found.Y, at.X, found.Left, found.Width, host.LayoutRect.Height);
			else
				Visibility = Visibility.Collapsed;
		}, handledEventsToo: true);
		host.PointerExited += (_, _) =>
		{
			if (!IsHeld)
				Visibility = Visibility.Collapsed;
		};
	}

	// The line along the gap at y, the + on it under the pointer, both kept inside the host
	private void Show(float y, float x, float left, float width, float height)
	{
		_line.Width = width;
		Canvas.SetLeft(_line, left);
		Canvas.SetTop(_line, Math.Clamp(y - 1f, 0f, Math.Max(0f, height - 2f)));
		Canvas.SetLeft(_plus, Math.Clamp(x, left + Size / 2f, left + width - Size / 2f) - Size / 2f);
		Canvas.SetTop(_plus, Math.Clamp(y, Size / 2f, Math.Max(Size / 2f, height - Size / 2f)) - Size / 2f);
		Visibility = Visibility.Visible;
	}

	/// <summary>Lets it go after what it asked is closed, hidden until the pointer moves over a gap again.</summary>
	public void Release()
	{
		IsHeld = false;
		Visibility = Visibility.Collapsed;
	}
}
