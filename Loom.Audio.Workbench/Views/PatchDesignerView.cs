using System.ComponentModel;

using SUIsei;
using SUIsei.Controls;
using SUIsei.Controls.Primitives;
using SUIsei.Input;
using SUIsei.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>Patch: the patch designer's graph, chain, scope and spectrum.</summary>
internal sealed partial class PatchDesignerView : ContentControl, INotifyPropertyChanged
{
	public PatchDesignerView()
	{
		InitializeComponent();
		Duplicate.Click += (_, _) => AskForCopy();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
		}
	}

	// A flyout asking the copy's name, Enter making it; the copy opens once the project has built with it
	private void AskForCopy()
	{
		if (ViewModel is not { Workbench: { } workbench } model || model.SelectedPatch < 0 || model.SelectedPatch >= model.Patches.Count)
			return;
		var patch = model.Patches[model.SelectedPatch].Name;
		var name = new TextBox { Text = patch + "-copy", Width = 240f };
		ToolTipService.SetToolTip(name, "Lowercase letters, digits and hyphens. Press Enter to make the copy.");
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Width = 240f };
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = $"Duplicate {patch}", FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		column.Children.Add(name);
		column.Children.Add(new TextBlock
		{
			Text = "To give one song's track its own copy, use Make a copy for this song in the track's inspector.",
			FontSize = 11f,
			Foreground = Theme.Faint,
			TextWrapping = TextWrapping.Wrap,
			Width = 240f,
			Margin = new Thickness(0f, 6f, 0f, 0f),
		});
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
		name.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is not KeyRoutedEventArgs { Key: VirtualKey.Enter })
				return;
			var why = workbench.DuplicatePatch(patch, name.Text);
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		});
		flyout.ShowAt(Duplicate);
	}
}
