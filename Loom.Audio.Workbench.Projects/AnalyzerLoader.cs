using System.Reflection;

using Microsoft.CodeAnalysis;

namespace Loom.Audio.Workbench.Projects;

/// <summary>
/// Loads a project's analyzers and source generators into this process, once each. One built against a newer Roslyn than
/// this process has fails to load and is left out: the SDK's own generators are, and a music project doesn't use them.
/// </summary>
internal sealed class AnalyzerLoader : IAnalyzerAssemblyLoader
{
	public static AnalyzerLoader Instance { get; } = new();

	public void AddDependencyLocation(string fullPath)
	{
	}

	public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
}
