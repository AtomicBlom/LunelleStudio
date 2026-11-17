using System.Diagnostics;
using System.Runtime.CompilerServices;

using Loom.Audio;
using Loom.Audio.Synth;
using Loom.Audio.Synth.Music;
using Loom.Audio.Workbench;
using Loom.Audio.Workbench.Projects;

namespace Loom.Tests;

/// <summary>
/// A music project opened from its .csproj and reloaded in-process with Roslyn: a save compiled and taken up, a change to
/// a type's shape included (which dotnet watch can't apply), a broken save's errors, and a replaced build unloading.
/// </summary>
public class MusicProjectTests
{
	private static string SynthProject([CallerFilePath] string here = "") =>
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "Loom.Audio.Synth", "Loom.Audio.Synth.csproj"));

	internal static string Music(string notes, string extra = "") => $$"""
		using Loom.Audio.Synth.Chip;
		using Loom.Audio.Synth.Music;

		[assembly: MusicCatalog(typeof(Tunes.Catalog))]

		namespace Tunes;

		{{extra}}

		public static class Catalog
		{
			public static IReadOnlyList<(string Name, Func<Song> Build)> Songs { get; } = [("tune", Tune)];

			public static IReadOnlyList<(string Name, Func<Patch> Build)> Patches { get; } = [("tone", () => Patch.Build("tone", p => p.Out(p.Sine(1f, Env.Gate))))];

			public static string? MacroName(int id) => id == 0 ? "Macros.Bright" : null;

			private static Song Tune()
			{
				var song = new Song("tune");
				song.Section("only", 1).Play(song.Track("lead", Patches[0].Build()), Line.Quarters("{{notes}}"));
				return song;
			}
		}
		""";

	internal static async Task<string> MakeProjectAsync()
	{
		var folder = Path.Combine(Path.GetTempPath(), "loom-music-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(folder);
		await File.WriteAllTextAsync(Path.Combine(folder, "Tunes.csproj"), $"""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net11.0</TargetFramework>
			    <ImplicitUsings>enable</ImplicitUsings>
			    <Nullable>enable</Nullable>
			  </PropertyGroup>
			  <ItemGroup>
			    <ProjectReference Include="{SynthProject()}" />
			  </ItemGroup>
			</Project>
			""");
		await File.WriteAllTextAsync(Path.Combine(folder, "Music.cs"), Music("1 2 3 4"));
		var restore = Process.Start(new ProcessStartInfo("dotnet", ["restore", folder]) { RedirectStandardOutput = true, UseShellExecute = false })!;
		await restore.WaitForExitAsync();
		restore.ExitCode.ShouldBe(0);
		return Path.Combine(folder, "Tunes.csproj");
	}

	private static async Task<ProjectBuild> NextBuildAsync(MusicProject project)
	{
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(60))
		{
			if (project.TakeBuild() is { } build)
				return build;
			await Task.Delay(50);
		}
		throw new TimeoutException("The project wasn't compiled again after the save.");
	}

	[Test]
	public async Task A_project_is_compiled_reloaded_on_a_save_and_its_old_build_unloads()
	{
		var path = await MakeProjectAsync();
		using var project = await MusicProject.OpenAsync(path);
		var first = WeakFirstBuild(project);

		// A change to a type's shape: a new record, which hot reload would call a rude edit
		await File.WriteAllTextAsync(Path.Combine(project.Directory, "Music.cs"), Music("5 5 5 5", "public sealed record Extra(int Value);"));
		var second = await NextBuildAsync(project);
		second.Errors.ShouldBeEmpty();
		second.Catalog!.Songs.ShouldHaveSingleItem().Name.ShouldBe("tune");
		second.Catalog.MacroName!(0).ShouldBe("Macros.Bright");

		// The first build, released and no longer referred to, is collected and its context unloaded
		for (var i = 0; i < 20 && first.IsAlive; i++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
		first.IsAlive.ShouldBeFalse();

		await File.WriteAllTextAsync(Path.Combine(project.Directory, "Music.cs"), Music("1 2 3 4").Replace("return song;", "return song", StringComparison.Ordinal));
		var broken = await NextBuildAsync(project);
		broken.Succeeded.ShouldBeFalse();
		broken.Errors.ShouldContain(e => e.Contains("Music.cs(") && e.Contains("error CS1002"));
		second.Release();
	}

	// The first build taken, its catalog checked, and let go: only a weak reference to its context leaves here
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static WeakReference WeakFirstBuild(MusicProject project)
	{
		var build = project.TakeBuild().ShouldNotBeNull();
		build.Errors.ShouldBeEmpty();
		build.Catalog!.Songs.ShouldHaveSingleItem().Name.ShouldBe("tune");
		build.Catalog.Patches.ShouldHaveSingleItem().Build().Name.ShouldBe("tone");
		build.Release();
		return build.Context;
	}

	[Test]
	public async Task A_workbench_on_a_project_plays_its_song_and_takes_up_a_save()
	{
		var path = await MakeProjectAsync();
		using var project = await MusicProject.OpenAsync(path);

		// The engine belongs to the thread that made it: from here on, no awaits
		using var session = new OfflineSession();
		var first = project.TakeBuild().ShouldNotBeNull();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, first.Catalog!);
		using var host = new WorkbenchProject(project, workbench);
		workbench.Play();
		File.WriteAllText(Path.Combine(project.Directory, "Music.cs"), Music("8 8 8 8"));
		var deadline = Stopwatch.StartNew();
		while (host.Current is null && deadline.Elapsed < TimeSpan.FromSeconds(60))
		{
			host.Update();
			workbench.Update(1.0 / 60.0);
			session.Render(800);
			Thread.Sleep(20);
		}
		host.Current.ShouldNotBeNull();
		workbench.ViewModel.BuildText.ShouldStartWith("compiled");
		workbench.ViewModel.SongName.ShouldBe("tune");
		first.Release();
	}

	[Test]
	public void A_project_built_against_another_build_of_what_the_host_shares_is_refused()
	{
		var synth = typeof(Song).Assembly.Location;
		SharedReferences.Mismatch(new Dictionary<string, string> { ["Loom.Audio.Synth"] = synth }).ShouldBeNull();

		// Another build of Loom.Audio.Synth: any other assembly stands in for it, its module's id differing
		var other = typeof(AudioTap).Assembly.Location;
		SharedReferences.Mismatch(new Dictionary<string, string> { ["Loom.Audio.Synth"] = other })
			.ShouldNotBeNull().ShouldContain($"Loom.Audio.Synth: the project's is {other}, the workbench runs {synth}");
	}

	[Test]
	public async Task A_new_project_from_the_template_builds_and_offers_its_song()
	{
		var folder = Path.Combine(Path.GetTempPath(), "loom-new-" + Guid.NewGuid().ToString("N")[..8], "My Tunes");
		var path = ProjectTemplate.Create(folder, Path.GetDirectoryName(typeof(Song).Assembly.Location));
		Path.GetFileName(path).ShouldBe("My Tunes.csproj");
		Should.Throw<InvalidOperationException>(() => ProjectTemplate.Create(folder));

		using var project = await MusicProject.OpenAsync(path);
		var build = project.TakeBuild().ShouldNotBeNull();
		build.Errors.ShouldBeEmpty();
		build.Catalog!.Songs.ShouldHaveSingleItem().Name.ShouldBe("first song");
		build.Catalog.Patches.Select(p => p.Name).ShouldBe(["keys", "bass"]);
		build.Catalog.Cues.ShouldHaveSingleItem().Name.ShouldBe("intensity");
		build.Catalog.MacroName!(0).ShouldBe("Instruments.Bright");
		build.Catalog.Songs[0].Build().Sections.ShouldHaveSingleItem();
		build.Release();
	}
}
