using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>
/// A build of a project shares what the host has already loaded (Kaldra above all) rather than loading it again, so each
/// such reference must be the very build the host runs: a project built against another Kaldra, or against a reference
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
		var versions = new List<string>();
		var rebuilt = new List<string>();
		foreach (var (name, reference) in references)
		{
			if (!loaded.TryGetValue(name, out var assembly))
				continue;
			var built = ProjectLoadContext.Implementation(reference);
			// A library built for several frameworks (Vysara) gives a project on another framework a build of its own, whose
			// id differs by design: only builds for the same framework are compared
			if (ModuleId(built) is not { } mine || mine.Id == assembly.ManifestModule.ModuleVersionId
				|| (mine.Framework is not null && mine.Framework != assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName))
				continue;
			var theirs = Version(built);
			var ours = Version(assembly);
			if (theirs is not null && ours is not null && theirs != ours)
				versions.Add($"This project uses {name} {theirs}; this studio is {ours}.");
			else
				rebuilt.Add($"{name}: the project's is {built}, the studio runs {assembly.Location}");
		}
		if (versions.Count > 0)
			return string.Join('\n', versions) + "\nA project with code must be built against the very Kaldra and Lunelle the studio runs, "
				+ "which it shares with the project rather than loading again. Open the project in the studio of its version, "
				+ "or update its references to this studio's.";
		return rebuilt.Count == 0
			? null
			: "The project builds against assemblies other than the ones this studio runs, which it shares with the project "
				+ "rather than loading again. A reference rebuilt since the studio started needs the studio restarted.\n"
				+ string.Join('\n', rebuilt);
	}

	/// <summary>An assembly's informational version (as its package was versioned), without a source revision after a <c>+</c>.</summary>
	internal static string? Version(string path)
	{
		try
		{
			return Trim(System.Diagnostics.FileVersionInfo.GetVersionInfo(path).ProductVersion);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private static string? Version(Assembly assembly) =>
		Trim(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

	private static string? Trim(string? version) =>
		string.IsNullOrEmpty(version) ? null : version.IndexOf('+') is var plus and > 0 ? version[..plus] : version;

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
