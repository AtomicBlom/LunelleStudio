using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// Under a condition being edited: a button making the cues it names that the project hasn't (docs/spec/ui-cues.md), as
/// the condition has them (<see cref="Workbench.MakeCuesIn"/>), then taking the condition again. Hidden while there are none.
/// </summary>
internal sealed class CueMaker : ContentControl
{
	private readonly Workbench? _workbench;
	private readonly Action<string> _again;
	private readonly Button _make = new() { Padding = new Thickness(8f, 2f, 8f, 3f), Margin = new Thickness(0f, 4f, 0f, 0f), HorizontalAlignment = HorizontalAlignment.Left };
	private readonly TextBlock _label = new() { FontSize = 11.5f, TextWrapping = TextWrapping.Wrap };
	private string? _condition;

	/// <param name="workbench">What makes the cues; none offers nothing.</param>
	/// <param name="again">Takes the condition again once its cues are made.</param>
	public CueMaker(Workbench? workbench, Action<string> again)
	{
		_workbench = workbench;
		_again = again;
		IsTabStop = false;
		_make.Content = _label;
		ToolTipService.SetToolTip(_make, "Make these cues in the project's cue files, as the condition reads them, and import them into this song");
		_make.Click += (_, _) =>
		{
			if (_condition is not { } condition || _workbench is null)
				return;
			if (_workbench.MakeCuesIn(condition) is { } why)
				_label.Text = why;
			else
			{
				Visibility = Visibility.Collapsed;
				_again(condition);
			}
		};
		Content = _make;
		Visibility = Visibility.Collapsed;
	}

	/// <summary>Offers to make the cues <paramref name="condition"/> names that the project hasn't; null offers nothing.</summary>
	public void Offer(string? condition)
	{
		var unknown = condition is null || _workbench is null ? [] : _workbench.UnknownCuesIn(condition);
		_condition = condition;
		Visibility = unknown.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
		_label.Text = "Make " + string.Join(", ", unknown.Select(u => u.Options.Count == 0
			? $"{u.Id} (a level)"
			: $"{u.Id} (a choice: {string.Join(", ", u.Options.Count == 1 ? [u.Options[0], "Other"] : u.Options)})"));
	}
}
