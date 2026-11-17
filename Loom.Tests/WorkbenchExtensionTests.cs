using Loom.Audio;
using Loom.Audio.Synth;
using Loom.Audio.Synth.Music;
using Loom.Audio.Workbench;
using Loom.Audio.Workbench.Projects;

using SUIsei.Controls;

[assembly: WorkbenchExtension(typeof(Loom.Tests.WorkbenchExtensionTests.TestExtension))]

namespace Loom.Tests;

/// <summary>
/// Extensions: a game's tools in the workbench, found by their assembly's attribute, playing the music into their own
/// bus, adding controls, kept across every compile of the music and told of each build; one built against another Loom
/// refused.
/// </summary>
public class WorkbenchExtensionTests
{
	/// <summary>An extension as a game would write one: a bus of its own for the music, and a control.</summary>
	public sealed class TestExtension : IWorkbenchExtension
	{
		public AudioBus? Bus;
		public string? Recording;
		public int Frames;
		public readonly List<System.Reflection.Assembly> Builds = [];

		public AudioBus? Output(AudioEngine engine) => Bus = engine.CreateBus("the game's mix");

		public void Attach(WorkbenchExtensionContext context)
		{
			Recording = context.Option("--recording");
			context.TransportExtras.Children.Add(new ToggleButton { Name = "CarButton" });
		}

		public void Update(double seconds) => Frames++;

		public void MusicBuilt(System.Reflection.Assembly music) => Builds.Add(music);

		public void Dispose()
		{
		}
	}

	[Test]
	public void An_extension_is_found_by_its_assemblys_attribute_and_attached_with_its_bus_and_controls()
	{
		var extension = WorkbenchExtensions.Create(typeof(WorkbenchExtensionTests).Assembly).ShouldBeOfType<TestExtension>();
		using var session = new OfflineSession();
		var bus = extension.Output(session.Engine);
		bus.ShouldNotBeNull();
		using var workbench = new Workbench(session.Engine, bus.Value, new WorkbenchCatalog());
		var extras = new StackPanel();
		extension.Attach(new WorkbenchExtensionContext(workbench, session.Engine, extras, ["--recording", "race.zip"]));
		extension.Recording.ShouldBe("race.zip");
		extras.Children.Count.ShouldBe(1);
		extras.Children[0].ShouldBeOfType<ToggleButton>().Name.ShouldBe("CarButton");
		Should.Throw<InvalidOperationException>(() => WorkbenchExtensions.Create(typeof(Song).Assembly)).Message.ShouldContain("names no extension");
	}

	[Test]
	public async Task An_extension_stays_the_same_while_the_music_compiles_again_and_hears_of_each_build()
	{
		var path = await MusicProjectTests.MakeProjectAsync();
		using var project = await MusicProject.OpenAsync(path);
		var extension = new TestExtension();
		using var session = new OfflineSession();
		var first = project.TakeBuild().ShouldNotBeNull();
		using var workbench = new Workbench(session.Engine, extension.Output(session.Engine)!.Value, first.Catalog!);
		using var host = new WorkbenchProject(project, workbench) { Extension = extension };
		File.WriteAllText(Path.Combine(project.Directory, "Music.cs"), MusicProjectTests.Music("2 2 2 2"));
		var deadline = System.Diagnostics.Stopwatch.StartNew();
		while (extension.Builds.Count == 0 && deadline.Elapsed < TimeSpan.FromSeconds(60))
		{
			host.Update();
			Thread.Sleep(20);
		}
		extension.Builds.ShouldHaveSingleItem().GetName().Name.ShouldBe("Tunes");
		host.Extension.ShouldBeSameAs(extension);
		first.Release();
	}

	[Test]
	public void An_extension_built_against_another_Loom_is_refused()
	{
		var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "loom-ext-" + Guid.NewGuid().ToString("N")[..8])).FullName;
		try
		{
			// Another build of Loom.Audio.Synth beside it: any other assembly of the same framework stands in for it
			var stale = Path.Combine(folder, "Loom.Audio.Synth.dll");
			File.Copy(typeof(AudioEngine).Assembly.Location, stale);
			Should.Throw<InvalidOperationException>(() => WorkbenchExtensions.Load(stale)).Message.ShouldContain("Loom.Audio.Synth: the project's is");
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}
