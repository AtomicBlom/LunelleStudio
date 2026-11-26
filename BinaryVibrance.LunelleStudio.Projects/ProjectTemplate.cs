using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>
/// A new music project to start from: samples/HelloSynth's music (format 2: a manifest, a song, its patches, a kit and
/// two cues as files; the catalog in C#) with a .csproj of its own, which the workbench opens and plays as it is. It references
/// the very Sirrow and Lunelle assemblies and music generator the workbench runs (by path, from <c>studioDirectory</c>),
/// so the two always match. Or the same music as files alone, with no C# (<see cref="CreateData"/>).
/// </summary>
public static class ProjectTemplate
{
	/// <summary>
	/// Writes the project into <paramref name="folder"/> (made if need be, refused if it has a .csproj already) and returns
	/// the .csproj's path. Its name is the folder's, made an identifier for its namespace. Its music is
	/// <c>samples/HelloSynth</c>'s: a song, its patches and its kit as files, and its cues, macro and catalog in C#.
	/// </summary>
	public static string Create(string folder, string? studioDirectory = null)
	{
		folder = Path.GetFullPath(folder);
		studioDirectory = Path.GetFullPath(studioDirectory ?? AppContext.BaseDirectory);
		if (System.IO.Directory.Exists(folder) && System.IO.Directory.EnumerateFiles(folder, "*.csproj").Any())
			throw new InvalidOperationException($"{folder} already has a project.");
		var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var space = Identifier(name);
		System.IO.Directory.CreateDirectory(folder);

		Write(folder, name, space, code: true);

		var studio = studioDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var project = Path.Combine(folder, name + ".csproj");
		File.WriteAllText(project, $"""
			<Project Sdk="Microsoft.NET.Sdk">

			  <!-- Music written in LunelleStudio: lunelle-studio {name}.csproj opens it, plays it, and compiles it again on
			       every save. Its music is files (lunelle.meta.json, Songs/, Patches/, Kits/, Cues/) that Lunelle's music generator
			       makes into C# as it builds; what a file can't say (the catalog) is C# beside them. Sirrow and Lunelle are
			       referenced from the workbench's own folder, so the two always match. -->

			  <PropertyGroup>
			    <TargetFramework>net{Environment.Version.Major}.0</TargetFramework>
			    <ImplicitUsings>enable</ImplicitUsings>
			    <Nullable>enable</Nullable>
			    <RootNamespace>{space}</RootNamespace>
			    <StudioDirectory>{studio}</StudioDirectory>
			  </PropertyGroup>

			  <ItemGroup>
			    <Reference Include="BinaryVibrance.Sirrow" HintPath="$(StudioDirectory)BinaryVibrance.Sirrow.dll" />
			    <Reference Include="BinaryVibrance.Lunelle" HintPath="$(StudioDirectory)BinaryVibrance.Lunelle.dll" />
			  </ItemGroup>

			  <!-- The music (lunelle.meta.json and its folders, and Lunelle's own sounds beside the studio) handed to the generator -->
			  <PropertyGroup>
			    <LunelleLibraryDir>$(StudioDirectory)Library\</LunelleLibraryDir>
			  </PropertyGroup>
			  <Import Project="$(StudioDirectory)lunelle-music.props" />
			  <ItemGroup>
			    <Analyzer Include="$(StudioDirectory)BinaryVibrance.Lunelle.Music.Generator.dll" />
			  </ItemGroup>

			</Project>

			""");
		File.WriteAllText(Path.Combine(folder, ".gitignore"), "bin/\nobj/\n.lunelle-studio/\n");
		return project;
	}

	/// <summary>
	/// Writes a project of music files alone into <paramref name="folder"/> (made if need be, refused if it has a project
	/// of either kind already) and returns the folder: <c>samples/HelloSynth</c>'s song, patches and kit as files, and its
	/// cues, with its macro in <c>lunelle.meta.json</c>, which the workbench reads with no C# (Lunelle's <c>MusicFolder</c>).
	/// </summary>
	public static string CreateData(string folder)
	{
		folder = Path.GetFullPath(folder);
		if (System.IO.Directory.Exists(folder) && (System.IO.Directory.EnumerateFiles(folder, "*.csproj").Any()
			|| File.Exists(Path.Combine(folder, MusicFolder.ProjectFileName))))
			throw new InvalidOperationException($"{folder} already has a project.");
		var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		System.IO.Directory.CreateDirectory(folder);
		Write(folder, name, Identifier(name), code: false);
		File.WriteAllText(Path.Combine(folder, ".gitignore"), ".lunelle-studio/\n");
		return folder;
	}

	// The sample's files, its namespace made this project's and its manifest named for it; its C# only if wanted
	private static void Write(string folder, string name, string space, bool code)
	{
		var assembly = typeof(ProjectTemplate).Assembly;
		foreach (var resource in assembly.GetManifestResourceNames().Where(r => r.StartsWith("template/", StringComparison.Ordinal)))
		{
			var cs = resource.EndsWith(".cs", StringComparison.Ordinal);
			if (cs && !code)
				continue;
			using var stream = assembly.GetManifestResourceStream(resource)!;
			using var reader = new StreamReader(stream);
			var text = reader.ReadToEnd();
			if (cs || resource.EndsWith(".cues.json", StringComparison.Ordinal))
				text = text.Replace("HelloSynth", space, StringComparison.Ordinal);
			else if (resource == "template/" + MusicFolder.ProjectFileName)
				text = text.Replace("\"name\": \"hello-synth\"", $"\"name\": \"{Slug(name)}\"", StringComparison.Ordinal)
					.Replace("\"title\": \"HelloSynth\"", $"\"title\": {System.Text.Json.JsonSerializer.Serialize(name)}", StringComparison.Ordinal);
			var path = Path.Combine(folder, resource["template/".Length..].Replace('/', Path.DirectorySeparatorChar));
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, text);
		}
	}

	// An id from a folder's name, as a manifest names a project: lowercase letters, digits and '-', starting with a letter
	private static string Slug(string name)
	{
		var slug = new string([.. name.ToLowerInvariant().Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-')]).Trim('-');
		while (slug.Contains("--", StringComparison.Ordinal))
			slug = slug.Replace("--", "-", StringComparison.Ordinal);
		return slug.Length == 0 || !char.IsLetter(slug[0]) ? "music-" + slug : slug;
	}

	// A namespace from a folder's name: letters, digits and underscores, not starting with a digit
	private static string Identifier(string name)
	{
		var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
		var identifier = new string(chars);
		return identifier.Length == 0 || char.IsDigit(identifier[0]) ? "Music" + identifier : identifier;
	}
}
