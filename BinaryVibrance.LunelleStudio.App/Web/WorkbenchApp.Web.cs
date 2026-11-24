using BinaryVibrance.LunelleStudio.App.Web;
using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.App;

// The browser's studio: its one project kept in the browser's files (BrowserProject), saved as a .lunelle file and
// replaced by one, from two buttons at the transport's end. It never compiles anything.
public sealed partial class WorkbenchApp
{
	private bool _importing;

	private partial bool ProjectHasMusic() => false;

	partial void AddControls(WorkbenchView view)
	{
		var extras = view.TransportExtras;
		extras.Children.Add(Button("export", "Save this music as a .lunelle file, to keep or to open elsewhere", Export));
		extras.Children.Add(Button("import", "Open a .lunelle file in place of this music", () => _ = ImportAsync()));
		if (_files is BrowserFiles { Kept: false })
			_workbench.ViewModel.Notice = "this browser keeps nothing: export the music to keep it";
	}

	private static Button Button(string text, string tip, Action click)
	{
		var button = new Button
		{
			Content = new TextBlock { Text = text, FontSize = 13f, Margin = new Thickness(8, 3, 8, 4) },
			Margin = new Thickness(0, 0, 4, 4),
		};
		ToolTipService.SetToolTip(button, tip);
		button.Click += (_, _) => click();
		return button;
	}

	private void Export()
	{
		var name = MusicArchive.FileName(_folder?.Current?.Name ?? "music");
		try
		{
			var archive = MusicArchive.Pack(_files, BrowserProject.Folder);
			_workbench.ReportStatus(SaveFile(name, "application/zip", archive) ? $"saved {name}" : $"{name} couldn't be saved");
		}
		catch (IOException ex)
		{
			_workbench.ReportStatus($"{name} couldn't be saved: {ex.Message}");
		}
	}

	private async Task ImportAsync()
	{
		if (_importing || _files is not BrowserFiles files)
			return;
		_importing = true;
		try
		{
			var name = await StudioPage.PickFile($"{MusicArchive.Extension},.zip",
				"Open {0} in place of the music here? Export this music first to keep it.");
			if (name.Length == 0)
				return;
			var count = BrowserProject.Import(files, StudioPage.PickedTake());
			_workbench.ReportStatus($"opened {name}: {count} files");
		}
		catch (InvalidDataException ex)
		{
			_workbench.ReportStatus($"that file couldn't be opened: {ex.Message}");
		}
		finally
		{
			_importing = false;
		}
	}
}
