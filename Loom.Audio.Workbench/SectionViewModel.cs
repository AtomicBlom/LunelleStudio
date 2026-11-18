namespace Loom.Audio.Workbench;

/// <summary>A section of the song, for the section strip: lit while it plays; a click jumps to it at the next bar.</summary>
public sealed class SectionViewModel : ViewModel
{
	private readonly Workbench _workbench;
	private bool _isCurrent;
	private bool _loops;

	internal SectionViewModel(Workbench workbench, int index, string name, int bars)
	{
		_workbench = workbench;
		Index = index;
		Name = name;
		Bars = bars;
	}

	/// <summary>Its name.</summary>
	public string Name { get; }

	/// <summary>How many bars long it is.</summary>
	public int Bars { get; }

	/// <summary>Its index in the song.</summary>
	public int Index { get; }

	/// <summary>
	/// Whether it is playing. Setting it true asks to go to it at the next bar line, which shows once it is heard; setting
	/// it false does nothing, since a section stops by another starting.
	/// </summary>
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

	/// <summary>Its name and length, as its button shows it: "verse (8)".</summary>
	public string Label => $"{Name} ({Bars})";

	/// <summary>Whether it is playing and held by the loop.</summary>
	public bool Loops
	{
		get => _loops;
		internal set => Set(ref _loops, value);
	}

	/// <summary>Goes to it at the next bar line.</summary>
	public void Jump() => _workbench.JumpTo(Index);
}
