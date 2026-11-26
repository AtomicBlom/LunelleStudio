namespace BinaryVibrance.LunelleStudio.App.Web;

/// <summary>
/// The one music project the browser studio keeps, in <see cref="Folder"/> of its files: <c>samples/Showcase</c> the
/// first time (embedded), and whatever is imported in its place after.
/// </summary>
internal static class BrowserProject
{
	/// <summary>Where the project is, in the browser's files.</summary>
	public const string Folder = "/music";

	/// <summary>Writes the showcase's music when there is no project yet (<see cref="StarterProject"/>).</summary>
	public static void StartIfEmpty(BrowserFiles files) => StarterProject.SeedIfEmpty(files, Folder);

	/// <summary>
	/// The project replaced by a <c>.lunelle</c> file's (<see cref="MusicArchive"/>), what the workbench was doing with
	/// the old one too; returns how many files it holds.
	/// </summary>
	/// <exception cref="InvalidDataException">It isn't a project's file, and nothing is changed.</exception>
	public static int Import(BrowserFiles files, byte[] archive)
	{
		var incoming = MusicArchive.Unpack(archive);
		foreach (var path in files.List(Folder).ToList())
			files.Delete(Path.Combine(Folder, path));
		foreach (var (path, bytes) in incoming)
			files.WriteBytes(Path.Combine(Folder, path), bytes);
		return incoming.Count;
	}
}
