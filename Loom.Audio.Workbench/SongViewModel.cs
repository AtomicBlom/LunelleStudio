using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>The song itself, for the inspector: its tempo, and the key and scale it is written in.</summary>
public sealed class SongViewModel : ViewModel
{
	private readonly Workbench _workbench;

	internal SongViewModel(Workbench workbench, Song song)
	{
		_workbench = workbench;
		Name = song.Name;
		Tempo = song.Tempo;
		Root = SongFile.ScaleRoots[(((int)song.Scale.Root % 12) + 12) % 12];
		ScaleName = song.Scale.Name;
		Meter = song.Meter.ToString();
	}

	/// <summary>Its name.</summary>
	public string Name { get; }

	/// <summary>Its tempo, in beats a minute.</summary>
	public double Tempo { get; }

	/// <summary>Its key's root: "C", "F#".</summary>
	public string Root { get; }

	/// <summary>Its scale over that root: "major", "dorian".</summary>
	public string ScaleName { get; }

	/// <summary>Its time signature: "4/4".</summary>
	public string Meter { get; }

	/// <summary>Whether it is what the inspector shows.</summary>
	public bool IsInspected
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Whether the song is a file the workbench edits, so its tempo and scale can be changed.</summary>
	public bool CanEdit => _workbench.CanEditSong;

	/// <summary>Sets its tempo, in beats a minute.</summary>
	public void SetTempo(double bpm) => _workbench.SetTempo(bpm);

	/// <summary>Sets its key and scale: a root of <see cref="SongFile.ScaleRoots"/>, a scale of <see cref="SongFile.ScaleNames"/>.</summary>
	public void SetScale(string root, string scale) => _workbench.SetScale(root, scale);
}
