using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>
/// A new music project to start from: samples/HelloSynth's music (format 2: a manifest, a song, its patches, a kit and
/// two cues as files; the catalog in C#) with a .csproj of its own, which the workbench opens and plays as it is. It takes
/// Lunelle as a package (BinaryVibrance.Lunelle, with Sirrow under it, the music generator as its analyzer, and Lunelle's
/// own sounds) at the very version the workbench runs, so the two always match: the studio ships the packages beside it
/// (packages\), and copies them into a feed of the user's own that outlives any one install, which the project's
/// nuget.config names beside nuget.org. Or the same music as files alone, with no C# (<see cref="CreateData"/>).
/// </summary>
public static class ProjectTemplate
{
	/// <summary>
	/// Writes the project into <paramref name="folder"/> (made if need be, refused if it has a .csproj already) and returns
	/// the .csproj's path. Its name is the folder's, made an identifier for its namespace. Its music is
	/// <c>samples/HelloSynth</c>'s: a song, its patches and its kit as files, and its cues, macro and catalog in C#.
	/// </summary>
	public static string Create(string folder, string? studioDirectory = null, string? feed = null)
	{
		folder = Path.GetFullPath(folder);
		studioDirectory = Path.GetFullPath(studioDirectory ?? AppContext.BaseDirectory);
		if (System.IO.Directory.Exists(folder) && System.IO.Directory.EnumerateFiles(folder, "*.csproj").Any())
			throw new InvalidOperationException($"{folder} already has a project.");
		feed = Path.GetFullPath(feed ?? DefaultFeed);
		var version = ShipPackages(Path.Combine(studioDirectory, "packages"), feed);
		var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var space = Identifier(name);
		System.IO.Directory.CreateDirectory(folder);

		Write(folder, name, space, code: true);

		var project = Path.Combine(folder, name + ".csproj");
		File.WriteAllText(project, $"""
			<Project Sdk="Microsoft.NET.Sdk">

			  <!-- Music written in LunelleStudio: lunelle-studio {name}.csproj opens it, plays it, and compiles it again on
			       every save. Its music is files (lunelle.meta.json, Songs/, Patches/, Kits/, Cues/) that Lunelle's music generator
			       makes into C# as it builds; what a file can't say (the catalog) is C# beside them. Lunelle's package carries the
			       generator, the props that hand it the music, and Lunelle's own sounds; its version is the studio's, so the two
			       match. -->

			  <PropertyGroup>
			    <TargetFramework>net{Environment.Version.Major}.0</TargetFramework>
			    <ImplicitUsings>enable</ImplicitUsings>
			    <Nullable>enable</Nullable>
			    <RootNamespace>{space}</RootNamespace>
			  </PropertyGroup>

			  <ItemGroup>
			    <PackageReference Include="BinaryVibrance.Lunelle" Version="{version}" />
			  </ItemGroup>

			</Project>

			""");
		File.WriteAllText(Path.Combine(folder, "nuget.config"), $"""
			<?xml version="1.0" encoding="utf-8"?>
			<configuration>
			  <!-- Lunelle's packages as LunelleStudio ships them, and nuget.org for the rest (and Lunelle's, once released there) -->
			  <packageSources>
			    <clear />
			    <add key="lunelle-studio" value="{System.Security.SecurityElement.Escape(feed)}" />
			    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
			  </packageSources>
			</configuration>

			""");
		File.WriteAllText(Path.Combine(folder, ".gitignore"), "bin/\nobj/\n.lunelle-studio/\n");
		return project;
	}

	/// <summary>The feed the studio's packages are copied into: the user's own, kept across installs and updates.</summary>
	public static string DefaultFeed => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BinaryVibrance", "LunelleStudio", "packages");

	// The studio's packages copied into the feed, and the version of Lunelle's. A package already there with other bytes
	// under the same version (a build from source, changed) replaces it, and NuGet's extracted copy of that version is
	// cleared, so a restore takes the new one
	private static string ShipPackages(string shipped, string feed)
	{
		var packages = System.IO.Directory.Exists(shipped) ? System.IO.Directory.GetFiles(shipped, "*.nupkg") : [];
		var lunelle = packages.Select(Path.GetFileNameWithoutExtension).FirstOrDefault(n => n!.StartsWith(LunellePackage + ".", StringComparison.OrdinalIgnoreCase)
			&& char.IsDigit(n[(LunellePackage.Length + 1)..][0]));
		if (lunelle is null)
			throw new InvalidOperationException($"The studio has no {LunellePackage} package beside it ({shipped}), for a project with code to take Lunelle from.");
		System.IO.Directory.CreateDirectory(feed);
		foreach (var package in packages)
		{
			var target = Path.Combine(feed, Path.GetFileName(package));
			if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(File.ReadAllBytes(package)))
				continue;
			var replacing = File.Exists(target);
			File.Copy(package, target, overwrite: true);
			if (replacing && PackageAt(Path.GetFileNameWithoutExtension(package)) is { } extracted && System.IO.Directory.Exists(extracted))
				System.IO.Directory.Delete(extracted, recursive: true);
		}
		return lunelle[(LunellePackage.Length + 1)..];
	}

	private const string LunellePackage = "BinaryVibrance.Lunelle";

	// Where NuGet extracts a package (its id and version, as its file is named), or null for a name that isn't one
	private static string? PackageAt(string fileName)
	{
		var at = fileName.Split('.').Select((part, i) => (part, i)).FirstOrDefault(p => p.part.Length > 0 && char.IsDigit(p.part[0]) && p.i > 0);
		if (at.i == 0)
			return null;
		var parts = fileName.Split('.');
		var id = string.Join('.', parts[..at.i]);
		var version = string.Join('.', parts[at.i..]);
		var root = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } custom ? custom
			: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
		return Path.Combine(root, id.ToLowerInvariant(), version.ToLowerInvariant());
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
