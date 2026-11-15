[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Loom.Audio.Workbench.WorkbenchHotReload))]

namespace Loom.Audio.Workbench;

/// <summary>Told by the runtime when hot reload has changed code, in any assembly: the workbenches rebuild what they play.</summary>
internal static class WorkbenchHotReload
{
	internal static void ClearCache(Type[]? updatedTypes) { }

	// On a thread of the runtime's own: it only sets the flag each workbench reads in its Update
	internal static void UpdateApplication(Type[]? updatedTypes) => Workbench.CodeChanged();
}
