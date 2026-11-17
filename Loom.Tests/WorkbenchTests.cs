using Loom.Audio.Synth;
using Loom.Audio.Synth.Chip;
using Loom.Audio.Synth.Music;
using Loom.Audio.Workbench;

namespace Loom.Tests;

/// <summary>The workbench's model, headless: what it lists, what its view models do, rebuilding, hot reload's flag, and its saved state.</summary>
public class WorkbenchTests
{
	private static readonly Cue Energy = new("energy");

	private sealed class Rig : IDisposable
	{
		public int Builds;
		public bool Broken;
		public int Degree = 1;
		public readonly OfflineSession Session = new();
		public readonly Workbench Workbench;

		public Rig(WorkbenchState? restore = null)
		{
			var catalog = new WorkbenchCatalog
			{
				Songs = [("first", First), ("second", Second)],
				Patches = [("click", () => MusicRig.Click), ("tone", () => ChipRig.Tone)],
				MacroName = id => id == 0 ? "Macros.Bright" : null,
				Kits = [("kit", () => DrumKit.FromPatches("kit", (Kit.Kick, MusicRig.Click, 0), (Kit.Snare, MusicRig.Click, 0), (new DrumPiece("cowbell"), MusicRig.Click, 0)))],
				Cues = [Energy],
			};
			Workbench = new Workbench(Session.Engine, Session.Engine.Master, catalog);
			if (restore is not null)
				Workbench.RestoreState(restore);
		}

		private Song First()
		{
			Builds++;
			if (Broken)
				throw new NotationException("Line \"1 h\": \"h\" isn't a note (column 3)", "1 h", 1, 3);
			var song = new Song("first");
			var lead = song.Track("lead", MusicRig.Click);
			var pad = song.Track("pad", ChipRig.Tone);
			var verse = song.Section("verse", 1);
			verse.Play(lead, Line.Quarters($"{Degree} 3 5 3")).Named("melody");
			verse.Play(pad, Line.Quarters("1 - - -")).When(Energy.Above(0.5f));
			song.Section("chorus", 2).Play(lead, Line.Quarters("5 5 5 5"));
			return song;
		}

		private static Song Second()
		{
			var song = new Song("second");
			song.Section("only", 1).Play(song.Track("solo", MusicRig.Click), Line.Quarters("1 1 1 1"));
			return song;
		}

		public void Step(int frames)
		{
			for (var done = 0; done < frames; done += 256)
			{
				Workbench.Update(256.0 / MusicRig.Rate);
				Session.Render(Math.Min(256, frames - done));
			}
		}

		public void Dispose()
		{
			Workbench.Dispose();
			Session.Dispose();
		}
	}

	[Test]
	public void Playing_a_song_lists_its_parts()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.SelectSong(0);
		rig.Workbench.Play();

		model.IsPlaying.ShouldBeTrue();
		model.Songs.Select(s => s.Name).ShouldBe(["first", "second"]);
		model.Songs.Select(s => s.IsSelected).ShouldBe([true, false]);
		model.Sections.Select(s => s.Name).ShouldBe(["verse", "chorus"]);
		model.Tracks.Select(t => t.Name).ShouldBe(["lead", "pad"]);
		model.Tracks[0].Placements.Select(p => p.Name).ShouldBe(["melody (verse)", "lead (chorus)"]);
		model.Tracks[1].Placements[0].ConditionText.ShouldBe("when energy above 0.5");
		model.Cues.Select(c => c.Name).ShouldBe(["energy"]);
	}

	[Test]
	public void A_parts_condition_and_what_reads_the_cue_show_whether_they_hold()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		rig.Step(MusicRig.Bar);
		var pad = model.Tracks[1].Placements[0];
		var reader = model.Cues[0].Readers.ShouldHaveSingleItem();
		(reader.Level, reader.Above, reader.Text, reader.Comparison).ShouldBe((0.5f, true, "verse · pad", "> 0.5"));
		pad.ConditionMet.ShouldBeFalse();
		reader.IsMet.ShouldBeFalse();

		model.Cues[0].Value = 0.8f;
		rig.Step(MusicRig.Bar);
		pad.ConditionMet.ShouldBeTrue();
		reader.IsMet.ShouldBeTrue();
		// A part with no condition is always met
		model.Tracks[0].Placements[0].ConditionMet.ShouldBeTrue();
	}

	[Test]
	public void Arrange_lays_each_tracks_parts_out_by_section_as_wide_as_the_timeline()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		var lead = model.Tracks[0];
		lead.Cells.Select(c => c.Placements.Select(p => p.Title).ToArray()).ShouldBe([["melody"], ["lead"]]);
		model.Tracks[1].Cells.Select(c => c.Placements.Count).ShouldBe([1, 0]);
		lead.Cells.Select(c => c.Width).ShouldBe(model.Sections.Select(s => s.Width));
		lead.Instrument.ShouldStartWith("patch ");
		// One pass of the melody drawn small: four quarter notes on three pitches
		var melody = lead.Placements[0];
		melody.Notes.Count.ShouldBe(4);
		melody.Rows.ShouldBe(3);
		melody.KindText.ShouldBe("notes");
	}

	[Test]
	public void A_jump_waits_for_the_next_bar_and_says_what_comes_next()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		rig.Step(MusicRig.Bar / 4);
		model.NextText.ShouldBe("Next: verse");
		model.Sections[0].Progress.ShouldBeGreaterThan(0f);

		rig.Workbench.JumpTo(1);
		// Long enough for the view model's refresh (30 a second), short of the bar's end
		rig.Step(MusicRig.Bar / 8);
		model.Sections[1].IsQueued.ShouldBeTrue();
		model.NextText.ShouldBe("Next: chorus, at the next bar");

		rig.Step(MusicRig.Bar);
		model.Sections[1].IsQueued.ShouldBeFalse();
		model.Sections[1].IsCurrent.ShouldBeTrue();
		model.Tracks[0].Cells[1].IsCurrent.ShouldBeTrue();
	}

	[Test]
	public void A_choice_from_the_library_takes_the_middle_and_folds_the_library_unless_it_is_pinned()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		model.Mode.ShouldBe(WorkbenchMode.Arrange);

		model.LibraryOpen = true;
		model.LibraryFlyout.ShouldBeTrue();
		model.Patches[0].IsSelected = true;
		model.Mode.ShouldBe(WorkbenchMode.Patch);
		model.IsPatch.ShouldBeTrue();
		model.LibraryOpen.ShouldBeFalse();
		model.DockLabel.ShouldBe("patch click");

		model.LibraryPinned = true;
		model.LibraryFlyout.ShouldBeFalse();
		model.Songs[1].IsSelected = true;
		model.Mode.ShouldBe(WorkbenchMode.Arrange);
		model.LibraryOpen.ShouldBeTrue();
	}

	[Test]
	public void The_librarys_filter_shows_only_what_has_every_word()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		model.LibraryQuery = "SEC";
		model.Songs.Select(s => s.IsVisible).ShouldBe([false, true]);
		model.Patches[0].IsVisible.ShouldBeFalse();
		model.LibraryQuery = "";
		model.Songs.Select(s => s.IsVisible).ShouldBe([true, true]);
	}

	[Test]
	public void The_palette_finds_by_every_word_and_runs_its_choice()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		model.PaletteOpen = true;
		model.PaletteItems.ShouldContain(i => i.Category == "Song" && i.Text == "second");
		model.PaletteItems[0].IsSelected.ShouldBeTrue();

		model.PaletteQuery = "song sec";
		var only = model.PaletteItems.ShouldHaveSingleItem();
		only.Text.ShouldBe("second");
		model.RunPalette();
		model.PaletteOpen.ShouldBeFalse();
		model.SelectedSong.ShouldBe(1);
		model.Mode.ShouldBe(WorkbenchMode.Arrange);
	}

	[Test]
	public void A_choice_of_part_gives_the_keyboard_its_track_and_is_kept_with_the_mode_and_pin()
	{
		WorkbenchState saved;
		using (var rig = new Rig())
		{
			var model = rig.Workbench.ViewModel;
			var pad = model.Tracks[1].Placements[0];
			pad.Select();
			pad.IsSelected.ShouldBeTrue();
			model.DockLabel.ShouldStartWith("pad · ");
			model.LibraryPinned = true;
			saved = rig.Workbench.CaptureState();
		}
		saved.SelectedPart.ShouldBe("verse/pad");
		saved.Mode.ShouldBe("Arrange");

		using var again = new Rig(saved);
		again.Workbench.ViewModel.Tracks[1].Placements[0].IsSelected.ShouldBeTrue();
		again.Workbench.ViewModel.LibraryPinned.ShouldBeTrue();
		again.Workbench.ViewModel.Mode.ShouldBe(WorkbenchMode.Arrange);
		again.Workbench.ViewModel.DockLabel.ShouldStartWith("pad · ");
	}

	[Test]
	public void The_window_lays_out_over_a_playing_song_in_every_mode_with_the_library_and_palette()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		var host = new SUIsei.UIHost();
		var view = new WorkbenchView(model);
		host.CreateRoot(SUIsei.UILayer.Default).Root.Content = view;
		var size = new System.Numerics.Vector2(1280f, 800f);
		void Frame()
		{
			rig.Step(MusicRig.Bar / 16);
			host.Tick(1.0 / 60.0, 1f, size);
			host.Render();
		}

		rig.Workbench.Play();
		Frame();
		model.Tracks[1].Placements[0].Select();
		Frame();
		model.LibraryOpen = true;
		Frame();
		model.LibraryPinned = true;
		Frame();
		model.PaletteOpen = true;
		Frame();
		model.PaletteQuery = "sec";
		Frame();
		model.PaletteOpen = false;
		model.Mode = WorkbenchMode.Patch;
		Frame();
		model.Mode = WorkbenchMode.Kit;
		Frame();
		view.LayoutRect.Width.ShouldBe(1280f);
		view.LayoutRect.Height.ShouldBe(800f);
	}

	[Test]
	public void Choosing_a_patch_opens_it_in_the_designer_as_a_graph_its_voice_and_its_code()
	{
		using var rig = new Rig();
		var designer = rig.Workbench.ViewModel.Designer;
		rig.Workbench.ChoosePatch(0);

		designer.Name.ShouldBe("click");
		designer.Nodes.ShouldHaveSingleItem().Summary.ShouldBe("SINE 1 kHz");
		designer.Selected.ShouldBe(0);
		designer.Inspector.ShouldContain(i => i.Label == "frequency" && i.IsSlider);
		designer.Code.ShouldContain("Patch.Build(\"click\"");
		designer.IsDirty.ShouldBeFalse();
		designer.HasProblems.ShouldBeFalse();
		designer.Stages.Select(s => s.Name).ShouldContain("Filter");
	}

	[Test]
	public void An_edit_is_heard_at_the_next_note_and_the_code_follows_it()
	{
		using var rig = new Rig();
		var designer = rig.Workbench.ViewModel.Designer;
		rig.Workbench.ChoosePatch(1);
		var ratio = designer.Inspector.First(i => i.Label == "ratio");
		ratio.Position = 2f;
		designer.Spec.Operators[0].Ratio.ShouldBe(2f);
		designer.IsDirty.ShouldBeTrue();
		designer.Code.ShouldContain("p.Sine(2f, Env.Gate)");

		rig.Session.Clear();
		var note = rig.Workbench.AuditionOn(57f, 0.8f);
		rig.Step(MusicRig.Rate / 4);
		rig.Workbench.AuditionOff(note);
		AudioAnalysis.PeakFrequency(rig.Session.Left(), MusicRig.Rate, 100, 2000).ShouldBe(440, 4);

		designer.RevertCommand.Execute(null);
		designer.IsDirty.ShouldBeFalse();
		designer.Spec.Operators[0].Ratio.ShouldBe(1f);
	}

	[Test]
	public void What_the_builder_refuses_is_shown_and_the_last_patch_that_built_plays_on()
	{
		using var rig = new Rig();
		var designer = rig.Workbench.ViewModel.Designer;
		rig.Workbench.ChoosePatch(1);
		designer.AddButtons.First(b => b.Label == "Modes").Command!.Execute(null);
		designer.Nodes.Count.ShouldBe(2);
		designer.HasProblems.ShouldBeFalse();   // a Modes is driven by the operator before it

		designer.Inspector.First(i => i.IsToggle && i.Label == "sine1").IsOn = false;
		designer.HasProblems.ShouldBeTrue();
		designer.Problems.ShouldContain("nothing ringing them");
		designer.Current.ShouldBeNull();

		designer.Inspector.First(i => i.IsToggle && i.Label == "sine1").IsOn = true;
		designer.HasProblems.ShouldBeFalse();
		designer.Inspector.First(i => i.IsAction && i.Label == "Delete").Command!.Execute(null);
		designer.Nodes.Count.ShouldBe(1);
	}

	[Test]
	public void A_rebuild_after_a_code_change_keeps_the_designers_edits_and_the_code_names_the_games_macros()
	{
		using var rig = new Rig();
		var designer = rig.Workbench.ViewModel.Designer;
		rig.Workbench.ChoosePatch(1);
		designer.Tab = DesignerTab.Macros;
		designer.Inspector.First(i => i.Label == "+ Macro").Command!.Execute(null);
		designer.Code.ShouldContain("p.Macro(Macros.Bright, MacroTarget.FilterOctaves, -1f, 1f);");

		Workbench.CodeChanged();
		rig.Step(256);
		designer.IsDirty.ShouldBeTrue();
		designer.Spec.Macros.Count.ShouldBe(1);
	}

	[Test]
	public void The_code_is_copied_through_the_host_and_says_so()
	{
		using var rig = new Rig();
		var copied = "";
		rig.Workbench.CopyText = text =>
		{
			copied = text;
			return true;
		};
		rig.Workbench.ChoosePatch(0);
		rig.Workbench.ViewModel.Designer.CopyCommand.Execute(null);
		copied.ShouldBe(rig.Workbench.ViewModel.Designer.Code);
		rig.Workbench.ViewModel.CopyStatus.ShouldStartWith("Copied at");
	}

	[Test]
	public void The_beat_sketcher_starts_from_a_groove_loops_through_the_sequencer_and_writes_Beat_notation()
	{
		using var rig = new Rig();
		var sketch = rig.Workbench.ViewModel.Sketch;
		rig.Workbench.ChooseKit(0);
		sketch.Rows.Select(r => r.Name).ShouldBe(["kick", "snare", "cowbell"]);
		sketch.Rows[0].Pattern.ShouldBe("x... .... x.x. ....");
		sketch.Rows[2].IsEmpty.ShouldBeTrue();

		sketch.Rows[2].Steps[0].Cycle.Execute(null);
		sketch.Rows[2].Steps[0].Cycle.Execute(null);
		sketch.Rows[2].Steps[0].Value.ShouldBe('X');
		sketch.Code.ShouldBe("Beat.Sixteenths(\n\t(Kit.Kick, \"x... .... x.x. ....\"),\n\t(Kit.Snare, \".... x... .... x...\"),\n\t(new DrumPiece(\"cowbell\"), \"X... .... .... ....\"))");

		sketch.IsLooping = true;
		rig.Session.Clear();
		rig.Step(MusicRig.Rate);
		MusicRig.Onsets(rig.Session.Left()).ShouldNotBeEmpty();
		sketch.Rows[0].Steps.Count(s => s.IsNow).ShouldBe(1);

		sketch.ClearCommand.Execute(null);
		rig.Step(MusicRig.Rate / 4);
		rig.Session.Clear();
		rig.Step(MusicRig.Rate / 2);
		MusicRig.Onsets(rig.Session.Left()).ShouldBeEmpty();
	}

	[Test]
	public void Its_view_models_steer_the_music()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.Tracks[0].Muted = true;
		rig.Session.Clear();
		rig.Step(MusicRig.Bar);
		MusicRig.Onsets(rig.Session.Left()).ShouldBeEmpty();
		// Unmuted, the same bar is heard: the silence was the mute's
		model.Tracks[0].Muted = false;
		rig.Session.Clear();
		rig.Step(MusicRig.Bar);
		MusicRig.Onsets(rig.Session.Left()).ShouldNotBeEmpty();

		model.Cues[0].Value = 1f;
		rig.Step(MusicRig.Bar);
		model.Tracks[1].Placements[0].IsActive.ShouldBeTrue();
	}

	[Test]
	public void A_choice_is_lit_by_the_model_and_kept_lit_until_another_is_chosen()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;

		model.Songs[1].IsSelected = true;
		model.SelectedSong.ShouldBe(1);
		model.Songs.Select(s => s.IsSelected).ShouldBe([false, true]);

		// A button bound to it unchecks it on a click; the model keeps it, as a radio button does
		model.Songs[1].IsSelected = false;
		model.Songs[1].IsSelected.ShouldBeTrue();

		model.Patches[0].IsSelected = true;
		model.SelectedPatch.ShouldBe(0);
		model.Patches[0].IsSelected.ShouldBeTrue();
	}

	[Test]
	public void A_part_held_on_or_off_shows_as_one_of_three()
	{
		using var rig = new Rig();
		var placement = rig.Workbench.ViewModel.Tracks[1].Placements[0];
		placement.IsAuto.ShouldBeTrue();

		placement.IsOn = true;
		placement.State.ShouldBe(PlacementState.On);
		(placement.IsAuto, placement.IsOn, placement.IsOff).ShouldBe((false, true, false));

		placement.IsOn = false;
		placement.State.ShouldBe(PlacementState.On);

		placement.IsOff = true;
		(placement.IsAuto, placement.IsOn, placement.IsOff).ShouldBe((false, false, true));
	}

	[Test]
	public void A_section_asked_for_lights_once_the_jump_is_heard()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		// The verse held, so the chorus comes only by the jump
		model.LoopSection = true;
		rig.Workbench.Play();
		rig.Step(MusicRig.Bar / 4);
		model.Sections[0].IsCurrent.ShouldBeTrue();

		model.Sections[1].IsCurrent = true;
		model.Sections[1].IsCurrent.ShouldBeFalse();

		rig.Step(MusicRig.Bar);
		model.Sections[1].IsCurrent.ShouldBeTrue();
		model.Sections[0].IsCurrent.ShouldBeFalse();
	}

	[Test]
	public void A_song_that_fails_to_build_shows_its_error_and_the_old_one_plays_on()
	{
		using var rig = new Rig();
		rig.Workbench.Play();
		rig.Step(MusicRig.Bar / 2);
		rig.Broken = true;
		rig.Workbench.Rebuild();

		rig.Workbench.ViewModel.Error.ShouldContain("isn't a note");
		rig.Step(MusicRig.Bar);
		rig.Workbench.Player.IsPlaying.ShouldBeTrue();
		rig.Workbench.Player.Position.AbsoluteBar.ShouldBe(1);

		rig.Broken = false;
		rig.Workbench.Rebuild();
		rig.Workbench.ViewModel.Error.ShouldBe("");
	}

	[Test]
	public void A_rebuild_keeps_mutes_and_forced_parts()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.Tracks[0].Muted = true;
		model.Tracks[1].Placements[0].State = PlacementState.On;
		rig.Degree = 2;
		rig.Workbench.Rebuild();

		model.Tracks[0].Muted.ShouldBeTrue();
		model.Tracks[1].Placements[0].State.ShouldBe(PlacementState.On);
		rig.Session.Clear();
		rig.Step(2 * MusicRig.Bar);
		// The lead's click (1000 Hz) stays muted; the pad, forced on, sounds
		AudioAnalysis.LevelAt(rig.Session.Left(), 1000, MusicRig.Rate).ShouldBeLessThan(0.001f);
		AudioAnalysis.LevelAt(rig.Session.Left(), 261.63, MusicRig.Rate).ShouldBeGreaterThan(0.05f);
		model.Tracks[1].Placements[0].IsActive.ShouldBeTrue();
	}

	[Test]
	public void Hot_reload_from_another_thread_rebuilds_on_the_next_update()
	{
		using var rig = new Rig();
		var before = rig.Builds;
		var thread = new Thread(Workbench.CodeChanged);
		thread.Start();
		thread.Join();
		rig.Workbench.Update(0.01);

		rig.Builds.ShouldBe(before + 1);
	}

	[Test]
	public void Its_state_comes_back_in_a_new_workbench()
	{
		WorkbenchState saved;
		using (var rig = new Rig())
		{
			var model = rig.Workbench.ViewModel;
			rig.Workbench.Play();
			model.LoopSection = true;
			model.Cues[0].Value = 0.75f;
			model.Tracks[1].Muted = true;
			model.Tracks[0].Placements[1].State = PlacementState.Off;
			saved = rig.Workbench.CaptureState();
		}

		using var again = new Rig(saved);
		var restored = again.Workbench.ViewModel;
		restored.SelectedSong.ShouldBe(0);
		restored.IsPlaying.ShouldBeTrue();
		restored.LoopSection.ShouldBeTrue();
		restored.Cues[0].Value.ShouldBe(0.75f);
		restored.Tracks[1].Muted.ShouldBeTrue();
		restored.Tracks[0].Placements[1].State.ShouldBe(PlacementState.Off);
		again.Workbench.Player.Get(Energy).ShouldBe(0.75f);
	}

	[Test]
	public void A_pitched_parts_pattern_has_a_row_per_pitch_and_a_note_per_event()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		model.Tracks[0].Placements[0].Select();

		var pattern = model.Pattern;
		pattern.RowLabels.ShouldBe(["G4", "E4", "C4"]);
		pattern.Notes.Count.ShouldBe(4);
		pattern.Notes.Select(n => n.Row).ShouldBe([2, 1, 0, 1]);
		pattern.LengthTicks.ShouldBe(3840);
	}
}
