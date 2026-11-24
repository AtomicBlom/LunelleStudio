using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A music project of files alone (<see cref="MusicFolder"/>) as what the workbench offers: its songs built from their
/// files with no compile, as a project with code builds a song with a file, and nothing that is code, so the workbench
/// shows no way to move a piece into C#. Needs no Roslyn and no MSBuild, so an app that can't run them (the browser)
/// can still offer the workbench.
/// </summary>
public static class FolderCatalog
{
	/// <summary>
	/// <paramref name="folder"/>'s music, its files' paths taken from <paramref name="root"/> (where the folder's paths are
	/// relative to), which the workbench writes its edits back to.
	/// </summary>
	public static WorkbenchCatalog From(MusicFolder folder, string root)
	{
		ArgumentNullException.ThrowIfNull(folder);
		ArgumentNullException.ThrowIfNull(root);
		string Full(string path) => Path.GetFullPath(Path.Combine(root, path));
		return new WorkbenchCatalog
		{
			Songs = folder.Songs,
			Patches = folder.Patches,
			Kits = folder.Kits,
			Cues = folder.Cues,
			// As a project with code would name it, so a patch's code reads the same in either
			MacroName = id => folder.MacroName(id) is { } name ? $"Macros.{PatchCode.Pascal(name)}" : null,
			SongSource = name => folder.SongPath(name) is { } path ? new SongSource(Full(path), folder.Bindings) : null,
			PatchFile = name => folder.PatchPath(name) is { } path ? Full(path) : null,
		};
	}

	/// <summary>
	/// What the workbench says of <paramref name="folder"/> as it reads it: each file left out, and each song's code that
	/// plays nothing here, a line each.
	/// </summary>
	public static IReadOnlyList<string> Problems(MusicFolder folder)
	{
		ArgumentNullException.ThrowIfNull(folder);
		return
		[
			.. folder.Problems,
			.. folder.Songs.Select(s => (s.Name, Code: folder.CodeIn(s.Name)))
				.Where(s => s.Code.Count > 0)
				.Select(s => $"{s.Name}: its code plays nothing without C# ({string.Join(", ", s.Code)})"),
		];
	}
}
