namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// One place a level cue is waited on: a part whose condition compares the cue with a level. The cue panel marks each
/// level on the cue's slider and lists what reads it, lit while it holds.
/// </summary>
public sealed class CueReaderViewModel : ViewModel
{
	private bool _isMet;

	internal CueReaderViewModel(float level, bool above, string text)
	{
		Level = level;
		Above = above;
		Text = text;
	}

	/// <summary>The level compared with, 0 to 1.</summary>
	public float Level { get; }

	/// <summary>Whether it waits for the cue above the level (else below).</summary>
	public bool Above { get; }

	/// <summary>What waits on it: "cruise · busy hats".</summary>
	public string Text { get; }

	/// <summary>The comparison as written: "> 0.6", "&lt; 0.2".</summary>
	public string Comparison => (Above ? "> " : "< ") + Level.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

	/// <summary>Whether the comparison holds for the cue as it is now.</summary>
	public bool IsMet
	{
		get => _isMet;
		internal set => Set(ref _isMet, value);
	}
}
