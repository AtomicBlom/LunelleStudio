using System.Text.Json.Nodes;
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
		// Placed where a note starts, a note joins it as a chord: to replace it, it goes first
		RollEdits.Delete(line, 2);
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

	[Test]
	public void Notes_placed_where_others_start_make_a_chord_edited_a_note_at_a_time()
	{
		var line = NotationText.Line("1 - . . | . . . .");
		// Row 2 is the third, row 4 the fifth: they join the note starting at step 0, as long as it is
		RollEdits.Place(line, 0, 2);
		RollEdits.Place(line, 0, 4);
		line.ToString().ShouldBe("1/3/5 - . . | . . . .");
		RollEdits.NoteAt(line, 1, 2).ShouldBe(0);
		RollEdits.NoteAt(line, 1, 3).ShouldBe(-1);

		// A chord lengthens together; one of its notes changes, leaves or goes alone
		RollEdits.Resize(line, 0, 2);
		line.ToString().ShouldBe("1/3/5 - - . | . . . .");
		RollEdits.Change(line, 0, n => n with { Accidental = -1 }, row: 2);
		line.ToString().ShouldBe("1/b3/5 - - . | . . . .");
		RollEdits.Move(line, 0, 4, 4, from: 4);
		line.ToString().ShouldBe("1/b3 - - . | 5 - - .");
		RollEdits.Delete(line, 0, row: 0);
		line.ToString().ShouldBe("b3 - - . | 5 - - .");
		RollEdits.Delete(line, 0, row: 2);
		line.ToString().ShouldBe(". . . . | 5 - - .");

		// Moved whole, a chord keeps its shape
		RollEdits.Place(line, 4, 2);
		RollEdits.Move(line, 4, 0, 1);
		line.ToString().ShouldBe("2/4 - - . | . . . .");
	}

	[Test]
	public void The_roll_stacks_a_chord_and_deletes_one_of_its_notes()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		model.Tracks[0].Placements[0].SelectCommand.Execute(null);
		var roll = model.Roll;
		// "1 3 5 3": a click two rows above the first note (on its step) stacks a third onto it
		var first = roll.Notes[0];
		roll.Press(first.Step, first.Row - 2, secondary: false, twice: false);
		roll.Release();
		roll.Notes.Where(n => n.Step == 0).Select(n => n.Token).ShouldBe(["1", "3"], ignoreOrder: true);
		roll.IsSelected(0, first.Row - 2).ShouldBeTrue();

		// A right-click on the first note takes it alone out of the chord
		roll.Press(first.Step, first.Row, secondary: true, twice: false);
		roll.Notes.Where(n => n.Step == 0).Select(n => n.Token).ShouldBe(["3"]);
	}

	[Test]
	public void A_part_regridded_keeps_its_timing()
	{
		// Finer: a line's notes held on, its rests still rests; a strum's strokes ringing
		var line = RollEdits.Regrid(["1", "-", "3", "."], Ticks.Quarter, Ticks.Eighth, characters: false)!;
		line.ShouldBe(["1", "-", "-", "-", "3", "-", ".", "."]);
		RollEdits.Regrid(["D", "x"], Ticks.Quarter, Ticks.Eighth, characters: true).ShouldBe(["D", ".", "x", "."]);

		// Coarser: back again, a note that ran short filling its step; one starting between steps can't go
		RollEdits.Regrid(line, Ticks.Eighth, Ticks.Quarter, characters: false).ShouldBe(["1", "-", "3", "."]);
		RollEdits.Regrid(["1", ".", "3", "."], Ticks.Eighth, Ticks.Quarter, characters: false).ShouldBe(["1", "3"]);
		RollEdits.Regrid(["1", "3", ".", "."], Ticks.Eighth, Ticks.Quarter, characters: false).ShouldBeNull();
		RollEdits.Regrid(["D", ".", "D", "U"], Ticks.Eighth, Ticks.Quarter, characters: true).ShouldBeNull();
		// Triplets and eighths don't divide into each other
		RollEdits.Regrid(["1", ".", "."], Ticks.Triplet, Ticks.Eighth, characters: false).ShouldBeNull();

		RollEdits.Write(line, 4, 2, characters: false).ShouldBe("1 - - - | 3 - . .");
		RollEdits.Write(["x", ".", "x", ".", "X", ".", "o", "."], 8, 2, characters: true).ShouldBe("x. x. X. o.");
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
		roll.Status.ShouldStartWith("Edited");
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
	public void A_track_is_inspected_renamed_and_its_patch_edited_as_the_song_plays()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.Tracks[0].Inspect();
		model.Inspected.ShouldBe(model.Tracks[0]);
		model.Tracks[0].IsInspected.ShouldBeTrue();

		// Renamed, the inspector follows it to its new name
		model.Tracks[0].Rename("hook").ShouldBeNull();
		model.Inspected.ShouldBeOfType<TrackViewModel>().Name.ShouldBe("hook");
		model.Tracks[0].IsInspected.ShouldBeTrue();

		// Its patch opened in the designer: an edit is heard in the song once the edits pause
		var before = rig.Workbench.Player.Song!.Tracks[0].Patch!.Operators.Length;
		model.Tracks[0].OpenInstrument();
		model.IsPatch.ShouldBeTrue();
		model.Designer.AddButtons[0].Command!.Execute(null);
		rig.Step(0.3);
		rig.Workbench.Player.Song!.Tracks[0].Patch!.Operators.Length.ShouldBe(before + 1);
	}

	[Test]
	public void The_roll_lights_each_chords_notes_and_a_line_can_follow_the_chords()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.Sections[0].SetChords("I IV").ShouldBeNull();
		model.Tracks[0].Placements[0].SelectCommand.Execute(null);
		var roll = model.Roll;
		// The middle octave's rows lit at a step, or only the root's
		string[] Lit(int step, bool root) => [.. roll.ChordTones
			.Where(t => t.From <= step && step < t.To && (!root || t.Root))
			.Select(t => roll.Rows[t.Row]).Where(r => r.Length == 1).Order()];

		// "1 3 5 3" in C major: C under I, then F's chord under IV
		Lit(0, root: false).ShouldBe(["1", "3", "5"]);
		Lit(0, root: true).ShouldBe(["1"]);
		Lit(2, root: false).ShouldBe(["1", "4", "6"]);
		Lit(2, root: true).ShouldBe(["4"]);

		// Following the chords, 1 is each chord's root: under IV, 1 3 5 are its notes
		model.Tracks[0].Placements[0].IsOverChords.ShouldBeFalse();
		model.Tracks[0].Placements[0].SetOverChords(true);
		rig.Workbench.Player.Song!.Sections[0].Placements[0].Part.ShouldBeOfType<Line>().IsOverChords.ShouldBeTrue();
		model.Tracks[0].Placements[0].IsOverChords.ShouldBeTrue();
		Lit(2, root: false).ShouldBe(["1", "3", "5"]);
		Lit(2, root: true).ShouldBe(["1"]);
		rig.Step(SongEditor.SaveAfterSeconds + 0.1);
		File.ReadAllText(rig.Path).ShouldContain("\"overChords\": true");
	}

	[Test]
	public void A_part_is_added_as_a_melody_a_bass_line_over_the_chords_or_a_strum()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		Part Added() => rig.Workbench.Player.Song!.Sections[0].Placements[^1].Part;

		model.Tracks[0].AddPart(0, NewPartKind.Bass);
		Added().ShouldBeOfType<Line>().ShouldSatisfyAllConditions(l => l.IsOverChords.ShouldBeTrue(), l => l.OctaveShift.ShouldBe(-1));
		model.Roll.Kind.ShouldBe(RollKind.Line);

		model.Tracks[0].AddPart(0, NewPartKind.Strum);
		Added().ShouldBeOfType<Strum>();
		model.Roll.Kind.ShouldBe(RollKind.Strum);

		model.Tracks[0].AddPart(0, NewPartKind.Melody);
		Added().ShouldBeOfType<Line>().IsOverChords.ShouldBeFalse();
	}

	[Test]
	public void A_part_and_a_track_go_in_where_the_line_was_shown()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;

		// Between the lead's two parts in the verse
		model.Tracks[0].AddPart(0, NewPartKind.Bass, 1);
		rig.Step(0.5);
		var parts = JsonNode.Parse(File.ReadAllText(rig.Path))!["sections"]![0]!["parts"]!.AsArray();
		parts.Count.ShouldBe(3);
		parts[0]!["notes"]!.GetValue<string>().ShouldBe("1 3 5 3");
		parts[1]!["overChords"]!.GetValue<bool>().ShouldBeTrue();
		parts[2]!["notes"]!.GetValue<string>().ShouldBe("1 . . .");

		// Above the lead, then below it
		model.Tracks[0].AddTrackBeside("click", isKit: false, below: false).ShouldBeNull();
		model.Tracks.Select(t => t.Name).ShouldBe(["click", "lead"]);
		model.Tracks[1].AddTrackBeside("click", isKit: false, below: true).ShouldBeNull();
		model.Tracks.Select(t => t.Name).ShouldBe(["click", "lead", "click 2"]);
		model.HasTracks.ShouldBeTrue();
	}

	[Test]
	public void A_patch_says_which_songs_use_it_and_a_track_gets_a_copy_of_its_own()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		var folder = Path.Combine(Path.GetTempPath(), "loom-patches-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(folder);
		try
		{
			var click = Path.Combine(folder, "click.patch.json");
			File.WriteAllText(click, """{ "format": "loom-patch/1", "name": "click", "operators": [ { "name": "sine1", "kind": "sine", "envelope": "decay 0.01", "level": 0.5 } ], "out": [ { "op": "sine1" } ] }""");
			var other = Path.Combine(folder, "other.song.json");
			File.WriteAllText(other, """{ "format": "loom-song/2", "name": "other", "tempo": 100, "scale": "C major", "tracks": [ { "name": "bell", "patch": "click" } ], "sections": [ { "name": "a", "bars": 1, "parts": [] } ] }""");
			var copy = Path.Combine(folder, "click-first.patch.json");
			var bindings = new SongBindings { Patch = n => n.StartsWith("click", StringComparison.Ordinal) ? MusicRig.Click : null, Cue = n => n == "energy" ? Energy : null };
			var catalog = rig.Catalog with
			{
				Songs = [.. rig.Catalog.Songs, ("other", () => new Song("other"))],
				PatchFile = n => n == "click" ? click : n == "click-first" ? copy : null,
				SongSource = n => n == "other" ? new SongSource(other, bindings) : n == "first" ? new SongSource(rig.Path, bindings) : null,
			};
			rig.Workbench.UseCatalog(catalog);

			rig.Workbench.SongsUsing("click").ShouldBe(["first", "other"]);
			model.Tracks[0].PatchUsedElsewhere.ShouldBe(["other"]);
			rig.Workbench.SelectPatch(0);
			model.Designer.UsedBy.ShouldContain("first and other");

			model.Tracks[0].CopyPatchForSong().ShouldBeNull();
			File.Exists(copy).ShouldBeTrue();
			JsonNode.Parse(File.ReadAllText(copy))!["name"]!.GetValue<string>().ShouldBe("click-first");

			// Once the project has built with the copy, the track plays it and the other song keeps the original
			rig.Workbench.UseCatalog(catalog with { Patches = [.. catalog.Patches, ("click-first", () => MusicRig.Click)] });
			rig.Step(0.5);
			JsonNode.Parse(File.ReadAllText(rig.Path))!["tracks"]![0]!["patch"]!.GetValue<string>().ShouldBe("click-first");
			JsonNode.Parse(File.ReadAllText(other))!["tracks"]![0]!["patch"]!.GetValue<string>().ShouldBe("click");
			model.SelectedPatch.ShouldBe(1);
			rig.Workbench.SongsUsing("click").ShouldBe(["other"]);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public void A_new_song_opens_once_the_project_has_built_with_it()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		var stem = "fresh-" + Guid.NewGuid().ToString("N")[..6];
		var made = Path.Combine(Path.GetDirectoryName(rig.Path)!, stem + ".song.json");
		try
		{
			rig.Workbench.NewSong(stem).ShouldBeNull();
			File.Exists(made).ShouldBeTrue();
			model.BuildText.ShouldContain(stem);
			model.SongName.ShouldBe("first");

			rig.Workbench.UseCatalog(rig.Catalog with
			{
				Songs = [.. rig.Catalog.Songs, (stem, () =>
				{
					var song = new Song(stem);
					song.Section("a", 4);
					return song;
				})],
			});
			model.SelectedSong.ShouldBe(1);
			model.SongName.ShouldBe(stem);
			model.IsArrange.ShouldBeTrue();
		}
		finally
		{
			File.Delete(made);
		}
	}

	[Test]
	public void A_part_changes_grid_and_is_deleted()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		var melody = model.Tracks[0].Placements[0];
		melody.GridChoice!.Value.Grid.ShouldBe("quarters");

		melody.SetGrid("eighths").ShouldBeNull();
		var line = rig.Workbench.Player.Song!.Sections[0].Placements[0].Part.ShouldBeOfType<Line>();
		line.Step.ShouldBe(Ticks.Eighth);
		line.Text.ShouldBe("1 - 3 - 5 - 3 -");

		// And back to quarters, as it was
		model.Tracks[0].Placements[0].SetGrid("quarters").ShouldBeNull();
		rig.Workbench.Player.Song!.Sections[0].Placements[0].Part.ShouldBeOfType<Line>().Text.ShouldBe("1 3 5 3");

		var count = rig.Workbench.Player.Song!.Sections[0].Placements.Count;
		model.Tracks[0].Placements[0].Select();
		model.Tracks[0].Placements[0].Delete();
		rig.Workbench.Player.Song!.Sections[0].Placements.Count.ShouldBe(count - 1);
		model.Inspected.ShouldBeNull();
		model.Roll.Kind.ShouldBe(RollKind.None);
	}

	[Test]
	public void The_songs_tempo_and_scale_are_set_from_the_inspector()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.InspectSongCommand.Execute(null);
		model.Inspected.ShouldBeOfType<SongViewModel>().IsInspected.ShouldBeTrue();

		model.Song!.SetTempo(90);
		rig.Workbench.Player.Song!.Tempo.ShouldBe(90f);
		model.Song!.SetScale("D", "dorian");
		rig.Workbench.Player.Song!.Scale.Name.ShouldBe("dorian");
		rig.Workbench.Player.Song!.Scale.Root.ShouldBe(Note.D);
		// Still shown, as the song built again
		model.Inspected.ShouldBeOfType<SongViewModel>().ShouldSatisfyAllConditions(
			s => s.Tempo.ShouldBe(90),
			s => s.Root.ShouldBe("D"),
			s => s.ScaleName.ShouldBe("dorian"));
		Should.Throw<ArgumentException>(() => model.Song!.SetScale("H", "major"));
	}

	[Test]
	public void The_songs_settings_open_in_the_inspector_from_the_transport()
	{
		using var rig = new Rig();
		var host = new Vysara.UIHost();
		var page = new WorkbenchView(rig.Workbench.ViewModel);
		host.CreateRoot(Vysara.UILayer.Default).Root.Content = page;
		var surface = new System.Numerics.Vector2(1400f, 900f);
		host.Tick(0.016, 0f, surface);

		var songButtons = Find<Vysara.Controls.Button>(page).Where(b => Vysara.Controls.ToolTipService.GetToolTip(b) is "Song settings: tempo, key and scale").ToList();
		songButtons.Count.ShouldBe(2);
		songButtons[0].PerformClick();
		host.Tick(0.016, 0f, surface);

		rig.Workbench.ViewModel.Inspected.ShouldBeOfType<SongViewModel>();
		Find<Vysara.Controls.TextBlock>(page).Select(t => t.Text).ShouldContain("Tempo");
	}

	// Every element of a kind in a tree
	private static List<T> Find<T>(Vysara.UIElement top) where T : Vysara.UIElement
	{
		var found = new List<T>();
		void Walk(Vysara.UIElement element)
		{
			if (element is T t)
				found.Add(t);
			for (var i = 0; i < Vysara.Media.VisualTreeHelper.GetChildrenCount(element); i++)
				Walk(Vysara.Media.VisualTreeHelper.GetChild(element, i));
		}
		Walk(top);
		return found;
	}

	[Test]
	public void An_instrument_dropped_on_the_board_adds_a_track_named_for_it()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.AddTrackPlaying("click", isKit: false).ShouldBeNull();
		rig.Workbench.AddTrackPlaying("click", isKit: false).ShouldBeNull();
		model.Tracks.Select(t => t.Name).ShouldBe(["lead", "click", "click 2"]);
		model.Inspected.ShouldBeOfType<TrackViewModel>().Name.ShouldBe("click 2");
	}

	[Test]
	public void An_operator_is_moved_to_another_place_unless_it_would_pass_what_it_reads()
	{
		using var session = new OfflineSession();
		using var workbench = new Workbench(session.Engine, session.Engine.Master, new WorkbenchCatalog { Patches = [("click", () => MusicRig.Click)] });
		workbench.ChoosePatch(0);
		var designer = workbench.ViewModel.Designer;
		designer.AddButtons[0].Command!.Execute(null);
		designer.AddButtons[0].Command!.Execute(null);
		var names = designer.Names.ToList();
		names.Count.ShouldBe(3);

		designer.MoveTo(2, 0).ShouldBe(0);
		designer.Names.ShouldBe([names[2], names[0], names[1]]);
		designer.MoveTo(0, 2).ShouldBe(2);
		designer.Names.ShouldBe(names);

		// One that modulates another can't be moved past it
		designer.Spec.Operators[2] = designer.Spec.Operators[2] with { Modulators = [new Modulation(1, 1f)] };
		designer.MoveTo(1, 2).ShouldBe(1);
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
		model.Roll.Status.ShouldContain("C#");
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
