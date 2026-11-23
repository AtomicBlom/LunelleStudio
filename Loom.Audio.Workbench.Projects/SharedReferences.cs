using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Loom.Audio.Workbench.Projects;

/// <summary>
/// A build of a project shares what the host has already loaded (Loom above all) rather than loading it again, so each
/// such reference must be the very build the host runs: a project built against another Loom, or against a reference
/// rebuilt since the host started, would find methods missing or changed. Each is compared by its module's version id.
/// </summary>
internal static class SharedReferences
{
	/// <summary>Why the project can't be loaded beside the host, or null when every shared reference is the host's.</summary>
	public static string? Mismatch(IReadOnlyDictionary<string, string> references)
	{
		var runtime = RuntimeEnvironment.GetRuntimeDirectory();
		var loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
		foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
			if (!assembly.IsDynamic && assembly.Location.Length > 0 && assembly.GetName().Name is { } name
				&& !assembly.Location.StartsWith(runtime, StringComparison.OrdinalIgnoreCase))
				loaded.TryAdd(name, assembly);
		var problems = new List<string>();
		foreach (var (name, reference) in references)
		{
			if (!loaded.TryGetValue(name, out var assembly))
				continue;
			var built = ProjectLoadContext.Implementation(reference);
			// A library built for several frameworks (Vysara) gives a project on another framework a build of its own, whose
			// id differs by design: only builds for the same framework are compared
			if (ModuleId(built) is { } mine && mine.Id != assembly.ManifestModule.ModuleVersionId
				&& (mine.Framework is null || mine.Framework == assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName))
				problems.Add($"{name}: the project's is {built}, the workbench runs {assembly.Location}");
		}
		return problems.Count == 0
			? null
			: "The project builds against assemblies other than the ones this workbench runs, which it shares with the project "
				+ "rather than loading again. A project built against another Loom can't be opened by this one; a reference "
				+ "rebuilt since the workbench started needs it restarted.\n" + string.Join("\n", problems);
	}

	// The module's version id and the framework it was built for, or null for a reference assembly (which has no code to
	// compare) or one that can't be read
	private static (Guid Id, string? Framework)? ModuleId(string path)
	{
		try
		{
			using var stream = File.OpenRead(path);
			using var pe = new PEReader(stream);
			if (!pe.HasMetadata)
				return null;
			var metadata = pe.GetMetadataReader();
			string? framework = null;
			foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
			{
				var attribute = metadata.GetCustomAttribute(handle);
				if (attribute.Constructor.Kind != HandleKind.MemberReference
					|| metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent is not { Kind: HandleKind.TypeReference } parent)
					continue;
				var type = metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Name);
				if (type == "ReferenceAssemblyAttribute")
					return null;
				if (type == "TargetFrameworkAttribute")
				{
					// A custom attribute's value: a prolog, then its one string argument
					var value = metadata.GetBlobReader(attribute.Value);
					value.ReadUInt16();
					framework = value.ReadSerializedString();
				}
			}
			return (metadata.GetGuid(metadata.GetModuleDefinition().Mvid), framework);
		}
		catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
		{
			return null;
		}
	}
}
