using System.ComponentModel;

using SUIsei;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The workbench page's layout (<c>WorkbenchLayout.xaml</c>): its regions, each a view handed the page's view model in
/// its XAML. <see cref="WorkbenchView"/> hosts it.
/// </summary>
internal sealed partial class WorkbenchLayout : SUIsei.Controls.ContentControl
{
	/// <summary>A page over <paramref name="viewModel"/>.</summary>
	public WorkbenchLayout(WorkbenchViewModel viewModel)
	{
		ArgumentNullException.ThrowIfNull(viewModel);
		ViewModel = viewModel;
		InitializeComponent();
		// The built-in controls in the workbench's colours, everywhere under the page, and the rail's as markers of the mode.
		// Set before the page joins a tree, so every control's template reads them as it is built.
		(Resources ??= new ResourceDictionary()).MergedDictionaries.Add(Theme.Controls());
		RailModes.Resources = Theme.Markers();
	}

	/// <summary>What it shows.</summary>
	public WorkbenchViewModel ViewModel { get; }

	/// <summary>The transport bar, for an app to add controls of its own to (<see cref="TransportBar.Extras"/>).</summary>
	public TransportBar Transport => TransportView;
}
