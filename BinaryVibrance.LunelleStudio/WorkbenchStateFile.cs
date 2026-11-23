using System.Text.Json;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// Where a workbench keeps what it was doing, so a restart comes back playing where it was: a JSON file, beside the music
/// project (<c>.lunelle-studio/workbench.json</c>) or wherever an app chooses. Written only when what it holds has changed.
/// </summary>
public sealed class WorkbenchStateFile(string path)
{
	private string _saved = "";

	/// <summary>The file, in full.</summary>
	public string Path { get; } = System.IO.Path.GetFullPath(path);

	/// <summary>The state kept beside a music project: <c>.lunelle-studio/workbench.json</c> in its folder.</summary>
	public static WorkbenchStateFile Beside(string projectPath) =>
		new(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(projectPath))!, ".lunelle-studio", "workbench.json"));

	/// <summary>What was saved, or null when nothing was or it can't be read.</summary>
	public WorkbenchState? Load()
	{
		try
		{
			if (!File.Exists(Path))
				return null;
			_saved = File.ReadAllText(Path);
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
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
			File.WriteAllText(Path, json);
			_saved = json;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine($"[workbench] couldn't save {Path}: {ex.Message}");
		}
	}
}
