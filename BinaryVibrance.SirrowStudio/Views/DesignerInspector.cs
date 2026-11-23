using System.ComponentModel;

using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.SirrowStudio.Views;

/// <summary>The patch designer's inspector: the operator chosen, the voice, the macros, the code.</summary>
internal sealed partial class DesignerInspector : ContentControl, INotifyPropertyChanged
{
	public DesignerInspector() => InitializeComponent();

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
