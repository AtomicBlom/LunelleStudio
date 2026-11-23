using System.ComponentModel;

using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.SirrowStudio.Views;

/// <summary>Files: the music's files down the side, the one chosen beside them, copied whole or a piece at a time.</summary>
internal sealed partial class FilesView : ContentControl, INotifyPropertyChanged
{
	public FilesView() => InitializeComponent();

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
}
