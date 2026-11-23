namespace BinaryVibrance.LunelleStudio;

/// <summary>What a part added to a track starts as.</summary>
public enum NewPartKind
{
	/// <summary>A line of notes by the scale's degrees, empty: a melody to write in the roll.</summary>
	Melody,

	/// <summary>A line an octave down that follows the chords, degree 1 each bar's root: a bass line.</summary>
	Bass,

	/// <summary>A strum of each step's chord across a guitar's strings, in the open voicing.</summary>
	Strum,

	/// <summary>Block chords from the progression, as on keys: a strum in the close voicing with no spread, a chord each half bar.</summary>
	Chords,
}
