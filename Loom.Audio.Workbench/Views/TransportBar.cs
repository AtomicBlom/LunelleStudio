using System.Collections.Specialized;
using System.ComponentModel;

using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>The transport (<c>TransportBar.xaml</c>): the songs, play, the position, tempo and scale.</summary>
internal sealed partial class TransportBar : ContentControl
{
	private readonly List<ToggleButton> _songs = [];
	private bool _showing;

	/// <summary>A bar over <paramref name="viewModel"/>.</summary>
	public TransportBar(WorkbenchViewModel viewModel)
	{
		ViewModel = viewModel;
		InitializeComponent();
		Apply();
		viewModel.PropertyChanged += OnChanged;
		viewModel.Songs.CollectionChanged += OnSongsChanged;
	}

	/// <summary>What it shows.</summary>
	public WorkbenchViewModel ViewModel { get; }

	/// <summary>A panel at the bar's end for an app's own controls (the Studio's recording toggle).</summary>
	public StackPanel Extras => ExtrasPanel;

	partial void OnComponentRebuilt() => Apply();

	private void Apply()
	{
		PositionLabel.Foreground = Theme.Ink;
		TempoLabel.Foreground = Theme.Dim;
		ScaleLabel.Foreground = Theme.Dim;
		PlayButton.Checked += (_, _) => { if (!_showing) ViewModel.IsPlaying = true; Show(); };
		PlayButton.Unchecked += (_, _) => { if (!_showing) ViewModel.IsPlaying = false; Show(); };
		MakeSongs();
		Show();
	}

	private void OnSongsChanged(object? sender, NotifyCollectionChangedEventArgs e) => MakeSongs();

	private void MakeSongs()
	{
		SongButtons.Children.Clear();
		_songs.Clear();
		for (var i = 0; i < ViewModel.Songs.Count; i++)
		{
			var index = i;
			var button = Make.Toggle(ViewModel.Songs[i]);
			button.Click += (_, _) => { ViewModel.SelectedSong = index; Show(); };
			_songs.Add(button);
			SongButtons.Children.Add(button);
		}
	}

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(WorkbenchViewModel.IsPlaying) or nameof(WorkbenchViewModel.SelectedSong))
			Show();
	}

	// The buttons as the model says, without that acting again
	private void Show()
	{
		_showing = true;
		PlayButton.IsChecked = ViewModel.IsPlaying;
		PlayLabel.Text = ViewModel.IsPlaying ? "Stop" : "Play";
		for (var i = 0; i < _songs.Count; i++)
			_songs[i].IsChecked = i == ViewModel.SelectedSong;
		_showing = false;
	}
}
