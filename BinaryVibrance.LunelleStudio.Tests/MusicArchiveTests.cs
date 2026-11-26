using System.IO.Compression;
using System.Text;

using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.LunelleStudio.Projects;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>
/// A project of files alone as a <c>.lunelle</c> file: packed whole without what the workbench was doing, unpacked to the
/// same files and read as the same music, from a zip of its folder too, and nothing taken from outside a project.
/// </summary>
public class MusicArchiveTests
{
	private static byte[] Zip(params (string Path, string Text)[] entries)
	{
		using var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
			foreach (var (path, text) in entries)
			{
				using var into = zip.CreateEntry(path).Open();
				into.Write(Encoding.UTF8.GetBytes(text));
			}
		return stream.ToArray();
	}

	[Test]
	public void A_project_packed_and_unpacked_is_the_same_music_without_the_workbench_s_state()
	{
		var folder = Path.Combine(Path.GetTempPath(), "lunelle-studio-archive-" + Guid.NewGuid().ToString("N")[..8], "Packed");
		ProjectTemplate.CreateData(folder);
		DiskFiles.Instance.WriteText(Path.Combine(folder, ".lunelle-studio", "workbench.json"), "{}");
		var written = DiskFiles.Instance.List(folder).Where(p => !p.StartsWith(".lunelle-studio/", StringComparison.Ordinal)).Order().ToList();

		var archive = MusicArchive.Pack(DiskFiles.Instance, folder);
		var files = MusicArchive.Unpack(archive);

		files.Select(f => f.Path).Order().ShouldBe(written);
		foreach (var (path, bytes) in files)
			bytes.ShouldBe(File.ReadAllBytes(Path.Combine(folder, path)));
		var unpacked = MusicFolder.Load(files.Select(f => f.Path), path => files.Single(f => f.Path == path).Bytes);
		var original = MusicFolder.FromDirectory(folder);
		unpacked.Name.ShouldBe("Packed");
		unpacked.Songs.Select(s => s.Name).ShouldBe(original.Songs.Select(s => s.Name));
		unpacked.Patches.Select(p => p.Name).ShouldBe(original.Patches.Select(p => p.Name));
		unpacked.Kits.Select(k => k.Name).ShouldBe(original.Kits.Select(k => k.Name));
		unpacked.Problems.ShouldBeEmpty();
	}

	[Test]
	public void A_file_opened_twice_unpacks_into_one_folder_and_another_by_its_name_into_the_next()
	{
		var home = Path.Combine(Path.GetTempPath(), "lunelle-studio-home-" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			var first = Zip(("lunelle-project.json", "{}"), ("Songs/a.song.json", "{\"tempo\": 90}"));
			var other = Zip(("lunelle-project.json", "{}"), ("Songs/b.song.json", "{}"));

			var folder = MusicArchive.UnpackBeside(DiskFiles.Instance, home, "Tunes", first);
			folder.ShouldBe(Path.Combine(home, "Tunes"));
			File.ReadAllText(Path.Combine(folder, "Songs", "a.song.json")).ShouldBe("{\"tempo\": 90}");
			// What the workbench keeps beside the music doesn't make it another project's
			DiskFiles.Instance.WriteText(Path.Combine(folder, ".lunelle-studio", "workbench.json"), "{}");
			MusicArchive.UnpackBeside(DiskFiles.Instance, home, "Tunes", first).ShouldBe(folder);

			MusicArchive.UnpackBeside(DiskFiles.Instance, home, "Tunes", other).ShouldBe(Path.Combine(home, "Tunes (2)"));
			// An edit made since is kept: the file opens beside it rather than over it
			File.WriteAllText(Path.Combine(folder, "Songs", "a.song.json"), "{\"tempo\": 120}");
			MusicArchive.UnpackBeside(DiskFiles.Instance, home, "Tunes", first).ShouldBe(Path.Combine(home, "Tunes (3)"));
			File.ReadAllText(Path.Combine(folder, "Songs", "a.song.json")).ShouldBe("{\"tempo\": 120}");
		}
		finally
		{
			if (Directory.Exists(home))
				Directory.Delete(home, true);
		}
	}

	[Test]
	public void A_zip_of_the_folder_holding_a_project_is_taken_from_that_folder()
	{
		var files = MusicArchive.Unpack(Zip(
			("Tunes/lunelle-project.json", "{}"),
			("Tunes/Songs/a.song.json", "{}"),
			("Tunes/.lunelle-studio/workbench.json", "{}"),
			("__MACOSX/Tunes/._lunelle-project.json", "")));
		files.Select(f => f.Path).ShouldBe(["lunelle-project.json", "Songs/a.song.json"], ignoreOrder: true);
	}

	[Test]
	public void Nothing_is_taken_from_a_zip_that_isn_t_a_project_or_reaches_outside_one()
	{
		Should.Throw<InvalidDataException>(() => MusicArchive.Unpack(Zip(("Songs/a.song.json", "{}"))));
		Should.Throw<InvalidDataException>(() => MusicArchive.Unpack(Zip(("lunelle-project.json", "{}"), ("../evil.json", "{}"))));
		Should.Throw<InvalidDataException>(() => MusicArchive.Unpack(Zip(("a/b/lunelle-project.json", "{}"))));
		Should.Throw<InvalidDataException>(() => MusicArchive.Unpack(Encoding.UTF8.GetBytes("not a zip")));
	}

	[Test]
	public void A_project_s_file_is_named_for_it()
	{
		MusicArchive.FileName("Night Drive").ShouldBe("Night Drive.lunelle");
		MusicArchive.FileName("a/b:c?").ShouldBe("abc.lunelle");
		MusicArchive.FileName("  ").ShouldBe("music.lunelle");
	}
}

