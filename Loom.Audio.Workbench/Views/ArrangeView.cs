using System.ComponentModel;

using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>Arrange: the song's sections, its tracks as lanes with their parts by section, and the part chosen.</summary>
internal sealed partial class ArrangeView : ContentControl, INotifyPropertyChanged
{
	public ArrangeView()
	{
		InitializeComponent();
		// The section playing is marked, as the rail's mode is, rather than switched on
		SectionStrip.Resources = Theme.Markers();
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
}
