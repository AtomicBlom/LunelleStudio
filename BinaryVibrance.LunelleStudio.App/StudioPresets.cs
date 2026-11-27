namespace BinaryVibrance.LunelleStudio.App;

// The sounds the studio offers a project of files alone: Lunelle's own library (BuiltInLibrary), which a project
// references as lunelle:<id>
internal static class StudioPresets
{
	public static PresetLibrary Load()
	{
		var library = PresetLibrary.Lunelle;
		foreach (var problem in library.Problems)
			Console.Error.WriteLine($"[studio] Lunelle's library: {problem}");
		return library;
	}
}
