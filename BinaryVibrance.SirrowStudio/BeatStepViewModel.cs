using System.Windows.Input;

namespace BinaryVibrance.SirrowStudio;

/// <summary>One step of the beat sketcher: off, a hit, an accent or a ghost note, as Beat notation writes them.</summary>
public sealed class BeatStepViewModel : ViewModel
{
	private static readonly Dictionary<char, char> Next = new() { ['.'] = 'x', ['x'] = 'X', ['X'] = 'o', ['o'] = '.' };
	private readonly Action _changed;

	internal BeatStepViewModel(int index, char value, Action changed)
	{
		Index = index;
		Value = value;
		_changed = changed;
	}

	/// <summary>Its step, 0 to 15.</summary>
	public int Index { get; }

	/// <summary>Whether it starts a beat, drawn a little apart.</summary>
	public bool IsBeat => Index % 4 == 0;

	/// <summary>What it plays: <c>.</c>, <c>x</c>, <c>X</c> or <c>o</c>.</summary>
	public char Value
	{
		get;
		internal set
		{
			if (Set(ref field, value))
				Changed(nameof(Text));
		}
	}

	/// <summary>What it shows: its character, or nothing when off.</summary>
	public string Text => Value == '.' ? "" : Value.ToString();

	/// <summary>Whether the sketch is playing it now.</summary>
	public bool IsNow
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Turns it to the next: off, hit, accent, ghost, off.</summary>
	public ICommand Cycle => field ??= new ActionCommand(() =>
	{
		Value = Next[Value];
		_changed();
	});
}
