namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>
/// A workbench reloading a music project: each frame it takes the project's newest build, hands its catalog to the
/// workbench (swapped in at the next bar) or shows its errors, and lets the build before it go.
/// </summary>
public sealed class WorkbenchProject(MusicProject project, Workbench workbench) : IDisposable
{
	private ProjectBuild? _current;

	/// <summary>The project.</summary>
	public MusicProject Project { get; } = project ?? throw new ArgumentNullException(nameof(project));

	/// <summary>The workbench it feeds.</summary>
	public Workbench Workbench { get; } = workbench ?? throw new ArgumentNullException(nameof(workbench));

	/// <summary>The build playing, or null before the first that built.</summary>
	public ProjectBuild? Current => _current;

	/// <summary>Once a frame, on the workbench's thread: takes up a new build, if one is waiting.</summary>
	public void Update()
	{
		if (Project.TakeBuild() is not { } build)
			return;
		// The catalog first: taking it up rebuilds the song, which notes its own time, and the compile's line goes over it
		if (build.Succeeded)
		{
			var catalog = build.Catalog!;
			_code.Types = [.. catalog.Songs.Select(s => catalog.SongSource(s.Name)?.Type).Append(catalog.PatchesType).OfType<string>()];
			Workbench.Code = _code;
			Workbench.UseCatalog(catalog);
			_current?.Release();
			_current = build;
			if (build.Music is { } music)
				Extension?.MusicBuilt(music);
		}
		Workbench.ReportBuild(build.Errors, build.Took);
	}

	/// <summary>Stops watching the project.</summary>
	public void Dispose() => Project.Dispose();

	// The project's C#, which the workbench ejects pieces into and brings them back from
	private readonly MusicCode _code = new(() => project.SourceFiles);

	/// <summary>The project's workbench extension, told of each new build; null for none.</summary>
	public IWorkbenchExtension? Extension { get; init; }
}
