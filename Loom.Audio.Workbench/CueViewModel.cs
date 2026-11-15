using Loom.Audio.Synth.Music;

namespace Loom.Audio.Workbench;

/// <summary>A cue, for the cue panel: a level from 0 to 1 on a slider, or a choice among options. Setting it acts.</summary>
public sealed class CueViewModel : ViewModel
{
	private readonly Workbench _workbench;
	private float _value;
	private int _selectedOption;

	internal CueViewModel(Workbench workbench, CueBase cue, float value)
	{
		_workbench = workbench;
		Cue = cue;
		Options = cue.OptionNames;
		_value = value;
		_selectedOption = Math.Max(0, OptionIndex(cue, value));
	}

	// The option of a choice whose value is `value`, or −1
	internal static int OptionIndex(CueBase cue, float value)
	{
		var count = cue.OptionNames.Length;
		for (var i = 0; i < count; i++)
			if (cue.OptionValue(i) == value)
				return i;
		return -1;
	}

	internal CueBase Cue { get; }

	/// <summary>Its name.</summary>
	public string Name => Cue.Name;

	/// <summary>Whether it is a choice (options) rather than a level (a slider).</summary>
	public bool IsChoice => Options.Count > 0;

	/// <summary>A choice's options, by name.</summary>
	public IReadOnlyList<string> Options { get; }

	/// <summary>A level's value, 0 to 1. Setting it tells the music.</summary>
	public float Value
	{
		get => _value;
		set
		{
			if (Set(ref _value, value))
				_workbench.SetCue(Cue, value);
		}
	}

	/// <summary>A choice's option, by index. Setting it tells the music.</summary>
	public int SelectedOption
	{
		get => _selectedOption;
		set
		{
			if (value < 0 || value >= Options.Count || !Set(ref _selectedOption, value))
				return;
			_value = Cue.OptionValue(value);
			Changed(nameof(Value));
			_workbench.SetCue(Cue, _value);
		}
	}
}
