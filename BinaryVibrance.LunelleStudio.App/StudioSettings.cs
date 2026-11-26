using System.Text.Json;
using System.Text.Json.Nodes;

namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// What the player of the studio chose for every project alike: the theme, and how the workbench is laid out. Kept in
/// <c>%LOCALAPPDATA%\BinaryVibrance\LunelleStudio\settings.json</c> (in the browser, its storage under that path), apart
/// from what each project opens at (<see cref="WorkbenchStateFile"/>, beside the project).
/// </summary>
internal sealed class StudioSettings
{
	private static readonly string FilePath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BinaryVibrance", "LunelleStudio", "settings.json");

	private string _saved = "";

	/// <summary>The theme's name, or null for the one a workbench starts in.</summary>
	public string? Theme { get; set; }

	/// <summary>How the workbench is laid out.</summary>
	public WorkbenchPreferences Workbench { get; set; } = new();

	/// <summary>The project opened last, to open again when the studio starts with none named.</summary>
	public string? LastProject { get; set; }

	/// <summary>What was saved, or nothing chosen when nothing was or it can't be read.</summary>
	public static StudioSettings Load(IMusicFiles files)
	{
		var settings = new StudioSettings();
		try
		{
			if (files.Exists(FilePath) && JsonNode.Parse(files.ReadText(FilePath)) is JsonObject json)
			{
				settings.Theme = json["theme"]?.GetValue<string>();
				settings.LastProject = json["lastProject"]?.GetValue<string>();
				if (json["workbench"] is JsonObject bench)
				{
					var defaults = new WorkbenchPreferences();
					settings.Workbench = new WorkbenchPreferences
					{
						LibraryPinned = bench["libraryPinned"]?.GetValue<bool>() ?? defaults.LibraryPinned,
						PartPanelHeight = bench["partPanelHeight"]?.GetValue<float>() ?? defaults.PartPanelHeight,
						Octave = bench["octave"]?.GetValue<int>() ?? defaults.Octave,
						Velocity = bench["velocity"]?.GetValue<float>() ?? defaults.Velocity,
					};
				}
				settings._saved = settings.ToJson();
			}
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
		{
			Console.Error.WriteLine($"[studio] {FilePath} couldn't be read: {ex.Message}");
		}
		return settings;
	}

	/// <summary>
	/// Writes the settings, unless they are what was last written; a failure is reported and otherwise ignored, as the
	/// choice still holds until the app closes.
	/// </summary>
	public void Save(IMusicFiles files)
	{
		var json = ToJson();
		if (json == _saved)
			return;
		try
		{
			files.WriteText(FilePath, json);
			_saved = json;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine($"[studio] {FilePath} couldn't be written: {ex.Message}");
		}
	}

	private string ToJson() => new JsonObject
	{
		["theme"] = Theme,
		["lastProject"] = LastProject,
		["workbench"] = new JsonObject
		{
			["libraryPinned"] = Workbench.LibraryPinned,
			["partPanelHeight"] = Workbench.PartPanelHeight,
			["octave"] = Workbench.Octave,
			["velocity"] = Workbench.Velocity,
		},
	}.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
