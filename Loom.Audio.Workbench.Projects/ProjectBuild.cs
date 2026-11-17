namespace Loom.Audio.Workbench.Projects;

/// <summary>One compile of a music project: its catalog when it built, its errors when it didn't, and how long it took.</summary>
public sealed class ProjectBuild
{
	private ProjectLoadContext? _context;

	internal ProjectBuild(WorkbenchCatalog? catalog, IReadOnlyList<string> errors, TimeSpan took, ProjectLoadContext? context)
	{
		Catalog = catalog;
		Errors = errors;
		Took = took;
		_context = context;
		Context = new WeakReference(context);
	}

	/// <summary>Whether it built and its catalog was found.</summary>
	public bool Succeeded => Catalog is not null;

	/// <summary>The music it built, or null.</summary>
	public WorkbenchCatalog? Catalog { get; }

	/// <summary>What went wrong, each as the compiler writes it: "Songs/Sketch.cs(12,5): error CS1002: ; expected".</summary>
	public IReadOnlyList<string> Errors { get; }

	/// <summary>How long it took, from the save to a loaded assembly.</summary>
	public TimeSpan Took { get; }

	/// <summary>Its load context, weakly: alive until the build after it has replaced it and nothing refers to it.</summary>
	public WeakReference Context { get; }

	/// <summary>Lets its assemblies go, once nothing playing refers to them: the context unloads when it is collected.</summary>
	public void Release()
	{
		_context?.Unload();
		_context = null;
	}

	/// <summary>The music's assembly when it built: held only while it plays, so its context can unload after.</summary>
	public System.Reflection.Assembly? Music { get; internal init; }
}
