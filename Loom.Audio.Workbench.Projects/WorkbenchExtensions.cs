using System.Reflection;
using System.Runtime.Loader;

namespace Loom.Audio.Workbench.Projects;

/// <summary>
/// A music project's workbench extension found, built and loaded: named by the project's <c>LoomWorkbenchExtension</c>
/// property (a .csproj, relative to the project) or given outright, built in the workbench's configuration, and loaded
/// once into the default context, where the music's builds share what it references as they share Loom.
/// </summary>
public static class WorkbenchExtensions
{
	/// <summary>The MSBuild property a music project names its extension's project by.</summary>
	public const string Property = "LoomWorkbenchExtension";

	/// <summary>The extension's .csproj that <paramref name="musicProject"/> names, in full, or null when it names none.</summary>
	public static async Task<string?> NamedByAsync(string musicProject, CancellationToken cancellation = default)
	{
		var named = await DesignTimeBuild.PropertyAsync(musicProject, Property, cancellation);
		return named is null ? null : Path.GetFullPath(named, Path.GetDirectoryName(Path.GetFullPath(musicProject))!);
	}

	/// <summary>Builds the extension's project and loads it.</summary>
	/// <exception cref="InvalidOperationException">It didn't build, or names no extension.</exception>
	public static async Task<IWorkbenchExtension> BuildAndLoadAsync(string extensionProject, string configuration, CancellationToken cancellation = default)
	{
		var assembly = await DesignTimeBuild.BuildAsync(extensionProject, configuration, cancellation);
		return Load(assembly);
	}

	/// <summary>Loads the extension built at <paramref name="assemblyPath"/>, what it references from beside it.</summary>
	/// <exception cref="InvalidOperationException">It names no extension, or names one that isn't.</exception>
	public static IWorkbenchExtension Load(string assemblyPath)
	{
		var folder = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;
		// Built against another build of what the host shares (Loom, the workbench), it would find methods missing
		var beside = Directory.GetFiles(folder, "*.dll").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => f, StringComparer.OrdinalIgnoreCase);
		if (SharedReferences.Mismatch(beside) is { } mismatch)
			throw new InvalidOperationException($"{Path.GetFileName(assemblyPath)}: {mismatch}");
		// What the host has (Loom, SUIsei) comes from the host; the extension's own references (a game's libraries) from
		// its folder
		AssemblyLoadContext.Default.Resolving += (context, name) =>
			Path.Combine(folder, name.Name + ".dll") is var path && File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
		return Create(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath)));
	}

	/// <summary>The extension <paramref name="assembly"/> names, made.</summary>
	/// <exception cref="InvalidOperationException">It names no extension, or names one that isn't.</exception>
	public static IWorkbenchExtension Create(Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(assembly);
		var attribute = assembly.GetCustomAttribute<WorkbenchExtensionAttribute>()
			?? throw new InvalidOperationException($"{assembly.GetName().Name} names no extension: add [assembly: WorkbenchExtension(typeof(...))] to it.");
		return Activator.CreateInstance(attribute.Extension) as IWorkbenchExtension
			?? throw new InvalidOperationException($"{attribute.Extension.FullName} isn't an IWorkbenchExtension.");
	}
}
