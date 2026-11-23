using System.ComponentModel;

using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.SirrowStudio.Views;

/// <summary>The keyboard docked along the bottom, and what it plays.</summary>
internal sealed partial class KeyboardDock : ContentControl, INotifyPropertyChanged
{
	public KeyboardDock()
	{
		InitializeComponent();
		OctaveDown.Click += (_, _) =>
		{
			if (ViewModel is { } model)
				model.Octave--;
		};
		OctaveUp.Click += (_, _) =>
		{
			if (ViewModel is { } model)
				model.Octave++;
		};
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
