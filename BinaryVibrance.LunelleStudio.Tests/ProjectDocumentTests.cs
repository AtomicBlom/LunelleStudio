namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// The editing model (docs/spec/transactions.md): edits on a document over the project's files, undone and redone,
/// joined into one step as they stream, saved, recovered from the journal, and changes made outside the studio kept apart.
/// </summary>
public class ProjectDocumentTests
{
	private sealed class Rig
	{
		public readonly string Root = Path.Combine(Path.GetTempPath(), "lunelle-document-" + Guid.NewGuid().ToString("N")[..8]);
		public readonly MemoryFiles Disk = new();
		public DateTime Now = new(2026, 11, 27);

		public Rig() => Disk.WriteText(File("Songs/a.song.json"), "a0");

		public string File(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));

		public ProjectDocument Open() => ProjectDocument.Open(Disk, Root, () => Now);
	}

	[Test]
	public void An_edit_is_undone_and_redone_and_the_disk_is_untouched_until_a_save()
	{
		var rig = new Rig();
		var document = rig.Open();
		var song = rig.File("Songs/a.song.json");

		document.WriteText(song, "a1");
		document.Seal();
		document.WriteText(song, "a2");
		document.ReadText(song).ShouldBe("a2");
		document.IsDirty.ShouldBeTrue();
		rig.Disk.ReadText(song).ShouldBe("a0");

		document.Undo().ShouldBeTrue();
		document.ReadText(song).ShouldBe("a1");
		document.Undo().ShouldBeTrue();
		document.ReadText(song).ShouldBe("a0");
		document.IsDirty.ShouldBeFalse();
		document.Undo().ShouldBeFalse();

		document.Redo().ShouldBeTrue();
		document.Redo().ShouldBeTrue();
		document.ReadText(song).ShouldBe("a2");
		document.Redo().ShouldBeFalse();

		// A new edit after an undo drops what could have been redone
		document.Undo();
		document.WriteText(song, "b");
		document.CanRedo.ShouldBeFalse();
		rig.Disk.ReadText(song).ShouldBe("a0");
	}

	[Test]
	public void Writes_to_one_file_within_two_seconds_are_one_step_until_sealed()
	{
		var rig = new Rig();
		var document = rig.Open();
		var song = rig.File("Songs/a.song.json");
		for (var i = 1; i <= 10; i++)
		{
			document.WriteText(song, $"drag {i}");
			rig.Now += TimeSpan.FromMilliseconds(500);
		}
		document.WriteText(song, "after a pause");
		document.Undo();
		document.ReadText(song).ShouldBe("a0");

		document.Redo();
		rig.Now += TimeSpan.FromSeconds(3);
		document.WriteText(song, "later");
		document.Undo();
		document.ReadText(song).ShouldBe("after a pause");
	}

	[Test]
	public void A_transaction_is_undone_whole_and_a_file_it_made_is_deleted()
	{
		var rig = new Rig();
		var document = rig.Open();
		using (document.Transaction("extract pattern"))
		{
			document.WriteText(rig.File("Songs/a.song.json"), "uses the pattern");
			document.WriteText(rig.File("Patterns/groove.pattern.json"), "the pattern");
			document.WriteText(rig.File("Songs/a.song.json"), "uses the pattern, tidied");
		}
		document.List(rig.Root).Where(p => !p.StartsWith(".lunelle-studio/")).ShouldBe(["Patterns/groove.pattern.json", "Songs/a.song.json"]);
		document.Undo().ShouldBeTrue();
		document.Exists(rig.File("Patterns/groove.pattern.json")).ShouldBeFalse();
		document.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a0");
		document.List(rig.Root).Where(p => !p.StartsWith(".lunelle-studio/")).ShouldBe(["Songs/a.song.json"]);
		document.Redo();
		document.ReadText(rig.File("Songs/a.song.json")).ShouldBe("uses the pattern, tidied");

		// A deletion is a step too
		document.Delete(rig.File("Songs/a.song.json"));
		document.Exists(rig.File("Songs/a.song.json")).ShouldBeFalse();
		document.Undo();
		document.ReadText(rig.File("Songs/a.song.json")).ShouldBe("uses the pattern, tidied");
	}

	[Test]
	public void A_save_writes_the_edits_and_clears_the_log()
	{
		var rig = new Rig();
		var document = rig.Open();
		document.WriteText(rig.File("Songs/a.song.json"), "a1");
		document.WriteText(rig.File("Songs/b.song.json"), "b1");
		rig.Disk.Exists(rig.File(ProjectDocument.JournalPath)).ShouldBeTrue();
		document.Save();
		rig.Disk.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a1");
		rig.Disk.ReadText(rig.File("Songs/b.song.json")).ShouldBe("b1");
		rig.Disk.Exists(rig.File(ProjectDocument.JournalPath)).ShouldBeFalse();
		document.IsDirty.ShouldBeFalse();
		document.Undo().ShouldBeFalse();

		// Revert drops the edits since
		document.WriteText(rig.File("Songs/a.song.json"), "a2");
		document.Revert();
		document.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a1");
		document.IsDirty.ShouldBeFalse();
	}

	[Test]
	public void Unsaved_edits_are_recovered_from_the_journal_and_still_undo()
	{
		var rig = new Rig();
		var first = rig.Open();
		first.WriteText(rig.File("Songs/a.song.json"), "a1");
		first.Seal();
		first.WriteText(rig.File("Songs/a.song.json"), "a2");
		first.Seal();
		first.WriteText(rig.File("Songs/b.song.json"), "b1");
		first.Undo();

		var again = rig.Open();
		again.Recovered[0].ShouldStartWith("Recovered 1 file");
		again.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a2");
		again.Exists(rig.File("Songs/b.song.json")).ShouldBeFalse();
		again.Redo().ShouldBeTrue();
		again.ReadText(rig.File("Songs/b.song.json")).ShouldBe("b1");
		again.Undo();
		again.Undo();
		again.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a1");

		// Recovered a third time, it is where the second left it
		var third = rig.Open();
		third.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a1");
		third.Undo();
		third.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a0");

		// A file changed on disk since its unsaved changes is left out, and said
		rig.Disk.WriteText(rig.File("Songs/a.song.json"), "changed elsewhere");
		var fourth = rig.Open();
		fourth.ReadText(rig.File("Songs/a.song.json")).ShouldBe("changed elsewhere");
		fourth.Recovered.ShouldContain(r => r.Contains("Songs/a.song.json was changed outside the studio"));
	}

	[Test]
	public void A_change_outside_to_an_unedited_file_is_heard_and_to_an_edited_one_is_noticed()
	{
		var rig = new Rig();
		var document = rig.Open();
		var heard = new List<string>();
		using var watch = document.Watch(rig.Root, heard.Add);

		rig.Disk.WriteText(rig.File("Songs/b.song.json"), "b from git");
		heard.ShouldBe([rig.File("Songs/b.song.json")]);
		document.ReadText(rig.File("Songs/b.song.json")).ShouldBe("b from git");

		document.WriteText(rig.File("Songs/a.song.json"), "a1");
		heard.Count.ShouldBe(2);
		rig.Disk.WriteText(rig.File("Songs/a.song.json"), "a from git");
		heard.Count.ShouldBe(2);
		document.ReadText(rig.File("Songs/a.song.json")).ShouldBe("a1");
		document.ChangedOutside.ShouldBe(["Songs/a.song.json"]);

		// The workbench's own state is written straight through
		document.WriteText(rig.File(".lunelle-studio/workbench.json"), "{}");
		rig.Disk.Exists(rig.File(".lunelle-studio/workbench.json")).ShouldBeTrue();
	}
}
