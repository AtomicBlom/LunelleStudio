using System.Numerics;
using BinaryVibrance.LunelleStudio;
using BinaryVibrance.Kaldra.Input;
using BinaryVibrance.Kaldra.UI.Vysara;
using Shouldly;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>A click in the workbench leaves its keys working: the focus a press gives a control is let go, a text box's kept.</summary>
public class WorkbenchFocusTests
{
	private static readonly Vector2 Surface = new(400f, 300f);

	[Test]
	public void A_clicked_button_gives_the_keyboard_back()
	{
		var (host, stack, _, _) = Page();

		Frame(host, stack, InputEvent.PointerMove(20f, 10f), InputEvent.MouseButtonDown(MouseButton.Left));
		Frame(host, stack, InputEvent.MouseButtonUp(MouseButton.Left));
		host.Focus.FocusedElement.ShouldBeNull();

		var keys = Frame(host, stack, InputEvent.KeyDown(Key.Z));
		keys.Pressed(Key.Z).ShouldBeTrue();
	}

	[Test]
	public void A_clicked_text_box_keeps_the_keys()
	{
		var (host, stack, _, box) = Page();

		Frame(host, stack, InputEvent.PointerMove(20f, 60f), InputEvent.MouseButtonDown(MouseButton.Left));
		Frame(host, stack, InputEvent.MouseButtonUp(MouseButton.Left));
		host.Focus.FocusedElement.ShouldBe(box);

		var keys = Frame(host, stack, InputEvent.KeyDown(Key.Z));
		keys.Pressed(Key.Z).ShouldBeFalse();
	}

	[Test]
	public void Focus_moved_by_the_keyboard_is_kept()
	{
		var (host, _, button, _) = Page();
		host.Focus.TrySetFocus(button, FocusState.Keyboard);

		WorkbenchFocus.KeepKeys(host.Focus);

		host.Focus.FocusedElement.ShouldBe(button);
	}

	private static (UIHost Host, InputLayerStack Stack, Button Button, TextBox Box) Page()
	{
		var host = new UIHost();
		var view = host.CreateRoot(UILayer.Default);
		var button = new Button { Width = 80f, Height = 30f };
		var box = new TextBox { Width = 80f, Height = 30f, Margin = new Thickness(0f, 20f, 0f, 0f) };
		var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
		panel.Children.Add(button);
		panel.Children.Add(box);
		view.Root.Content = panel;
		host.Tick(0.016, 0f, Surface);
		var stack = new InputLayerStack();
		stack.Push(new VysaraInputLayer(host));
		return (host, stack, button, box);
	}

	// One frame as the workbench runs it: input resolved against the last tick's report, then the tick, then the focus kept
	private static InputSnapshot Frame(UIHost host, InputLayerStack stack, params InputEvent[] events)
	{
		var output = stack.Resolve(events);
		host.Tick(0.016, 0f, Surface);
		WorkbenchFocus.KeepKeys(host.Focus);
		return output;
	}
}
