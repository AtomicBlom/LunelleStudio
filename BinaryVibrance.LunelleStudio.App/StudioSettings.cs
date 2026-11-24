using System.Text.Json;
using System.Text.Json.Nodes;

namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// What the player of the studio chose for every project alike: the theme. Kept in
/// <c>%LOCALAPPDATA%\BinaryVibrance\LunelleStudio\settings.json</c>, apart from what each project's workbench was doing
/// (<see cref="WorkbenchStateFile"/>, beside the project).
/// </summary>
internal sealed class StudioSettings
{
	private static readonly string FilePath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BinaryVibrance", "LunelleStudio", "settings.json");

	/// <summary>The theme's name, or null for the one a workbench starts in.</summary>
	public string? Theme { get; set; }

	/// <summary>What was saved, or nothing chosen when nothing was or it can't be read.</summary>
	public static StudioSettings Load()
	{
		try
		{
			if (File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject json)
				return new StudioSettings { Theme = json["theme"]?.GetValue<string>() };
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
		{
			Console.Error.WriteLine($"[studio] {FilePath} couldn't be read: {ex.Message}");
		}
		return new StudioSettings();
	}

	/// <summary>Writes the settings; a failure is reported and otherwise ignored, as the choice still holds until the app closes.</summary>
	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			var json = new JsonObject { ["theme"] = Theme };
			File.WriteAllText(FilePath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine($"[studio] {FilePath} couldn't be written: {ex.Message}");
		}
	}
}
