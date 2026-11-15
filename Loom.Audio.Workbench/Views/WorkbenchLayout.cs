using System.ComponentModel;

using SUIsei;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The workbench page's layout (<c>WorkbenchLayout.xaml</c>): its regions, each a view made here over the page's view model and put
/// in its host, made again when hot reload rebuilds the page. <see cref="WorkbenchView"/> hosts it.
/// </summary>
internal sealed partial class WorkbenchLayout : SUIsei.Controls.ContentControl
{
	/// <summary>A page over <paramref name="viewModel"/>.</summary>
	public WorkbenchLayout(WorkbenchViewModel viewModel)
	{
		ArgumentNullException.ThrowIfNull(viewModel);
		ViewModel = viewModel;
		InitializeComponent();
		Page.Background = Theme.Page;
		Fill();
		viewModel.PropertyChanged += OnChanged;
	}

	/// <summary>What it shows.</summary>
	public WorkbenchViewModel ViewModel { get; }

	/// <summary>The transport bar, for an app to add controls of its own to (<see cref="TransportBar.Extras"/>).</summary>
	public TransportBar Transport { get; private set; } = null!;

	partial void OnComponentRebuilt()
	{
		Page.Background = Theme.Page;
		Fill();
	}

	private void Fill()
	{
		Transport = new TransportBar(ViewModel);
		TransportHost.Content = Transport;
		SectionHost.Content = new SectionStrip(ViewModel);
		CueHost.Content = new CuePanel(ViewModel);
		TrackHost.Content = new TrackList(ViewModel);
		BenchHost.Content = new PatchBench(ViewModel);
		PatternHost.Content = new PatternView(ViewModel.Pattern);
		ShowError();
		StatusText.Text = ViewModel.StatsText;
	}

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(WorkbenchViewModel.Error):
				ShowError();
				break;
			case nameof(WorkbenchViewModel.StatsText):
				StatusText.Text = ViewModel.StatsText;
				break;
		}
	}

	// The banner is how a notation's mistake is seen: there while there is one
	private void ShowError()
	{
		ErrorText.Text = ViewModel.Error;
		ErrorBanner.Visibility = ViewModel.Error.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
	}
}
