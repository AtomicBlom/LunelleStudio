using Loom.Audio.Synth;
using Loom.Audio.Synth.Chip;
using Loom.Audio.Synth.Music;
using Loom.Audio.Workbench;
using Loom.Audio.Workbench.Projects;

namespace Loom.Tests;

/// <summary>
/// Editing as data: the roll's edits to a part's notation, and a workbench over a song file, an edit heard at the next
/// bar, written to the file, undone, and the file read again when something else changes it.
/// </summary>
public class RollTests
{
	private static readonly Cue Energy = new("energy");

	[Test]
	public void A_line_edited_a_step_at_a_time_keeps_its_bars()
	{
		var line = NotationText.Line("1 - 3 . | 5 . . .");
		RollEdits.Place(line, 2, 4);
		line.ToString().ShouldBe("1 - 5 . | 5 . . .");
		RollEdits.Resize(line, 2, 4);
		line.ToString().ShouldBe("1 - 5 - | - . . .");
		RollEdits.Move(line, 2, 5, 7);
		line.ToString().ShouldBe("1 - . . | . 1' - -");
		RollEdits.Change(line, 5, n => n with { Accidental = -1, Accent = true });
		line.ToString().ShouldBe("1 - . . | . ^b1' - -");
		RollEdits.NoteAt(line, 7).ShouldBe(5);
		RollEdits.Delete(line, 0);
		line.ToString().ShouldBe(". . . . | . ^b1' - -");
		// A note placed over the middle of another cuts it there, and what it covered of it is gone
		RollEdits.Place(line, 6, 0, 1);
		line.ToString().ShouldBe(". . . . | . ^b1' 1 .");
		line.Count.ShouldBe(8);
	}

	[Test]
	public void A_beat_and_a_strum_step_through_their_hits()
	{
		var hits = new[] { "." };
		foreach (var _ in Enumerable.Range(0, 4))
			hits = [.. hits, RollEdits.NextHit(hits[^1])];
		hits.ShouldBe([".", "x", "X", "o", "."]);
		RollEdits.NextStroke(".").ShouldBe("D");
		RollEdits.NextStroke("_").ShouldBe(".");
	}

	private sealed class Rig : IDisposable
	{
		public readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "loom-roll-" + Guid.NewGuid().ToString("N")[..8] + ".song.json");
		public readonly OfflineSession Session = new();
		public readonly Workbench Workbench;
		public readonly WorkbenchCatalog Catalog;

		// The song file written from code (Code() where none is given), and the catalog's song built from it too
		public Rig(Func<Song>? code = null)
		{
			code ??= Code;
			File.WriteAllText(Path, SongFile.Write(code(), new SongNames { Patch = p => p.Name }));
			Catalog = new WorkbenchCatalog
			{
				Songs = [("first", code)],
				Patches = [("click", () => MusicRig.Click)],
				Cues = [Energy],
				SongSource = name => name == "first"
					? new SongSource(Path, new SongBindings { Patch = n => n == MusicRig.Click.Name ? MusicRig.Click : null, Cue = n => n == "energy" ? Energy : null })
					: null,
			};
			Workbench = new Workbench(Session.Engine, Session.Engine.Master, Catalog);
		}

		// The song as code: what the file was written from, and what plays where the song has no file
		private static Song Code()
		{
			var song = new Song("first");
			var lead = song.Track("lead", MusicRig.Click);
			var verse = song.Section("verse", 1);
			verse.Play(lead, Line.Quarters("1 3 5 3")).Named("melody");
			verse.Play(lead, Line.Quarters("1 . . .")).When(Energy.Above(0.5f));
			return song;
		}

		public void Step(double seconds)
		{
			for (var done = 0.0; done < seconds; done += 256.0 / MusicRig.Rate)
			{
				Workbench.Update(256.0 / MusicRig.Rate);
				Session.Render(256);
			}
		}

		public void Dispose()
		{
			Workbench.Dispose();
			Session.Dispose();
			File.Delete(Path);
		}
	}

	[Test]
	public void An_edit_in_the_roll_is_heard_written_to_the_file_and_undone()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.Tracks[0].Placements[0].SelectCommand.Execute(null);
		var roll = model.Roll;
		roll.Kind.ShouldBe(RollKind.Line);
		roll.Title.ShouldBe("melody · verse");
		roll.Notes.Select(n => n.Token).ShouldBe(["1", "3", "5", "3"]);

		// The second step's note, the third, moved up to the sixth: drag it from where it is
		var third = roll.Notes[1];
		roll.Press(third.Step, third.Row, secondary: false, twice: false);
		roll.DragTo(third.Step, third.Row - 3);
		roll.Release();
		roll.Notes.Select(n => n.Token).ShouldBe(["1", "6", "5", "3"]);
		rig.Workbench.Editor!.IsDirty.ShouldBeTrue();
		roll.Status.ShouldStartWith("edited");
		roll.CanUndo.ShouldBeTrue();
		rig.Workbench.Player.Song!.Sections[0].Placements[0].Part.ShouldBeOfType<Line>().Text.ShouldBe("1 6 5 3");

		rig.Step(SongEditor.SaveAfterSeconds + 0.1);
		File.ReadAllText(rig.Path).ShouldContain("\"notes\": \"1 6 5 3\"");
		model.BuildText.ShouldStartWith("saved");

		// The drag was one edit, undone in one
		rig.Workbench.UndoEdit();
		roll.Notes.Select(n => n.Token).ShouldBe(["1", "3", "5", "3"]);
		roll.CanRedo.ShouldBeTrue();
		rig.Step(SongEditor.SaveAfterSeconds + 0.1);
		File.ReadAllText(rig.Path).ShouldContain("\"notes\": \"1 3 5 3\"");
		rig.Workbench.RedoEdit();
		roll.Notes.Select(n => n.Token).ShouldBe(["1", "6", "5", "3"]);
		rig.Workbench.RevertPart();
		roll.Notes.Select(n => n.Token).ShouldBe(["1", "3", "5", "3"]);
	}

	[Test]
	public void A_file_changed_by_something_else_is_read_again_and_its_undo_cleared()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.Tracks[0].Placements[0].SelectCommand.Execute(null);
		model.Roll.Press(0, model.Roll.Notes[0].Row, secondary: true, twice: false);
		model.Roll.Notes.Count.ShouldBe(3);
		rig.Step(SongEditor.SaveAfterSeconds + 0.1);

		File.WriteAllText(rig.Path, File.ReadAllText(rig.Path).Replace(". 3 5 3", "8 8 8 8", StringComparison.Ordinal));
		rig.Workbench.UseCatalog(rig.Catalog);
		model.Roll.Notes.Select(n => n.Token).ShouldBe(["8", "8", "8", "8"]);
		model.Roll.CanUndo.ShouldBeFalse();
		model.BuildText.ShouldContain("changed on disk");
	}

	[Test]
	public void A_part_that_is_code_shows_why_it_is_not_edited_here()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		var text = File.ReadAllText(rig.Path);
		File.WriteAllText(rig.Path, text.Replace("\"line\": \"quarters\", \"notes\": \"1 . . .\"", "\"part\": { \"code\": \"Hit\" }", StringComparison.Ordinal));
		var withCode = rig.Catalog with
		{
			SongSource = name => new SongSource(rig.Path, new SongBindings
			{
				Patch = n => n == MusicRig.Click.Name ? MusicRig.Click : null,
				Cue = n => n == "energy" ? Energy : null,
				Part = n => n == "Hit" ? Line.Quarters("5 . . .") : null,
			}),
		};
		rig.Workbench.UseCatalog(withCode);
		model.Tracks[0].Placements[1].SelectCommand.Execute(null);
		model.Roll.Kind.ShouldBe(RollKind.None);
		model.Roll.Status.ShouldContain("code");
	}

	[Test]
	public void The_patch_designer_writes_its_edits_to_the_patch_file_keeping_its_names()
	{
		var path = Path.Combine(Path.GetTempPath(), "loom-patch-" + Guid.NewGuid().ToString("N")[..8] + ".patch.json");
		var spec = PatchSpec.From(MusicRig.Click);
		spec.About = "A click, for the test";
		File.WriteAllText(path, PatchFile.Write(spec, ["blip"]));
		try
		{
			using var session = new OfflineSession();
			using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog
			{
				Patches = [("click", () => MusicRig.Click)],
				PatchFile = name => name == "click" ? path : null,
			});
			workbench.ChoosePatch(0);
			workbench.PatchFilePath.ShouldBe(path);
			workbench.ViewModel.Designer.Names.ShouldBe(["blip"]);

			workbench.ViewModel.Designer.AddButtons[0].Command!.Execute(null);
			for (var seconds = 0.0; seconds < SongEditor.SaveAfterSeconds + 0.1; seconds += 256.0 / MusicRig.Rate)
			{
				workbench.Update(256.0 / MusicRig.Rate);
				session.Render(256);
			}
			var saved = PatchFile.Read(File.ReadAllText(path), out var names);
			saved.Operators.Count.ShouldBe(2);
			names[0].ShouldBe("blip");
			saved.About.ShouldBe("A click, for the test");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public void A_member_is_added_to_a_types_own_half_in_its_indentation_and_taken_out_with_its_comment()
	{
		var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "loom-code-" + Guid.NewGuid().ToString("N")[..8])).FullName;
		try
		{
			var own = Path.Combine(folder, "Tune.cs");
			File.WriteAllText(own, "namespace Tunes.Songs;\n\nstatic partial class Tune\n{\n    private static int Kept() => 1;\n}\n");
			File.WriteAllText(Path.Combine(folder, "Tune.g.cs"), "namespace Tunes.Songs;\n\nstatic partial class Tune\n{\n}\n");
			var code = new MusicCode(() => [own, Path.Combine(folder, "Tune.g.cs")]) { Types = ["Tunes.Songs.Tune"] };

			var (file, line) = code.AddMember("Tunes.Songs.Tune", folder, "// A riff\nprivate static partial Part Riff() =>\n\tLine.Eighths(\"1 3\");");
			file.ShouldBe(own);
			File.ReadAllText(own).ShouldBe("namespace Tunes.Songs;\n\nstatic partial class Tune\n{\n    private static int Kept() => 1;\n\n    // A riff\n    private static partial Part Riff() =>\n        Line.Eighths(\"1 3\");\n}\n");
			line.ShouldBe(7);
			code.Members("Tunes.Songs.Tune").ShouldBe(["Kept", "Riff"]);
			code.Find("Tunes.Songs.Tune", "Riff")!.Value.Line.ShouldBe(8);
			code.Files.ShouldBe([own]);
			code.Pieces(own).Select(p => p.Name).ShouldBe(["class Tune", "Kept", "Riff"]);

			code.RemoveMember("Tunes.Songs.Tune", "Riff").ShouldBe("// A riff\nprivate static partial Part Riff() =>\n    Line.Eighths(\"1 3\");");
			File.ReadAllText(own).ShouldBe("namespace Tunes.Songs;\n\nstatic partial class Tune\n{\n    private static int Kept() => 1;\n}\n");

			// A type with no half of its own gets one, beside the music
			var (made, _) = code.AddMember("Tunes.Songs.Other", folder, "static partial void OnBuilt(Song song) { }");
			made.ShouldBe(Path.Combine(folder, "Other.cs"));
			File.ReadAllText(made).ShouldContain("namespace Tunes.Songs;");
			File.ReadAllText(made).ShouldContain("static partial class Other\n{\n    static partial void OnBuilt(Song song) { }\n}");
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public void A_part_ejected_to_code_and_brought_back_leaves_the_song_file_and_its_code_as_they_were()
	{
		using var rig = new Rig();
		var folder = Path.GetDirectoryName(rig.Path)!;
		var own = Path.Combine(folder, "First" + Guid.NewGuid().ToString("N")[..6] + ".cs");
		var type = "Tunes." + Path.GetFileNameWithoutExtension(own);
		File.WriteAllText(own, $"namespace Tunes;\n\nstatic partial class {Path.GetFileNameWithoutExtension(own)}\n{{\n}}\n");
		try
		{
			var catalog = rig.Catalog with
			{
				SongSource = name => new SongSource(rig.Path, new SongBindings
				{
					Patch = n => n == MusicRig.Click.Name ? MusicRig.Click : null,
					Cue = n => n == "energy" ? Energy : null,
				}, type),
			};
			rig.Workbench.UseCatalog(catalog);
			rig.Workbench.Code = new MusicCode(() => [own]) { Types = [type] };
			var model = rig.Workbench.ViewModel;
			model.Tracks[0].Placements[0].SelectCommand.Execute(null);
			model.Roll.CanEject.ShouldBeTrue();
			var before = File.ReadAllText(rig.Path);

			model.Roll.EjectNotationCommand.Execute(null);
			File.ReadAllText(rig.Path).ShouldContain("\"part\": { \"code\": \"Melody\" }");
			File.ReadAllText(own).ShouldContain("private static partial Part Melody() =>");
			File.ReadAllText(own).ShouldContain("Line.Quarters(\"1 3 5 3\");");
			model.Roll.Kind.ShouldBe(RollKind.None);
			model.Roll.CanBringBack.ShouldBeTrue();
			model.Roll.CodeWhere.ShouldContain("Melody, in");

			// The project hasn't compiled the member, so what plays is still the data's: brought back, it is the data again
			model.Roll.BringBackCommand.Execute(null);
			File.ReadAllText(rig.Path).ShouldBe(before);
			File.ReadAllText(own).ShouldNotContain("Melody");
			model.Roll.Kind.ShouldBe(RollKind.Line);
		}
		finally
		{
			File.Delete(own);
		}
	}

	[Test]
	public void A_track_renamed_takes_its_parts_and_sweeps_with_it()
	{
		using var rig = new Rig(() =>
		{
			var song = new Song("first");
			var lead = song.Track("lead", MusicRig.Click);
			var verse = song.Section("verse", 1);
			verse.Play(lead, Line.Quarters("1 3 5 3")).Named("melody");
			verse.Sweep(lead, new Macro(0), 0f, 1f);
			return song;
		});
		var model = rig.Workbench.ViewModel;

		model.Tracks[0].Rename("bass").ShouldBeNull();
		rig.Step(SongEditor.SaveAfterSeconds + 0.1);

		var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(rig.Path))!;
		var section = json["sections"]![0]!;
		section["parts"]![0]!["track"]!.GetValue<string>().ShouldBe("bass");
		section["sweeps"]![0]!["track"]!.GetValue<string>().ShouldBe("bass");
		// The file still reads: a sweep left naming the old track would be refused
		var song = SongFile.Load(File.ReadAllText(rig.Path), new SongBindings { Patch = n => n == MusicRig.Click.Name ? MusicRig.Click : null });
		song.Sections[0].Sweeps.Single().Track.Name.ShouldBe("bass");
	}
}
