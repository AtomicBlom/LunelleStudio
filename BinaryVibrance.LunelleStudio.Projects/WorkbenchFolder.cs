using System.Diagnostics;

using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>
/// A workbench over a music project of files alone (<see cref="MusicFolder"/>), with no C# and nothing built: the folder
/// read again a moment after any of its music files changes, off the frame, and each new read handed to the workbench
/// on its frame (swapped in at the next bar), with what it said of files left out.
/// </summary>
public sealed class WorkbenchFolder : IDisposable
{
	private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(150);

	private readonly FileSystemWatcher _watcher;
	private readonly Lock _lock = new();
	private (MusicFolder Folder, TimeSpan Took)? _pending;
	private CancellationTokenSource? _debounce;

	private WorkbenchFolder(string directory, Workbench workbench)
	{
		Directory = directory;
		Workbench = workbench;
		_watcher = new FileSystemWatcher(directory)
		{
			IncludeSubdirectories = true,
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
		};
		_watcher.Changed += (_, e) => Changed(e.FullPath);
		_watcher.Created += (_, e) => Changed(e.FullPath);
		_watcher.Deleted += (_, e) => Changed(e.FullPath);
		_watcher.Renamed += (_, e) => Changed(e.FullPath);
	}

	/// <summary>The folder, in full, which is watched.</summary>
	public string Directory { get; }

	/// <summary>The workbench it feeds.</summary>
	public Workbench Workbench { get; }

	/// <summary>The music as last taken up, or null before the first read is.</summary>
	public MusicFolder? Current { get; private set; }

	/// <summary>Reads <paramref name="directory"/> once and watches it; its first read waits for <see cref="Update"/>.</summary>
	/// <exception cref="DirectoryNotFoundException">There is no such folder.</exception>
	public static WorkbenchFolder Open(string directory, Workbench workbench)
	{
		ArgumentNullException.ThrowIfNull(directory);
		ArgumentNullException.ThrowIfNull(workbench);
		var folder = new WorkbenchFolder(Path.GetFullPath(directory), workbench);
		folder.Read();
		folder._watcher.EnableRaisingEvents = true;
		return folder;
	}

	/// <summary>Whether <paramref name="directory"/> is music alone: a folder with no .csproj in it.</summary>
	public static bool IsFilesAlone(string directory) =>
		System.IO.Directory.Exists(directory) && System.IO.Directory.GetFiles(directory, "*.csproj").Length == 0;

	/// <summary>Once a frame, on the workbench's thread: takes up a new read, if one is waiting.</summary>
	public void Update()
	{
		(MusicFolder Folder, TimeSpan Took)? read;
		lock (_lock)
		{
			read = _pending;
			_pending = null;
		}
		if (read is not { } taken)
			return;
		Current = taken.Folder;
		Workbench.UseCatalog(FolderCatalog.From(taken.Folder, Directory));
		Workbench.ReportRead(FolderCatalog.Problems(taken.Folder), taken.Took);
	}

	/// <summary>Stops watching the folder.</summary>
	public void Dispose()
	{
		_watcher.Dispose();
		_debounce?.Cancel();
	}

	// The whole folder read again; the newest read waits for the frame, any read before it not taken being dropped
	private void Read()
	{
		var clock = Stopwatch.StartNew();
		var folder = MusicFolder.FromDirectory(Directory);
		lock (_lock)
			_pending = (folder, clock.Elapsed);
	}

	// A music file, or the project file, changed: read again once the saves have settled (an editor writes a file in steps)
	private void Changed(string path)
	{
		var relative = Path.GetRelativePath(Directory, path).Replace('\\', '/');
		if (relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal)
			|| relative.StartsWith(".lunelle-studio/", StringComparison.Ordinal)
			|| !(relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !Path.HasExtension(relative)
				|| Path.GetExtension(relative) is ".wav" or ".ogg" or ".flac" or ".mp3"))
			return;
		var debounce = new CancellationTokenSource();
		Interlocked.Exchange(ref _debounce, debounce)?.Cancel();
		_ = Task.Delay(Settle, debounce.Token).ContinueWith(t =>
		{
			if (t.IsCanceled)
				return;
			try
			{
				Read();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// Mid-save, or a folder removed under it: the next change reads again
			}
		}, TaskScheduler.Default);
	}
}
