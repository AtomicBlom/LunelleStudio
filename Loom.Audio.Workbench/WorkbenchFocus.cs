using Vysara;
using Vysara.Controls;

namespace Loom.Audio.Workbench;

/// <summary>
/// The workbench's keys work whatever was last clicked. A press focuses the control it lands on, as in WinUI, and while
/// anything in the UI holds focus the host keeps the keyboard from the app; so a click on a button or a slider would
/// leave Space, the piano's keys and Z and X dead until focus moved on. A host calls <see cref="KeepKeys"/> once a
/// frame, after its UI's tick: focus a pointer gave to anything but a text box is let go again. Focus moved by the
/// keyboard (Tab) or set by code is kept, and so is a text box's, which the keys are typed into.
/// </summary>
public static class WorkbenchFocus
{
	/// <summary>Lets go of focus a pointer press gave to anything but a text box.</summary>
	public static void KeepKeys(FocusManager focus)
	{
		ArgumentNullException.ThrowIfNull(focus);
		if (focus.FocusState == FocusState.Pointer && focus.FocusedElement is { } focused and not TextBox)
			focus.TrySetFocus(null, FocusState.Programmatic);
	}
}
