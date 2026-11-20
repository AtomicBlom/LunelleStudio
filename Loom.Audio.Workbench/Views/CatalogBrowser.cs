using System.ComponentModel;
using SUIsei;
using SUIsei.Controls;
using SUIsei.Controls.Primitives;
using SUIsei.Input;
using SUIsei.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The library: the catalog filtered by what is typed, as lists of songs, patches and kits. A click on a row opens it, and
/// leaves the keys with the workbench (<see cref="WorkbenchFocus"/>); a list reached with Tab takes the arrows, and Enter
/// opens the row. The lists show what is open. While its filter has the keys, the workbench's own keys wait. The + on
/// Songs and Patches makes a new file of that kind beside the project's others, which it lists once the project has built
/// again.
/// </summary>
internal sealed partial class CatalogBrowser : ContentControl, INotifyPropertyChanged
{
	public CatalogBrowser()
	{
		InitializeComponent();
		Wire();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field is not null)
				field.PropertyChanged -= OnModelChanged;
			field = value;
			if (value is not null)
				value.PropertyChanged += OnModelChanged;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
			ShowChosen();
		}
	}

	partial void OnComponentRebuilt() => Wire();

	private void Wire()
	{
		Filter.TextChanged += (_, _) =>
		{
			if (ViewModel is { } model)
				model.LibraryQuery = Filter.Text;
		};
		Filter.GotFocus += (_, _) =>
		{
			if (ViewModel is { } model)
				model.IsTyping = true;
		};
		Filter.LostFocus += (_, _) =>
		{
			if (ViewModel is { } model)
				model.IsTyping = false;
		};
		foreach (var list in new[] { SongList, PatchList, KitList })
			list.ItemClick += (_, e) =>
			{
				if (e.ClickedItem is ChoiceViewModel choice)
					choice.IsSelected = true;
			};
		NewSong.Click += (_, _) => AskForName(NewSong, "New song", "open-road", copy: false);
		NewPatch.Click += (_, _) => AskForName(NewPatch, "New patch", "warm-pad", copy: true);
	}

	private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(WorkbenchViewModel.SelectedSong) or nameof(WorkbenchViewModel.SelectedPatch) or nameof(WorkbenchViewModel.SelectedKit)
			or nameof(WorkbenchViewModel.Songs) or nameof(WorkbenchViewModel.Patches) or nameof(WorkbenchViewModel.Kits))
			ShowChosen();
	}

	// The lists lit at what is open
	private void ShowChosen()
	{
		if (ViewModel is not { } model)
			return;
		SongList.SelectedIndex = model.SelectedSong;
		PatchList.SelectedIndex = model.SelectedPatch;
		KitList.SelectedIndex = model.SelectedKit;
	}

	// A flyout asking for the new file's name (and, for a patch, whether to start from the one open), Enter making it
	private void AskForName(Button anchor, string title, string example, bool copy)
	{
		if (ViewModel?.Workbench is not { } workbench)
			return;
		var name = new TextBox { Width = 220f };
		ToolTipService.SetToolTip(name, $"Its name: letters, digits and hyphens, as {example}");
		var from = new ToggleSwitch { OnContent = "a copy of the patch open", OffContent = "blank", IsOn = copy && ViewModel.SelectedPatch >= 0 };
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Width = 220f };
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = title, FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		column.Children.Add(name);
		if (copy)
			column.Children.Add(from);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		name.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is not KeyRoutedEventArgs { Key: VirtualKey.Enter })
				return;
			var open = ViewModel?.SelectedPatch is >= 0 and var at && at < ViewModel.Patches.Count ? ViewModel.Patches[at].Name : null;
			var why = copy ? workbench.NewPatch(name.Text, from.IsOn ? open : null) : workbench.NewSong(name.Text);
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		});
		flyout.ShowAt(anchor);
	}
}
