using System.ComponentModel;

using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>The transport (<c>TransportBar.xaml</c>): the songs, play, the position, tempo and scale, bound to <see cref="ViewModel"/>.</summary>
internal sealed partial class TransportBar : ContentControl, INotifyPropertyChanged
{
	/// <summary>A bar with nothing to show until it is given a <see cref="ViewModel"/>.</summary>
	public TransportBar() => InitializeComponent();

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>What it shows: its bindings follow it when it is set.</summary>
	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
		}
	}

	/// <summary>
	/// A panel at the bar's end for an app's own controls (a toggle for a sound of its own under the music).
	/// </summary>
	public StackPanel Extras => ExtrasPanel;
}
