using System.ComponentModel;

using SUIsei.Controls;
using SUIsei.Input;
using SUIsei.RoutedEvents;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The command palette: its field takes the keys as it opens, what is typed filters it, Up and Down choose, Enter runs,
/// Escape or a click on the backdrop closes it.
/// </summary>
internal sealed partial class CommandPalette : ContentControl, INotifyPropertyChanged
{
	public CommandPalette()
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
			if (field is not null)
				field.PropertyChanged -= OnModelChanged;
			field = value;
			if (value is not null)
				value.PropertyChanged += OnModelChanged;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
		}
	}

	partial void OnComponentRebuilt() => Wire();

	private void Wire()
	{
		Query.TextChanged += (_, _) =>
		{
			if (ViewModel is { PaletteOpen: true } model)
				model.PaletteQuery = Query.Text;
		};
		Query.AddHandler(PreviewKeyDownEvent, OnQueryKey);
		Backdrop.PointerPressed += (_, _) =>
		{
			if (ViewModel is { } model)
				model.PaletteOpen = false;
		};
	}

	private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(WorkbenchViewModel.PaletteOpen) || ViewModel is not { } model)
			return;
		if (model.PaletteOpen)
		{
			Query.Text = "";
			Query.Focus();
		}
		model.IsTyping = model.PaletteOpen;
	}

	private void OnQueryKey(object sender, RoutedEventArgs e)
	{
		if (e is not KeyRoutedEventArgs key || ViewModel is not { } model)
			return;
		switch (key.Key)
		{
			case VirtualKey.Up:
				model.MovePalette(-1);
				e.Handled = true;
				break;
			case VirtualKey.Down:
				model.MovePalette(1);
				e.Handled = true;
				break;
			case VirtualKey.Enter:
				model.RunPalette();
				e.Handled = true;
				break;
			case VirtualKey.Escape:
				model.PaletteOpen = false;
				e.Handled = true;
				break;
		}
	}
}
