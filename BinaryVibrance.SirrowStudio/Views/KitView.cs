using System.ComponentModel;

using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.SirrowStudio.Views;

/// <summary>Kit: a pad per piece of the kit chosen.</summary>
internal sealed partial class KitView : ContentControl, INotifyPropertyChanged
{
	public KitView() => InitializeComponent();

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
