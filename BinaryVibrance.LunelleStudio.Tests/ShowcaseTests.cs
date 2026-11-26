using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Sirrow;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// samples/Showcase, the browser's first project: it reads whole, every section of every song sounds whatever the cues
/// say, and every instrument in it is a preset's own file, so the library shows each as the project's.
/// </summary>
public class ShowcaseTests
{
	private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
		Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, ".."));

	private static string Showcase => Path.Combine(RepositoryRoot(), "samples", "Showcase");

	[Test]
	public void The_showcase_reads_whole_with_its_songs_cues_and_macros()
	{
		var music = MusicFolder.FromDirectory(Showcase);
		FolderCatalog.Problems(music).ShouldBeEmpty();
		music.Songs.Select(s => s.Name).ShouldBe(["aurora", "back-porch", "blue-hour", "double-shot", "undertow", "watchtower"]);
		music.Cues.Select(c => c.Name).ShouldBe(["intensity", "mood", "caffeine", "time-of-day", "activity"]);
		music.Macros.Keys.ShouldBe(["bright", "drive", "sweep"], ignoreOrder: true);
		foreach (var (name, build) in music.Songs)
		{
			var song = build();
			song.Sections.Count.ShouldBeGreaterThan(1, name);
			song.Tracks.ShouldAllBe(t => t.Patch != null || t.Kit != null, name);
		}
	}

	[Test]
	public void Every_section_of_every_song_sounds_low_and_high_and_with_every_choice()
	{
		var music = MusicFolder.FromDirectory(Showcase);
		foreach (var (name, build) in music.Songs)
		{
			var song = build();
			// Each song's own cues: its levels low, then high, each time with every choice cue on each of its options in turn
			// (a cue with fewer options wraps round), so every option of every cue is heard at both levels
			var levels = song.Cues.Where(c => c.OptionNames.Count == 0).ToList();
			var choices = song.Cues.Where(c => c.OptionNames.Count > 0).ToList();
			var turns = choices.Count == 0 ? 1 : choices.Max(c => c.OptionNames.Count);
			foreach (var level in new[] { 0.15f, 0.9f })
				for (var turn = 0; turn < turns; turn++)
					foreach (var section in song.Sections)
					{
						using var session = new OfflineSession(48_000);
						using var player = new MusicPlayer(session.Engine, session.Engine.Master);
						player.Play(player.Prepare(song), 0f);
						foreach (var cue in levels)
							player.Set(cue, level);
						foreach (var cue in choices)
							player.Set(cue, cue.OptionValue(turn % cue.OptionNames.Count));
						player.Jump(section);
						// Two bars of the section, whatever its tempo
						var frames = (int)(2 * 4 * 60.0 / song.Tempo * 48_000);
						for (var done = 0; done < frames; done += 256)
						{
							player.Update();
							session.Render(256);
						}
						var left = session.Left();
						var where = $"{name} {section.Name} at {level}, {string.Join(", ", choices.Select(c => $"{c.Name} {c.OptionNames[turn % c.OptionNames.Count]}"))}";
						left.ShouldAllBe(x => float.IsFinite(x), where);
						left.Max(MathF.Abs).ShouldBeGreaterThan(0.01f, where);
						left.Max(MathF.Abs).ShouldBeLessThanOrEqualTo(1f, where);
					}
		}
	}

	[Test]
	public void Every_instrument_in_the_showcase_is_a_preset_s_own()
	{
		var library = PresetLibrary.FromDirectory(Path.Combine(RepositoryRoot(), "presets"));
		var music = MusicFolder.FromDirectory(Showcase);
		var shipped = library.Presets.SelectMany(library.FilesFor).ToLookup(f => f.Path);
		foreach (var file in Directory.EnumerateFiles(Showcase, "*", SearchOption.AllDirectories))
		{
			var path = Path.GetRelativePath(Showcase, file).Replace('\\', '/');
			if (!path.StartsWith("Patches/", StringComparison.Ordinal) && !path.StartsWith("Kits/", StringComparison.Ordinal))
				continue;
			shipped[path].ShouldNotBeEmpty(path);
			shipped[path].First().Bytes.ShouldBe(File.ReadAllBytes(file), path);
		}
		music.Kits.Select(k => k.Name).ShouldBe(["breaks", "breaks-deep", "general", "grind", "rock"], ignoreOrder: true);
	}
}
