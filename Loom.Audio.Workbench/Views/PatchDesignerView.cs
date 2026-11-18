using System.ComponentModel;

using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>Patch: the patch designer's graph, chain, scope and spectrum.</summary>
internal sealed partial class PatchDesignerView : ContentControl, INotifyPropertyChanged
{
	public PatchDesignerView() => InitializeComponent();

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
