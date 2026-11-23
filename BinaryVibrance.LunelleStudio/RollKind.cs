namespace BinaryVibrance.LunelleStudio;

/// <summary>What the roll edits: nothing (the part is code, or the song has no file), a line, a beat or a strum.</summary>
public enum RollKind
{
	/// <summary>Nothing it can edit.</summary>
	None,

	/// <summary>A line's notes, by degree.</summary>
	Line,

	/// <summary>A beat's rows of hits.</summary>
	Beat,

	/// <summary>A strum's strokes.</summary>
	Strum,
}
