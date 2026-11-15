using System.Collections.Specialized;

using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>The cues (<c>CuePanel.xaml</c>): a <see cref="CueRow"/> each.</summary>
internal sealed partial class CuePanel : ContentControl
{
	private readonly WorkbenchViewModel _model;

	/// <summary>A panel over <paramref name="viewModel"/>'s cues.</summary>
	public CuePanel(WorkbenchViewModel viewModel)
	{
		_model = viewModel;
		InitializeComponent();
		Apply();
		viewModel.Cues.CollectionChanged += OnCuesChanged;
	}

	partial void OnComponentRebuilt() => Apply();

	private void OnCuesChanged(object? sender, NotifyCollectionChangedEventArgs e) => MakeRows();

	private void Apply()
	{
		Heading.Foreground = Theme.Dim;
		MakeRows();
	}

	private void MakeRows()
	{
		Rows.Children.Clear();
		foreach (var cue in _model.Cues)
			Rows.Children.Add(new CueRow(cue));
	}
}
