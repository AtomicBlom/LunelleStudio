namespace Loom.Audio.Workbench;

/// <summary>
/// One of the song's sections on Arrange's timeline: its name, bars and chords, lit while it plays with how far through
/// it is, marked while a jump to it waits for the next bar. Choosing it jumps there at the next bar.
/// </summary>
public sealed class SectionViewModel : ViewModel
{
	private readonly Workbench _workbench;
	private bool _isCurrent;
	private bool _loops;
	private bool _isQueued;
	private float _progress;

	internal SectionViewModel(Workbench workbench, int index, string name, int bars, string chordText)
	{
		_workbench = workbench;
		Index = index;
		Name = name;
		Bars = bars;
		ChordText = chordText;
		Width = ArrangeLayout.SectionWidth(bars);
	}

	/// <summary>Its name.</summary>
	public string Name { get; }

	/// <summary>How many bars long it is.</summary>
	public int Bars { get; }

	/// <summary>Its index in the song: what 1 to 9 jump to.</summary>
	public int Index { get; }

	/// <summary>Its chords as written: "I | IV | V vi | I".</summary>
	public string ChordText { get; }

	/// <summary>How wide it is drawn: as wide as its bars, and its lanes' columns with it.</summary>
	public float Width { get; }

	/// <summary>"2 · 8 bars": its key and its length.</summary>
	public string Detail => Index < 9 ? $"{Index + 1} · {Bars} bars" : $"{Bars} bars";

	/// <summary>Whether it is the section playing. Setting it true jumps there at the next bar.</summary>
	public bool IsCurrent
	{
		get => _isCurrent;
		set
		{
			if (value && !_isCurrent)
				Jump();
		}
	}

	internal void ShowCurrent(bool current) => Set(ref _isCurrent, current, nameof(IsCurrent));

	/// <summary>A label: "verse (8)".</summary>
	public string Label => $"{Name} ({Bars})";

	/// <summary>Whether it is held, looping.</summary>
	public bool Loops
	{
		get => _loops;
		internal set => Set(ref _loops, value);
	}

	/// <summary>Whether a jump to it waits for the next bar.</summary>
	public bool IsQueued
	{
		get => _isQueued;
		internal set => Set(ref _isQueued, value);
	}

	/// <summary>How far through it the song is, 0 to 1, while it plays; 0 otherwise.</summary>
	public float Progress
	{
		get => _progress;
		internal set
		{
			if (Set(ref _progress, value))
				Changed(nameof(ProgressWidth));
		}
	}

	/// <summary>The width of its progress bar.</summary>
	public float ProgressWidth => Width * _progress;

	/// <summary>Jumps to it at the next bar.</summary>
	public void Jump() => _workbench.JumpTo(Index);
}
