using System.ComponentModel;


using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>The library: the catalog filtered by what is typed. While its filter has the keys, the workbench's own keys wait.</summary>
internal sealed partial class CatalogBrowser : ContentControl, INotifyPropertyChanged
{
	public CatalogBrowser()
	{
		InitializeComponent();
		Wire();
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

	partial void OnComponentRebuilt() => Wire();

	private void Wire()
	{
		Filter.TextChanged += (_, _) =>
		{
			if (ViewModel is { } model)
				model.LibraryQuery = Filter.Text;
		};
		Filter.GotFocus += (_, _) =>
		{
			if (ViewModel is { } model)
				model.IsTyping = true;
		};
		Filter.LostFocus += (_, _) =>
		{
			if (ViewModel is { } model)
				model.IsTyping = false;
		};
	}
}
