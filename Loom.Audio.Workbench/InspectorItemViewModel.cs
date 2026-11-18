using System.Globalization;
using System.Windows.Input;

namespace Loom.Audio.Workbench;

/// <summary>
/// One line of an inspector: a number on a slider (on a log scale for hertz and seconds), a choice among options, a
/// switch, or a button. The patch designer's inspector is a list of these, so every operator kind and every setting is
/// drawn by one template.
/// </summary>
public sealed class InspectorItemViewModel : ViewModel
{
	private const float LogSteps = 1000f;

	private readonly Func<float>? _get;
	private readonly Action<float>? _set;
	private readonly Func<bool>? _getOn;
	private readonly Action<bool>? _setOn;
	private readonly bool _log;

	private InspectorItemViewModel(string label) => Label = label;

	/// <summary>What it is: "ratio", "attack (s)".</summary>
	public string Label { get; }

	/// <summary>Whether it is a number on a slider.</summary>
	public bool IsSlider { get; private init; }

	/// <summary>Whether it is a choice among options.</summary>
	public bool IsChoice { get; private init; }

	/// <summary>Whether it is a switch.</summary>
	public bool IsToggle { get; private init; }

	/// <summary>Whether it is a button.</summary>
	public bool IsAction { get; private init; }

	/// <summary>Whether it is a heading over the lines after it.</summary>
	public bool IsHeading { get; private init; }

	/// <summary>The slider's ends: the value's, or 0 to 1000 along a log scale.</summary>
	public float Minimum { get; private init; }

	/// <inheritdoc cref="Minimum"/>
	public float Maximum { get; private init; }

	/// <summary>The slider's step.</summary>
	public float Step { get; private init; }

	private float _min;
	private float _max;

	/// <summary>Where the slider is: the value, or its place along the log scale.</summary>
	public float Position
	{
		get => _get is null ? 0f : _log ? ToLog(_get()) : _get();
		set
		{
			if (_set is null)
				return;
			var next = _log ? FromLog(value) : value;
			if (next == _get!())
				return;
			_set(next);
			Changed();
			Changed(nameof(ValueText));
		}
	}

	/// <summary>The value as written: "0.35", "2.4 kHz".</summary>
	public string ValueText => _get is null ? "" : Format(_get());

	/// <summary>Whether a switch is on.</summary>
	public bool IsOn
	{
		get => _getOn?.Invoke() ?? false;
		set
		{
			if (_setOn is null || value == IsOn)
				return;
			_setOn(value);
			Changed();
		}
	}

	/// <summary>A choice's options, the one chosen lit.</summary>
	public IReadOnlyList<ChoiceViewModel> Options { get; private init; } = [];

	/// <summary>What a button does.</summary>
	public ICommand? Command { get; private init; }

	private string Unit { get; init; } = "";

	/// <summary>Tells its view that what it shows changed underneath it (an undo, a revert).</summary>
	internal void Refresh()
	{
		Changed(nameof(Position));
		Changed(nameof(ValueText));
		Changed(nameof(IsOn));
	}

	internal static InspectorItemViewModel Heading(string text) => new(text) { IsHeading = true };

	internal static InspectorItemViewModel Number(string label, Func<float> get, Action<float> set, float min, float max, float step = 0.01f, string unit = "") =>
		new(label, get, set, false) { IsSlider = true, Minimum = min, Maximum = max, Step = step, Unit = unit, _min = min, _max = max };

	// A number spread evenly by ratio: hertz, seconds
	internal static InspectorItemViewModel Scaled(string label, Func<float> get, Action<float> set, float min, float max, string unit = "") =>
		new(label, get, set, true) { IsSlider = true, Minimum = 0f, Maximum = LogSteps, Step = 1f, Unit = unit, _min = min, _max = max };

	internal static InspectorItemViewModel Toggle(string label, Func<bool> get, Action<bool> set) =>
		new(label, get, set) { IsToggle = true };

	internal static InspectorItemViewModel Choice(string label, IReadOnlyList<string> options, Func<int> get, Action<int> set)
	{
		ChoiceViewModel[] choices = [];
		choices = [.. options.Select((name, index) => new ChoiceViewModel(index, name, chosen =>
		{
			set(chosen);
			foreach (var choice in choices)
				choice.Show(choice.Index == chosen);
		}))];
		var current = get();
		foreach (var choice in choices)
			choice.Show(choice.Index == current);
		return new InspectorItemViewModel(label) { IsChoice = true, Options = choices };
	}

	internal static InspectorItemViewModel Button(string label, Action run) => new(label) { IsAction = true, Command = new ActionCommand(run) };

	private InspectorItemViewModel(string label, Func<float> get, Action<float> set, bool log) : this(label)
	{
		_get = get;
		_set = set;
		_log = log;
	}

	private InspectorItemViewModel(string label, Func<bool> get, Action<bool> set) : this(label)
	{
		_getOn = get;
		_setOn = set;
	}

	private float ToLog(float value) => LogSteps * MathF.Log(Math.Clamp(value, _min, _max) / _min) / MathF.Log(_max / _min);

	// Back from the log scale, rounded to three figures so the code written reads as a person would write it
	private float FromLog(float position)
	{
		var value = _min * MathF.Pow(_max / _min, Math.Clamp(position, 0f, LogSteps) / LogSteps);
		var figure = MathF.Pow(10f, MathF.Floor(MathF.Log10(value)) - 2f);
		return MathF.Round(value / figure) * figure;
	}

	private string Format(float value)
	{
		if (Unit == "Hz" && value >= 1000f)
			return (value / 1000f).ToString("0.##", CultureInfo.InvariantCulture) + " kHz";
		var text = value.ToString(MathF.Abs(value) < 0.01f && value != 0f ? "0.####" : "0.###", CultureInfo.InvariantCulture);
		return Unit.Length > 0 ? $"{text} {Unit}" : text;
	}
}
