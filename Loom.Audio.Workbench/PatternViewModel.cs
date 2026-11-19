namespace Loom.Audio.Workbench;

/// <summary>The selected placement as a grid: its notes over one pass of its part, and where the song is in it.</summary>
public sealed class PatternViewModel : ViewModel
{
	private string _title = "Choose a part to see it";
	private int _lengthTicks;
	private int _ticksPerBar = 3840;
	private IReadOnlyList<PatternNote> _notes = [];
	private IReadOnlyList<string> _rowLabels = [];
	private int _playheadTick = -1;

	/// <summary>What it shows.</summary>
	public string Title
	{
		get => _title;
		private set => Set(ref _title, value);
	}

	/// <summary>One pass's length, in ticks.</summary>
	public int LengthTicks
	{
		get => _lengthTicks;
		private set => Set(ref _lengthTicks, value);
	}

	/// <summary>A bar's length, in ticks.</summary>
	public int TicksPerBar
	{
		get => _ticksPerBar;
		private set => Set(ref _ticksPerBar, value);
	}

	/// <summary>Its notes.</summary>
	public IReadOnlyList<PatternNote> Notes
	{
		get => _notes;
		private set
		{
			if (Set(ref _notes, value))
				Changed(nameof(HasNotes));
		}
	}

	/// <summary>Whether it has a part's notes to show.</summary>
	public bool HasNotes => _notes.Count > 0;

	/// <summary>Each row's label: a note's name (C4), or a piece's id.</summary>
	public IReadOnlyList<string> RowLabels
	{
		get => _rowLabels;
		private set => Set(ref _rowLabels, value);
	}

	/// <summary>Where the song is in the pass, in ticks; −1 when the part isn't playing.</summary>
	public int PlayheadTick
	{
		get => _playheadTick;
		internal set => Set(ref _playheadTick, value);
	}

	/// <summary>Shows nothing: no part is chosen.</summary>
	internal void Clear() => Show("", 0, 0, [], []);

	internal void Show(string title, int lengthTicks, int ticksPerBar, IReadOnlyList<PatternNote> notes, IReadOnlyList<string> rowLabels)
	{
		// The notes and rows first, so a view redrawing on the length sees them
		Notes = notes;
		RowLabels = rowLabels;
		TicksPerBar = ticksPerBar;
		LengthTicks = lengthTicks;
		Title = title;
	}
}
