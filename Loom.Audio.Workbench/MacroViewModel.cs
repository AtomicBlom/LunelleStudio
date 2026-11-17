namespace Loom.Audio.Workbench;

/// <summary>One of the bench patch's eight macros, 0 to 1, for a slider. Setting it acts.</summary>
public sealed class MacroViewModel : ViewModel
{
	private readonly Action<int, float> _set;
	private float _value;

	internal MacroViewModel(int index, Action<int, float> set)
	{
		Index = index;
		Name = $"macro {index}";
		_set = set;
	}

	/// <summary>Which macro it is.</summary>
	public int Index { get; }

	/// <summary>Its label.</summary>
	public string Name { get; }

	/// <summary>Its value, 0 to 1. Setting it tells the bench.</summary>
	public float Value
	{
		get => _value;
		set
		{
			if (Set(ref _value, Math.Clamp(value, 0f, 1f)))
				_set(Index, _value);
		}
	}
}
