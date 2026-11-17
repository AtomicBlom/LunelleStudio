namespace Loom.Audio.Workbench.Projects;

/// <summary>
/// A new music project to start from: samples/HelloSynth's music (a song and four patches as files, a kit, two cues and
/// the catalog in C#) with a .csproj of its own, which the workbench opens and plays as it is. It references the very
/// Loom assemblies and music generator the workbench runs (by path, from <c>loomDirectory</c>), so the two
/// always match.
/// </summary>
public static class ProjectTemplate
{
	/// <summary>
	/// Writes the project into <paramref name="folder"/> (made if need be, refused if it has a .csproj already) and returns
	/// the .csproj's path. Its name is the folder's, made an identifier for its namespace. Its music is
	/// <c>samples/HelloSynth</c>'s: a song, four patches as files, and its kit, cues and catalog in C#.
	/// </summary>
	public static string Create(string folder, string? loomDirectory = null)
	{
		folder = Path.GetFullPath(folder);
		loomDirectory = Path.GetFullPath(loomDirectory ?? AppContext.BaseDirectory);
		if (System.IO.Directory.Exists(folder) && System.IO.Directory.EnumerateFiles(folder, "*.csproj").Any())
			throw new InvalidOperationException($"{folder} already has a project.");
		var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var space = Identifier(name);
		System.IO.Directory.CreateDirectory(folder);

		// The sample's files, its namespace made this project's
		var assembly = typeof(ProjectTemplate).Assembly;
		foreach (var resource in assembly.GetManifestResourceNames().Where(r => r.StartsWith("template/", StringComparison.Ordinal)))
		{
			using var stream = assembly.GetManifestResourceStream(resource)!;
			using var reader = new StreamReader(stream);
			var text = reader.ReadToEnd();
			if (resource.EndsWith(".cs", StringComparison.Ordinal))
				text = text.Replace("HelloSynth", space, StringComparison.Ordinal);
			var path = Path.Combine(folder, resource["template/".Length..].Replace('/', Path.DirectorySeparatorChar));
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, text);
		}

		var loom = loomDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var project = Path.Combine(folder, name + ".csproj");
		File.WriteAllText(project, $"""
			<Project Sdk="Microsoft.NET.Sdk">

			  <!-- Music written in Loom's workbench: loom-workbench {name}.csproj opens it, plays it, and compiles it again on
			       every save. Its song and patches are files (Songs/, Patches/) that Loom's music generator makes into C# as it
			       builds; what a file can't say (the kit, the cues, the catalog) is C# beside them. Loom is referenced from the
			       workbench's own folder, so the two always match. -->

			  <PropertyGroup>
			    <TargetFramework>net{Environment.Version.Major}.0</TargetFramework>
			    <ImplicitUsings>enable</ImplicitUsings>
			    <Nullable>enable</Nullable>
			    <RootNamespace>{space}</RootNamespace>
			    <LoomDirectory>{loom}</LoomDirectory>
			  </PropertyGroup>

			  <ItemGroup>
			    <Reference Include="Loom.Audio" HintPath="$(LoomDirectory)Loom.Audio.dll" />
			    <Reference Include="Loom.Audio.Synth" HintPath="$(LoomDirectory)Loom.Audio.Synth.dll" />
			  </ItemGroup>

			  <ItemGroup>
			    <AdditionalFiles Include="Patches\*.patch.json;Songs\*.song.json" />
			    <CompilerVisibleProperty Include="RootNamespace" />
			    <CompilerVisibleProperty Include="ProjectDir" />
			    <Analyzer Include="$(LoomDirectory)Loom.Audio.Music.Generator.dll" />
			  </ItemGroup>

			</Project>

			""");
		File.WriteAllText(Path.Combine(folder, ".gitignore"), "bin/\nobj/\n.loom/\n");
		return project;
	}

	// A namespace from a folder's name: letters, digits and underscores, not starting with a digit
	private static string Identifier(string name)
	{
		var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
		var identifier = new string(chars);
		return identifier.Length == 0 || char.IsDigit(identifier[0]) ? "Music" + identifier : identifier;
	}
}
