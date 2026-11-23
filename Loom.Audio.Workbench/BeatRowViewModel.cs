using System.Collections.ObjectModel;

using BinaryVibrance.Sirrow.Synth.Chip;

namespace Loom.Audio.Workbench;

/// <summary>A piece of the kit across the beat sketcher's sixteen steps.</summary>
public sealed class BeatRowViewModel
{
	internal BeatRowViewModel(DrumPiece piece, string pattern, Action changed)
	{
		Piece = piece;
		for (var i = 0; i < 16; i++)
			Steps.Add(new BeatStepViewModel(i, i < pattern.Length ? pattern[i] : '.', changed));
	}

	/// <summary>The piece it plays.</summary>
	public DrumPiece Piece { get; }

	/// <summary>Its name, as the kit has it.</summary>
	public string Name => Piece.Id;

	/// <summary>Its sixteen steps.</summary>
	public ObservableCollection<BeatStepViewModel> Steps { get; } = [];

	/// <summary>Its steps as Beat notation, four to a group: "x... ..x. x... ....".</summary>
	public string Pattern
	{
		get
		{
			var text = new string([.. Steps.Select(s => s.Value)]);
			return string.Join(' ', Enumerable.Range(0, 4).Select(g => text.Substring(g * 4, 4)));
		}
	}

	/// <summary>Whether it plays anything.</summary>
	public bool IsEmpty => Steps.All(s => s.Value == '.');
}
