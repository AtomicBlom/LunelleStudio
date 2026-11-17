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
				Patches = [("click", () => MusicRig.Click)],
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
	public void Its_view_models_steer_the_music()
	{
		using var rig = new Rig();
		var model = rig.Workbench.ViewModel;
		rig.Workbench.Play();
		model.Tracks[0].Muted = true;
		rig.Session.Clear();
		rig.Step(MusicRig.Bar);
		MusicRig.Onsets(rig.Session.Left()).ShouldBeEmpty();

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
