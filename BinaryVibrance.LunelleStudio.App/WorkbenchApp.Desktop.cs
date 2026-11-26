using System.Diagnostics;

using BinaryVibrance.Kaldra.Core.Hosting;
using BinaryVibrance.LunelleStudio.Projects;

namespace BinaryVibrance.LunelleStudio.App;

// A project with code, compiled in-process with Roslyn (BinaryVibrance.LunelleStudio.Projects), and its extension's
// project watched: the desktop's alone
public sealed partial class WorkbenchApp
{
	private Task<MusicProject>? _opening;
	private WorkbenchProject? _project;
	private bool _opened;

	partial void OpenProject() =>
		_opening = MusicProject.OpenAsync(projectPath, new Dictionary<string, string> { ["Configuration"] = Configuration });

	partial void UpdateProject()
	{
		TakeProject();
		_project?.Update();
		if (_extensionChanged && _workbench.ViewModel.Notice.Length == 0)
			_workbench.ViewModel.Notice = $"{Path.GetFileNameWithoutExtension(_extensionProject)} changed: restart to take it up";
	}

	partial void CloseProject()
	{
		_extensionWatcher?.Dispose();
		_project?.Dispose();
	}

	private partial bool ProjectHasMusic() => _project?.Current is not null;

	// The project, once it has opened: or why it didn't, on the build pill
	private void TakeProject()
	{
		if (_opened || _opening is null || !_opening.IsCompleted)
			return;
		_opened = true;
		if (_opening.IsCompletedSuccessfully)
		{
			_project = new WorkbenchProject(_opening.Result, _workbench) { Extension = extension };
			return;
		}
		var problem = _opening.Exception?.GetBaseException().Message ?? "it was cancelled";
		_workbench.ReportBuild([$"{Name} couldn't be opened: {problem}"], TimeSpan.Zero);
		Console.Error.WriteLine($"[workbench] {projectPath} couldn't be opened: {problem}");
	}

	private FileSystemWatcher? _extensionWatcher;
	private volatile bool _extensionChanged;

	// The extension isn't reloaded: a change to its project is said beside the build pill, so its author knows to restart
	partial void WatchExtension()
	{
		if (extension is null || _extensionProject is null || Path.GetDirectoryName(_extensionProject) is not { } folder)
			return;
		_extensionWatcher = new FileSystemWatcher(folder) { IncludeSubdirectories = true, EnableRaisingEvents = true };
		void Changed(string path)
		{
			var relative = Path.GetRelativePath(folder, path);
			if (!relative.StartsWith("bin", StringComparison.OrdinalIgnoreCase) && !relative.StartsWith("obj", StringComparison.OrdinalIgnoreCase)
				&& Path.GetExtension(path) is ".cs" or ".csproj" or ".xaml" or ".props" or ".targets")
				_extensionChanged = true;
		}
		_extensionWatcher.Changed += (_, e) => Changed(e.FullPath);
		_extensionWatcher.Created += (_, e) => Changed(e.FullPath);
		_extensionWatcher.Deleted += (_, e) => Changed(e.FullPath);
		_extensionWatcher.Renamed += (_, e) => Changed(e.FullPath);
	}

	private bool _asking;

	// Open, open a folder and export, at the transport's end; and a project dropped on the window opens too
	partial void AddControls(WorkbenchView view)
	{
		if (!CanShowFileDialogs)
			return;
		var extras = view.TransportExtras;
		extras.Children.Add(Button("open", "Open a .lunelle file, or a music project's .csproj", () => _ = OpenFileAsync()));
		extras.Children.Add(Button("open folder", "Open a folder of music files", () => _ = OpenFolderAsync()));
		extras.Children.Add(Button("export", "Save this music as a .lunelle file, to keep or to hand on", () => _ = ExportAsync()));
	}

	protected override void OnFilesDropped(IReadOnlyList<string> paths)
	{
		if (paths.FirstOrDefault(Opens) is { } path)
			Reopen(path);
		else
			_workbench.ReportStatus("drop a .lunelle file, a .csproj or a folder of music to open it");
	}

	// What the studio opens: a project's file, its .csproj, or a folder
	private static bool Opens(string path) =>
		Directory.Exists(path)
		|| File.Exists(path) && Path.GetExtension(path).ToLowerInvariant() is MusicArchive.Extension or ".zip" or ".csproj";

	private async Task OpenFileAsync()
	{
		if (await AskAsync(new FileDialog
		{
			Kind = FileDialogKind.OpenFile,
			Filters = [new("Lunelle music", "lunelle;zip"), new("Music project with code", "csproj")],
			DefaultLocation = StudioHome.Folder,
		}) is { } path)
			Reopen(path);
	}

	private async Task OpenFolderAsync()
	{
		if (await AskAsync(new FileDialog { Kind = FileDialogKind.OpenFolder, DefaultLocation = StudioHome.Folder }) is { } path)
			Reopen(path);
	}

	private async Task ExportAsync()
	{
		var folder = filesAlone ? projectPath : Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
		var name = MusicArchive.FileName(_folder?.Current?.Name ?? Name);
		if (await AskAsync(new FileDialog
		{
			Kind = FileDialogKind.SaveFile,
			Filters = [new("Lunelle music", "lunelle")],
			DefaultLocation = Path.Combine(StudioHome.Folder, name),
		}) is not { } path)
			return;
		if (!path.EndsWith(MusicArchive.Extension, StringComparison.OrdinalIgnoreCase))
			path += MusicArchive.Extension;
		try
		{
			File.WriteAllBytes(path, MusicArchive.Pack(_files, folder));
			_workbench.ReportStatus($"saved {Path.GetFileName(path)}");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			_workbench.ReportStatus($"{Path.GetFileName(path)} couldn't be saved: {ex.Message}");
		}
	}

	// One dialog at a time; the path chosen, or null when none was
	private async Task<string?> AskAsync(FileDialog dialog)
	{
		if (_asking)
			return null;
		_asking = true;
		try
		{
			return await ShowFileDialogAsync(dialog) is [var path, ..] ? path : null;
		}
		finally
		{
			_asking = false;
		}
	}

	// Another project opens in a studio of its own, as a project's code and extension are loaded for the life of one;
	// this one closes, saving what it was at
	private void Reopen(string path)
	{
		if (Environment.ProcessPath is not { } studio || Path.GetFileNameWithoutExtension(studio).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
		{
			_workbench.ReportStatus("this studio can't start another: run it again with the project named");
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(studio) { ArgumentList = { Path.GetFullPath(path) }, UseShellExecute = false });
			Exit();
		}
		catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
		{
			_workbench.ReportStatus($"{Path.GetFileName(path)} couldn't be opened: {ex.Message}");
		}
	}
}
