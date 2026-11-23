namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// One of the song's sections on Arrange's timeline: its name, bars and chords bar by bar, its exits, lit while it plays
/// with the bar playing and how far through it is, marked while a jump to it waits for the next bar. Choosing it shows it
/// in the inspector; <see cref="Jump"/> plays it from the next bar.
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

	/// <summary>How far Arrange's lanes are scrolled, which its header follows down.</summary>
	public ArrangeScroll Scroll => _workbench.ViewModel.ArrangeScroll;

	/// <summary>It, for a view built from all of it at once.</summary>
	public SectionViewModel Self => this;

	/// <summary>Its chords bar by bar as written; fewer than its bars repeat to fill it.</summary>
	public IReadOnlyList<BinaryVibrance.Lunelle.Music.ChordBar> Progression { get; internal init; } = [];

	/// <summary>The scale the song plays in, which spells its chords.</summary>
	public BinaryVibrance.Lunelle.Music.Scale Scale { get; internal init; } = BinaryVibrance.Lunelle.Music.Scale.Major(BinaryVibrance.Lunelle.Music.Note.C);

	/// <summary>Its exits as a song file writes them, in the order they are tried: a condition (null for always) and where to.</summary>
	public IReadOnlyList<(string? When, string Then)> Exits { get; internal init; } = [];

	/// <summary>Its exits in a line: "→ open when intensity &gt; 0.7 · → cruise"; "plays again" with none.</summary>
	public string ExitsText => Exits.Count == 0
		? "↻ repeats"
		: string.Join("  ·  ", Exits.Select(e => e.When is { } when ? $"→ {e.Then} when {when}" : $"→ {e.Then}"));

	/// <summary>The width of one of its bars on the ruler.</summary>
	public float BarWidth => Width / Bars;

	/// <summary>Which of its exits would be taken were it ending now, or -1; while it plays.</summary>
	public int NextExit
	{
		get;
		internal set
		{
			if (Set(ref field, value))
				Changed(nameof(NextText));
		}
	} = -1;

	/// <summary>Where it goes next, as it stands: "→ open (exit 1)"; empty while it isn't playing.</summary>
	public string NextText => !_isCurrent ? "" : NextExit >= 0 && NextExit < Exits.Count ? $"next → {Exits[NextExit].Then}" : "next ↻ again";

	/// <summary>The bar of it playing, from 0, or -1 while it isn't.</summary>
	public int PlayingBar
	{
		get;
		internal set => Set(ref field, value);
	} = -1;

	/// <summary>Whether it is what the inspector shows.</summary>
	public bool IsInspected
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Shows it in the inspector.</summary>
	public void Inspect() => _workbench.Inspect(this);

	/// <summary>Sets its chords; why they can't be taken, or null.</summary>
	public string? SetChords(string chords) => _workbench.SetChords(Index, chords);

	/// <summary>Sets how many bars it is.</summary>
	public void SetBars(int bars) => _workbench.SetBars(Index, bars);

	/// <summary>Renames it; why it can't be, or null.</summary>
	public string? Rename(string name) => _workbench.RenameSection(Index, name);

	/// <summary>Sets its exits; why they can't be taken, or null.</summary>
	public string? SetExits(IReadOnlyList<(string? When, string Then)> exits) => _workbench.SetExits(Index, exits);

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

	internal void ShowCurrent(bool current)
	{
		if (Set(ref _isCurrent, current, nameof(IsCurrent)))
			Changed(nameof(NextText));
	}

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
