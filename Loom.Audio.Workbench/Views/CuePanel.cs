using System.ComponentModel;

using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>The cues (<c>CuePanel.xaml</c>): a row each, bound to <see cref="ViewModel"/>.</summary>
internal sealed partial class CuePanel : ContentControl, INotifyPropertyChanged
{
	/// <summary>A panel with nothing to show until it is given a <see cref="ViewModel"/>.</summary>
	public CuePanel() => InitializeComponent();

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
}
