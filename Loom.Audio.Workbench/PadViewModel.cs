using System.Windows.Input;

namespace Loom.Audio.Workbench;

/// <summary>A pad of the bench's kit: a piece, hit when it is pressed.</summary>
public sealed class PadViewModel
{
	internal PadViewModel(string piece, Action hit)
	{
		Piece = piece;
		Hit = new ActionCommand(hit);
	}

	/// <summary>The piece it plays, by id.</summary>
	public string Piece { get; }

	/// <summary>Hits it, at the bench's velocity.</summary>
	public ICommand Hit { get; }
}
