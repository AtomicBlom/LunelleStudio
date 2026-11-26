namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// Where the desktop studio keeps music of its own: <c>Documents\Lunelle Studio</c>. The project it opens when given
/// none is <c>Showcase</c> there, made from the showcase the first time; a <c>.lunelle</c> file opened is unpacked
/// beside it, as a folder to edit.
/// </summary>
internal static class StudioHome
{
	/// <summary>The folder, in the user's documents.</summary>
	public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lunelle Studio");

	/// <summary>The project to open given none: the last one opened if it is still there, else the showcase.</summary>
	public static string StartingProject(string? last, IMusicFiles files)
	{
		if (last is not null && (File.Exists(last) || Directory.Exists(last)))
			return last;
		var showcase = Path.Combine(Folder, "Showcase");
		StarterProject.SeedIfEmpty(files, showcase);
		return showcase;
	}

	/// <summary>
	/// A folder holding the project in the <c>.lunelle</c> file at <paramref name="path"/>, under <see cref="Folder"/>
	/// (<see cref="MusicArchive.UnpackBeside"/>).
	/// </summary>
	/// <exception cref="InvalidDataException">It isn't a project's file.</exception>
	public static string Unpack(string path, IMusicFiles files) =>
		MusicArchive.UnpackBeside(files, Folder, Path.GetFileNameWithoutExtension(path), File.ReadAllBytes(path));
}
