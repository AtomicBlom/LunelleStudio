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
}
