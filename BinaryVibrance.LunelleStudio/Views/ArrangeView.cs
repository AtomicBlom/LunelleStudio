using System.ComponentModel;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;
using BinaryVibrance.Vysara.Input;
using BinaryVibrance.Vysara.Media;
using BinaryVibrance.Vysara.RoutedEvents;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// Arrange: the board of sections and tracks, the inspector, and the part chosen. The heads drawn over the lanes follow
/// how far they are scrolled (<see cref="ArrangeScroll"/>), told as the lanes move and checked each frame; the corner
/// over both heads follows both ways. The splitter's place is kept with the workbench's state.
/// </summary>
internal sealed partial class ArrangeView : ContentControl, INotifyPropertyChanged
{
	private readonly TranslateTransform _corner = new();

	public ArrangeView()
	{
		InitializeComponent();
		Corner.RenderTransform = _corner;
		Lanes.ViewChanged += (_, _) => Follow();
		AddTrack.Click += (_, _) => AskForTrack();
		Splitter.Resized += (_, _) =>
		{
			if (ViewModel is { } model)
				model.PartPanelHeight = Layout.RowDefinitions[3].Value;
		};
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field is not null)
				field.Frame -= OnFrame;
			field = value;
			if (value is not null)
			{
				value.Frame += OnFrame;
				Layout.RowDefinitions[3] = GridLength.Pixels(value.PartPanelHeight);
				Layout.InvalidateMeasure();
			}
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
		}
	}

	private void OnFrame() => Follow();

	// The heads moved back by as much as the lanes are scrolled, so they stay put
	private void Follow()
	{
		if (ViewModel is not { } model)
			return;
		model.ArrangeScroll.X = Lanes.HorizontalOffset;
		model.ArrangeScroll.Y = Lanes.VerticalOffset;
		_corner.X = Lanes.HorizontalOffset;
		_corner.Y = Lanes.VerticalOffset;
	}

	// A flyout asking for the new track's name and what it plays
	private void AskForTrack()
	{
		if (ViewModel?.Workbench is not { } workbench)
			return;
		if (!workbench.CanEditSong)
		{
			var note = new Flyout { Content = new TextBlock { Text = "This song is defined in code. Only songs with a .song.json file can be edited here.", FontSize = 12f, Foreground = Theme.Dim } };
			note.ShowAt(AddTrack);
			return;
		}
		var instruments = workbench.Instruments.ToList();
		var name = new TextBox { Text = "", Width = 260f };
		ToolTipService.SetToolTip(name, "Track name");
		var list = new ListView
		{
			ItemsSource = instruments.Select(i => (object?)$"{(i.IsKit ? "kit" : "patch")} · {i.Name}").ToList(),
			SelectedIndex = instruments.Count > 0 ? 0 : -1,
			MaxHeight = 220f,
		};
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
		var add = new Button { Content = new TextBlock { Text = "Add track", FontSize = 12f }, Padding = new Thickness(10f, 3f, 10f, 4f), Margin = new Thickness(0f, 8f, 0f, 0f) };
		var column = new StackPanel { Orientation = Orientation.Vertical, Width = 280f };
		column.Children.Add(Label("Name"));
		column.Children.Add(name);
		column.Children.Add(Label("Plays"));
		column.Children.Add(list);
		column.Children.Add(add);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
		void Add()
		{
			if (list.SelectedIndex < 0)
				return;
			var (instrument, isKit) = instruments[list.SelectedIndex];
			var why = workbench.AddTrack(name.Text, instrument, isKit);
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		}
		add.Click += (_, _) => Add();
		name.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e is KeyRoutedEventArgs { Key: VirtualKey.Enter })
				Add();
		});
		flyout.ShowAt(AddTrack);
	}

	private static TextBlock Label(string text) => new() { Text = text.ToUpperInvariant(), FontSize = 10f, Foreground = Theme.Faint, Margin = new Thickness(0f, 6f, 0f, 4f) };
}
