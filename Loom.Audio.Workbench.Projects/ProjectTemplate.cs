namespace Loom.Audio.Workbench.Projects;

/// <summary>
/// A new music project to start from: a .csproj, a catalog, a cue, two patches and a song, which the workbench opens and
/// plays as it is. It references the very Loom assemblies the workbench runs (by path, from <c>loomDirectory</c>), so
/// the two always match.
/// </summary>
public static class ProjectTemplate
{
	/// <summary>
	/// Writes the project into <paramref name="folder"/> (made if need be, refused if it has a .csproj already) and returns
	/// the .csproj's path. Its name is the folder's, made an identifier for its namespace.
	/// </summary>
	public static string Create(string folder, string? loomDirectory = null)
	{
		folder = Path.GetFullPath(folder);
		loomDirectory = Path.GetFullPath(loomDirectory ?? AppContext.BaseDirectory);
		if (System.IO.Directory.Exists(folder) && System.IO.Directory.EnumerateFiles(folder, "*.csproj").Any())
			throw new InvalidOperationException($"{folder} already has a project.");
		var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var space = Identifier(name);
		System.IO.Directory.CreateDirectory(Path.Combine(folder, "Songs"));
		var project = Path.Combine(folder, name + ".csproj");
		File.WriteAllText(project, $"""
			<Project Sdk="Microsoft.NET.Sdk">

			  <!-- Music written in Loom's workbench: loom-workbench {name}.csproj opens it, plays it, and compiles it again on
			       every save. Loom is referenced from the workbench's own folder, so the two always match. -->

			  <PropertyGroup>
			    <TargetFramework>net{Environment.Version.Major}.0</TargetFramework>
			    <ImplicitUsings>enable</ImplicitUsings>
			    <Nullable>enable</Nullable>
			    <RootNamespace>{space}</RootNamespace>
			    <LoomDirectory>{loomDirectory.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}</LoomDirectory>
			  </PropertyGroup>

			  <ItemGroup>
			    <Reference Include="Loom.Audio" HintPath="$(LoomDirectory)Loom.Audio.dll" />
			    <Reference Include="Loom.Audio.Synth" HintPath="$(LoomDirectory)Loom.Audio.Synth.dll" />
			  </ItemGroup>

			</Project>

			""");
		File.WriteAllText(Path.Combine(folder, ".gitignore"), "bin/\nobj/\n.loom/\n");
		File.WriteAllText(Path.Combine(folder, "Catalog.cs"), $$"""
			using Loom.Audio.Synth.Chip;
			using Loom.Audio.Synth.Music;

			[assembly: MusicCatalog(typeof({{space}}.Catalog))]

			namespace {{space}};

			/// <summary>What the workbench finds: the songs, patches and kits, each a factory it calls again after a save, and the cues.</summary>
			public static class Catalog
			{
				public static IReadOnlyList<(string Name, Func<Song> Build)> Songs { get; } = [("first song", FirstSong.Build)];

				public static IReadOnlyList<(string Name, Func<Patch> Build)> Patches { get; } =
				[
					("keys", () => Instruments.Keys),
					("bass", () => Instruments.Bass),
				];

				public static IReadOnlyList<(string Name, Func<DrumKit> Build)> Kits { get; } = [];

				public static IReadOnlyList<CueBase> Cues { get; } = [MusicCues.Intensity];

				/// <summary>How the code names a macro, for the patch designer's C#.</summary>
				public static string? MacroName(int id) => id == Instruments.Bright.Id ? "Instruments.Bright" : null;
			}

			""");
		File.WriteAllText(Path.Combine(folder, "MusicCues.cs"), $$"""
			using Loom.Audio.Synth.Music;

			namespace {{space}};

			/// <summary>What the music listens to: a game sets these as it plays, the workbench from its sliders.</summary>
			public static class MusicCues
			{
				/// <summary>How much is going on, 0 to 1.</summary>
				public static Cue Intensity { get; } = new("intensity", 0.3f);
			}

			""");
		File.WriteAllText(Path.Combine(folder, "Instruments.cs"), $$"""
			using Loom.Audio.Synth.Chip;

			namespace {{space}};

			/// <summary>The instruments: each a property that builds its patch afresh, so a save is heard at the next bar.</summary>
			public static class Instruments
			{
				/// <summary>Brings the keys up as the intensity rises.</summary>
				public static Macro Bright { get; } = new(0);

				/// <summary>A tine piano: a sine struck by a fast-dying one at fourteen times.</summary>
				public static Patch Keys => Patch.Build("keys", p =>
				{
					var tine = p.Sine(14f, Env.Decay(0.12f), 0.6f, velocity: 0.8f);
					p.Out(p.Sine(1f, Env.Adsr(0.002f, 1.6f, 0.2f, 0.3f)).ModulatedBy(tine, 1.2f));
					p.Macro(Bright, MacroTarget.Gain, 0.6f, 1f);
					p.Send(ChipSend.Space, 0.2f);
				});

				/// <summary>A round bass with a little bite at the start of each note.</summary>
				public static Patch Bass => Patch.Build("bass", p =>
				{
					var pick = p.Sine(3f, Env.Decay(0.06f), 0.8f, velocity: 0.7f);
					p.Out(p.Sine(1f, Env.Adsr(0.002f, 0.9f, 0.3f, 0.08f)).ModulatedBy(pick, 2f));
					p.Mono();
				});
			}

			""");
		File.WriteAllText(Path.Combine(folder, "Songs", "FirstSong.cs"), $$"""
			using Loom.Audio.Synth.Music;

			namespace {{space}};

			/// <summary>A first song: a bass under four chords, keys that come in with the intensity. Edit it and save.</summary>
			public static class FirstSong
			{
				public static Song Build()
				{
					var song = new Song("first song") { Tempo = 100, Scale = Scale.Minor(Note.D) };
					var bass = song.Track("bass", Instruments.Bass).Octave(2);
					var keys = song.Track("keys", Instruments.Keys).Octave(4).Level(-4f);
					keys.Macro(Instruments.Bright).Follow(MusicCues.Intensity, 0f, 1f);

					var verse = song.Section("verse", 4, "i | VI | III | VII");
					verse.Play(bass, Line.Eighths("1 . 1 1 5, . 1 .").OverChords());
					verse.Play(keys, Line.Quarters("1 3 5 3").OverChords()).When(MusicCues.Intensity.Above(0.2f));

					song.Order(verse);
					return song;
				}
			}

			""");
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
