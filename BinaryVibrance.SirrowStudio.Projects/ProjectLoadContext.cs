using System.Reflection;
using System.Runtime.Loader;

namespace BinaryVibrance.SirrowStudio.Projects;

/// <summary>
/// Where one build of a music project is loaded: collectible, so the build before it can be let go. Anything the host
/// already has (Loom above all, whose types the workbench and the music must share) comes from the host's context; the
/// rest of the project's references load here from their paths.
/// </summary>
internal sealed class ProjectLoadContext(string name, IReadOnlyDictionary<string, string> references)
	: AssemblyLoadContext(name, isCollectible: true)
{
	protected override Assembly? Load(AssemblyName assemblyName)
	{
		// What the host can resolve (the framework, Loom, any reference the host was built with) it shares; a framework
		// assembly not loaded yet must come from the runtime, since the project references only its reference assembly
		try
		{
			return Default.LoadFromAssemblyName(assemblyName);
		}
		catch (FileNotFoundException)
		{
		}
		return assemblyName.Name is { } simple && references.TryGetValue(simple, out var path) ? LoadFromAssemblyPath(Implementation(path)) : null;
	}

	/// <summary>
	/// The assembly that runs for a reference: a project reference is compiled against its reference assembly
	/// (<c>obj/…/ref/X.dll</c> or <c>refint/X.dll</c>), which has no code, beside the assembly built with it.
	/// </summary>
	internal static string Implementation(string reference)
	{
		var folder = Path.GetDirectoryName(reference)!;
		var built = Path.Combine(Path.GetDirectoryName(folder)!, Path.GetFileName(reference));
		return Path.GetFileName(folder) is "ref" or "refint" && File.Exists(built) ? built : reference;
	}
}
