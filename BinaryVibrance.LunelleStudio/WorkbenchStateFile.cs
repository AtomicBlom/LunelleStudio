using System.Text.Json;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// Where a workbench keeps what it was doing, so a restart comes back playing where it was: a JSON file, beside the music
/// project (<c>.lunelle-studio/workbench.json</c>) or wherever an app chooses, on disk or in a host's own files. Written
/// only when what it holds has changed.
/// </summary>
public sealed class WorkbenchStateFile(string path, IMusicFiles? files = null)
{
	private readonly IMusicFiles _files = files ?? DiskFiles.Instance;
	private string _saved = "";

	/// <summary>The file, in full.</summary>
	public string Path { get; } = System.IO.Path.GetFullPath(path);

	/// <summary>The state kept beside a music project: <c>.lunelle-studio/workbench.json</c> in its folder.</summary>
	public static WorkbenchStateFile Beside(string projectPath, IMusicFiles? files = null) =>
		new(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(projectPath))!, ".lunelle-studio", "workbench.json"), files);

	/// <summary>What was saved, or null when nothing was or it can't be read.</summary>
	public WorkbenchState? Load()
	{
		try
		{
			if (!_files.Exists(Path))
				return null;
			_saved = _files.ReadText(Path);
			return JsonSerializer.Deserialize(_saved, WorkbenchStateJson.Default.WorkbenchState);
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	/// <summary>Saves <paramref name="state"/>, unless it is what the file already holds.</summary>
	public void Save(WorkbenchState state)
	{
		var json = JsonSerializer.Serialize(state, WorkbenchStateJson.Default.WorkbenchState);
		if (json == _saved)
			return;
		try
		{
			_files.WriteText(Path, json);
			_saved = json;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine($"[workbench] couldn't save {Path}: {ex.Message}");
		}
	}
}
