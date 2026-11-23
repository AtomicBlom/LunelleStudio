using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

using BinaryVibrance.Sirrow.Synth.Chip;
using BinaryVibrance.Sirrow.Synth.Dsp;

namespace BinaryVibrance.SirrowStudio;

/// <summary>
/// The patch designer: a patch as data (<see cref="PatchSpec"/>) under the hands, its operators drawn as a graph in
/// render order, an inspector for the operator chosen, the voice, the macros, and the C# that builds it. Every edit
/// builds the patch again (as <see cref="Patch.Build"/> checks it) and hands it to be heard; what the builder refuses is
/// shown, and the last patch that built plays on.
/// </summary>
public sealed class PatchDesigner : ViewModel
{
	private static readonly string[] EnvelopeKinds = ["ADSR", "Decay", "Gate"];
	private static readonly string[] FilterKinds = ["None", "Low pass", "Band pass", "High pass"];
	private static readonly string[] Targets = Enum.GetNames<MacroTarget>();

	private PatchSpec _spec = new();
	private List<string> _names = [];
	private Patch? _original;
	private string _originalCode = "";
	private Func<int, string?>? _macroName;

	internal PatchDesigner()
	{
		AddButtons =
		[
			InspectorItemViewModel.Button("Sine", () => Add(new OperatorSpec { Kind = OperatorKind.Sine, Envelope = EnvelopeShape.Struck(1f), Level = 0.6f })),
			InspectorItemViewModel.Button("Saw", () => Add(new OperatorSpec { Kind = OperatorKind.Wave, Waveform = Waveform.Saw, Envelope = EnvelopeShape.Adsr(0.01f, 0.3f, 0.7f, 0.2f), Level = 0.4f })),
			InspectorItemViewModel.Button("Pulse", () => Add(new OperatorSpec { Kind = OperatorKind.Wave, Waveform = Waveform.Pulse, Envelope = EnvelopeShape.Adsr(0.01f, 0.3f, 0.7f, 0.2f), Level = 0.4f })),
			InspectorItemViewModel.Button("Noise", () => Add(new OperatorSpec { Kind = OperatorKind.Noise, Envelope = EnvelopeShape.Struck(0.2f), Level = 0.4f })),
			InspectorItemViewModel.Button("Level", () => Add(new OperatorSpec { Kind = OperatorKind.Level, Envelope = EnvelopeShape.Adsr(0.04f, 0.2f, 0.8f, 0.12f) }, heard: false)),
			InspectorItemViewModel.Button("String", () => Add(new OperatorSpec { Kind = OperatorKind.String, DecaySeconds = 2f, Damping = 0.4f, Pick = 0.2f })),
			InspectorItemViewModel.Button("Tube", () => Add(new OperatorSpec { Kind = OperatorKind.Tube, Drivers = LastAsDriver() })),
			InspectorItemViewModel.Button("Modes", () => Add(new OperatorSpec { Kind = OperatorKind.Modes, Modes = [Mode.At(1f, 1f)], Drivers = LastAsDriver() }, level: 0.1f)),
		];
	}

	/// <summary>The patch to hear, each time it builds: the edit, the operator alone, or the patch as its code has it.</summary>
	internal event Action<Patch>? Built;

	/// <summary>The C# asked to be copied.</summary>
	internal event Action<string>? CopyRequested;

	/// <summary>Whether a patch is loaded.</summary>
	public bool IsLoaded => _original is not null;

	/// <summary>The patch's name.</summary>
	public string Name => _spec.Name;

	/// <summary>The operators, as the graph's nodes.</summary>
	public ObservableCollection<OperatorNodeViewModel> Nodes { get; } = [];

	/// <summary>The voice after the operators, stage by stage.</summary>
	public ObservableCollection<StageViewModel> Stages { get; } = [];

	/// <summary>The inspector's lines for the tab open.</summary>
	public ObservableCollection<InspectorItemViewModel> Inspector { get; } = [];

	/// <summary>A button per kind of operator to add.</summary>
	public IReadOnlyList<InspectorItemViewModel> AddButtons { get; }

	/// <summary>The operator chosen, by index, or −1 when the voice is.</summary>
	public int Selected
	{
		get;
		private set
		{
			if (field == value)
				return;
			// The nodes first: whatever redraws on the change reads which one is chosen from them.
			foreach (var node in Nodes)
				node.IsSelected = node.Index == value;
			Set(ref field, value);
		}
	} = -1;

	/// <summary>Raised once <see cref="Nodes"/> are laid out and each says what its operator is: after a new patch, a change
	/// to its operators, or an edit to one. A view drawing the graph redraws on it rather than as each node is added, before
	/// it is described.</summary>
	public event EventHandler? NodesDescribed;

	/// <summary>What the inspector shows.</summary>
	public DesignerTab Tab
	{
		get;
		set
		{
			if (value == DesignerTab.Operator && Selected < 0)
				value = DesignerTab.Voice;
			if (!Set(ref field, value))
				return;
			Changed(nameof(IsOperatorTab));
			Changed(nameof(IsVoiceTab));
			Changed(nameof(IsMacrosTab));
			Changed(nameof(IsCodeTab));
			ShowInspector();
		}
	}

	/// <summary>Whether the inspector shows the operator chosen.</summary>
	public bool IsOperatorTab
	{
		get => Tab == DesignerTab.Operator;
		set
		{
			if (value)
				Tab = DesignerTab.Operator;
		}
	}

	/// <summary>Whether it shows the voice.</summary>
	public bool IsVoiceTab
	{
		get => Tab == DesignerTab.Voice;
		set
		{
			if (value)
				Tab = DesignerTab.Voice;
		}
	}

	/// <summary>Whether it shows the macros.</summary>
	public bool IsMacrosTab
	{
		get => Tab == DesignerTab.Macros;
		set
		{
			if (value)
				Tab = DesignerTab.Macros;
		}
	}

	/// <summary>Whether it shows the code.</summary>
	public bool IsCodeTab
	{
		get => Tab == DesignerTab.Code;
		set
		{
			if (value)
				Tab = DesignerTab.Code;
		}
	}

	/// <summary>The C# that builds the patch as it stands.</summary>
	public string Code
	{
		get;
		private set => Set(ref field, value);
	} = "";

	/// <summary>What the builder refuses in it, or empty when it builds.</summary>
	public string Problems
	{
		get;
		private set
		{
			if (Set(ref field, value))
				Changed(nameof(HasProblems));
		}
	} = "";

	/// <summary>Whether it doesn't build (the last that built plays on).</summary>
	public bool HasProblems => Problems.Length > 0;

	/// <summary>"4 of 16 operators · builds", "· edited".</summary>
	public string Status
	{
		get;
		private set => Set(ref field, value);
	} = "";

	/// <summary>Which songs the patch is used in, said so an edit's reach is clear: "Used in open-road and understory".</summary>
	public string UsedBy
	{
		get;
		internal set => Set(ref field, value);
	} = "";

	/// <summary>Whether it differs from the patch as its code has it.</summary>
	public bool IsDirty
	{
		get;
		private set => Set(ref field, value);
	}

	/// <summary>Whether the keyboard plays the patch as its code has it, rather than the edit.</summary>
	public bool HearOriginal
	{
		get;
		set
		{
			if (!Set(ref field, value))
				return;
			if (value && _original is not null)
				Built?.Invoke(_original);
			else
				Rebuild(false);
		}
	}

	/// <summary>Whether the keyboard hears the operator chosen alone.</summary>
	public bool ListenAlone
	{
		get;
		set
		{
			if (!Set(ref field, value))
				return;
			foreach (var node in Nodes)
				node.IsAlone = value && node.Index == Selected;
			Rebuild(false);
		}
	}

	/// <summary>Whether the buttons to add an operator are showing.</summary>
	public bool AddOpen
	{
		get;
		set => Set(ref field, value);
	}

	/// <summary>Puts the patch back as its code has it.</summary>
	public ICommand RevertCommand => field ??= new ActionCommand(Revert);

	/// <summary>Asks for the code to be copied.</summary>
	public ICommand CopyCommand => field ??= new ActionCommand(() => CopyRequested?.Invoke(Code));

	/// <summary>Opens the voice after the operators in the inspector.</summary>
	public ICommand VoiceCommand => field ??= new ActionCommand(() =>
	{
		Select(-1);
		Tab = DesignerTab.Voice;
	});

	/// <summary>The patch built from the edit, or null while it doesn't build.</summary>
	public Patch? Current { get; private set; }

	/// <summary>The spec under edit.</summary>
	public PatchSpec Spec => _spec;

	/// <summary>
	/// Opens <paramref name="patch"/>, its operators named by <paramref name="names"/> (its file's) or by kind. With
	/// <paramref name="keepEdits"/> an edited patch of the same name keeps its edits (a rebuild after a code change), and
	/// only what "as its code has it" means is updated.
	/// </summary>
	public void Load(Patch patch, Func<int, string?>? macroName = null, bool keepEdits = false, IReadOnlyList<string>? names = null)
	{
		ArgumentNullException.ThrowIfNull(patch);
		_macroName = macroName;
		if (keepEdits && IsDirty && patch.Name == _spec.Name)
		{
			_original = patch;
			_originalCode = PatchCode.Write(patch, names: _names, macroName: _macroName);
			Rebuild(false);
			return;
		}
		_original = patch;
		_spec = PatchSpec.From(patch);
		_names = [.. PatchCode.OperatorNames(_spec.Operators, names)];
		_originalCode = PatchCode.Write(_spec, names: _names, macroName: _macroName);
		ListenAlone = false;
		HearOriginal = false;
		AddOpen = false;
		Changed(nameof(Name));
		Changed(nameof(IsLoaded));
		Rebuild(true);
		Select(_spec.Operators.Count - 1);
	}

	/// <summary>Chooses an operator for the inspector, or the voice with −1.</summary>
	public void Select(int index)
	{
		Selected = (uint)index < (uint)_spec.Operators.Count ? index : -1;
		if (ListenAlone)
		{
			foreach (var node in Nodes)
				node.IsAlone = node.Index == Selected;
			Rebuild(false);
		}
		if (Tab is DesignerTab.Operator or DesignerTab.Voice)
			Tab = Selected >= 0 ? DesignerTab.Operator : DesignerTab.Voice;
		ShowInspector();
	}

	private void Revert()
	{
		if (_original is { } original)
			Load(original, _macroName);
	}

	// ---- Building

	private void Rebuild(bool structure)
	{
		if (structure)
			LayOut();
		else
			Describe();
		ShowStages();
		try
		{
			Current = HeardSpec().ToPatch();
			Problems = "";
		}
		catch (PatchException ex)
		{
			Current = null;
			Problems = ex.Message;
		}
		try
		{
			Code = PatchCode.Write(_spec, names: _names, macroName: _macroName);
		}
		catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
		{
			Code = "// This patch can't be written as C# while it routes to an operator that doesn't exist.";
		}
		IsDirty = Code != _originalCode;
		Status = $"{_spec.Operators.Count} of {Patch.MaxOperators} operators · {(HasProblems ? "doesn't build" : "builds")}{(IsDirty ? " · edited" : "")}";
		if (Current is { } patch && !HearOriginal)
			Built?.Invoke(patch);
	}

	// What the keyboard hears: the edit, or only the operator chosen when it is heard alone
	private PatchSpec HeardSpec()
	{
		if (!ListenAlone || Selected < 0)
			return _spec;
		var alone = _spec.Clone();
		alone.Outputs.Clear();
		alone.Outputs.Add(new OutputTap(Selected, 1f));
		return alone;
	}

	// Columns by depth: an operator sits one right of the deepest operator it reads, so signal runs left to right
	private void LayOut()
	{
		Nodes.Clear();
		var ops = _spec.Operators;
		var depth = new int[ops.Count];
		var rows = new Dictionary<int, int>();
		for (var i = 0; i < ops.Count; i++)
		{
			var d = 0;
			foreach (var m in ops[i].Modulators)
				if (m.Operator < i)
					d = Math.Max(d, depth[m.Operator] + 1);
			foreach (var driver in ops[i].Drivers)
				if (driver < i)
					d = Math.Max(d, depth[driver] + 1);
			depth[i] = d;
			var row = rows.GetValueOrDefault(d);
			rows[d] = row + 1;
			Nodes.Add(new OperatorNodeViewModel(i) { Column = d, Row = row, IsSelected = i == Selected, IsAlone = ListenAlone && i == Selected });
		}
		Describe();
	}

	private void Describe()
	{
		var heard = _spec.Outputs.Select(t => t.Operator).ToHashSet();
		foreach (var node in Nodes)
		{
			if (node.Index >= _spec.Operators.Count)
				continue;
			var o = _spec.Operators[node.Index];
			node.Name = node.Index < _names.Count ? _names[node.Index] : "";
			node.Kind = o.Kind;
			node.IsHeard = heard.Contains(node.Index);
			node.Summary = Summary(o);
			node.Detail = o.Kind is OperatorKind.String or OperatorKind.Tube or OperatorKind.Modes
				? o.Drivers.Length > 0 ? "driven by " + string.Join(", ", o.Drivers.Select(NameOf)) : o.Kind == OperatorKind.String ? "plucked" : "not driven"
				: $"level {N(o.Level)}{(o.Velocity != 0f ? " · vel " + N(o.Velocity) : "")}";
		}

		NodesDescribed?.Invoke(this, EventArgs.Empty);
	}

	private string NameOf(int index) => index >= 0 && index < _names.Count ? _names[index] : $"#{index}";

	private static string N(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

	private static string Hz(float value) => value >= 1000f ? (value / 1000f).ToString("0.##", CultureInfo.InvariantCulture) + " kHz" : N(value) + " Hz";

	private static string Summary(OperatorSpec o)
	{
		var pitch = (o.FixedHz > 0f ? Hz(o.FixedHz) : "×" + N(o.Ratio)) + (o.DetuneCents != 0f ? $" {o.DetuneCents:+0.#;-0.#}¢" : "");
		return o.Kind switch
		{
			OperatorKind.Sine => "SINE " + pitch + (o.Feedback != 0f ? " fb" : ""),
			OperatorKind.Wave => o.Waveform.ToString().ToUpperInvariant() + " " + pitch,
			OperatorKind.Noise => o.Pink ? "NOISE pink" : "NOISE white",
			OperatorKind.Level => "LEVEL",
			OperatorKind.Sample => "SAMPLE",
			OperatorKind.String => $"STRING {pitch} {N(o.DecaySeconds)} s",
			OperatorKind.Tube => "TUBE ×" + N(o.Ratio),
			OperatorKind.Modes => $"MODES {o.Modes.Length}",
			_ => o.Kind.ToString(),
		};
	}

	private void ShowStages()
	{
		var s = _spec;
		var voice = VoiceCommand;
		Stages.Clear();
		Stages.Add(new StageViewModel("Mix", $"{s.Outputs.Count} heard", false, voice));
		Stages.Add(new StageViewModel("Filter", s.Filter is { } f ? $"{FilterKinds[(int)f.Kind + 1]} {Hz(f.CutoffHz)}{(f.EnvelopeOctaves != 0f ? " +env" : "")}" : "none", s.Filter is null, voice));
		Stages.Add(new StageViewModel("Drive", s.Drive != 0f ? N(s.Drive) : "off", s.Drive == 0f, voice));
		Stages.Add(new StageViewModel("Gain", $"{s.GainDb:+0.#;-0.#;0} dB × vel²", false, voice));
		Stages.Add(new StageViewModel("Pan", s.Pan != 0f ? N(s.Pan) : "centre", s.Pan == 0f, voice));
		Stages.Add(new StageViewModel("Sends", $"space {N(s.SendSpace)} · echo {N(s.SendEcho)}", s.SendSpace == 0f && s.SendEcho == 0f && s.SendKey == 0f, voice));
		Stages.Add(new StageViewModel("Vibrato", s.VibratoSemitones != 0f ? $"{N(s.VibratoHz)} Hz {N(s.VibratoSemitones)} st" : "off", s.VibratoSemitones == 0f, voice));
		Stages.Add(new StageViewModel("Tremolo", s.TremoloDepth != 0f ? $"{N(s.TremoloHz)} Hz {N(s.TremoloDepth)}" : "off", s.TremoloDepth == 0f, voice));
		Stages.Add(new StageViewModel("Pitch drop", s.PitchDropSemitones != 0f ? $"{N(s.PitchDropSemitones)} st / {N(s.PitchDropSeconds * 1000f)} ms" : "off", s.PitchDropSemitones == 0f, voice));
		Stages.Add(new StageViewModel("Mono", s.Mono ? s.GlideSeconds != 0f ? $"glide {N(s.GlideSeconds * 1000f)} ms" : "on" : "poly", !s.Mono, voice));
	}

	// ---- Changing it

	private OperatorSpec Op => _spec.Operators[Selected];

	private void SetOp(Func<OperatorSpec, OperatorSpec> change, bool structure = false)
	{
		if (Selected < 0)
			return;
		_spec.Operators[Selected] = change(Op);
		Rebuild(structure);
		if (structure)
			ShowInspector();
	}

	private void SetVoice(Action<PatchSpec> change, bool structure = false)
	{
		change(_spec);
		Rebuild(false);
		if (structure)
			ShowInspector();
	}

	private int[] LastAsDriver() => _spec.Operators.Count > 0 ? [_spec.Operators.Count - 1] : [];

	private void Add(OperatorSpec spec, bool heard = true, float level = 1f)
	{
		if (_spec.Operators.Count >= Patch.MaxOperators)
			return;
		_spec.Operators.Add(spec);
		_names = [.. PatchCode.OperatorNames(_spec.Operators, _names)];
		if (heard)
			_spec.Outputs.Add(new OutputTap(_spec.Operators.Count - 1, level));
		AddOpen = false;
		Rebuild(true);
		Select(_spec.Operators.Count - 1);
	}

	private void Delete(int d)
	{
		var ops = _spec.Operators;
		ops.RemoveAt(d);
		int Shift(int x) => x > d ? x - 1 : x;
		for (var i = 0; i < ops.Count; i++)
			ops[i] = ops[i] with
			{
				Modulators = [.. ops[i].Modulators.Where(m => m.Operator != d).Select(m => m with { Operator = Shift(m.Operator) })],
				Drivers = [.. ops[i].Drivers.Where(x => x != d).Select(Shift)],
			};
		var outputs = _spec.Outputs.Where(t => t.Operator != d).Select(t => t with { Operator = Shift(t.Operator) }).ToArray();
		_spec.Outputs.Clear();
		_spec.Outputs.AddRange(outputs);
		var macros = _spec.Macros.Where(m => m.Operator != d).Select(m => m with { Operator = m.Operator < 0 ? -1 : Shift(m.Operator) }).ToArray();
		_spec.Macros.Clear();
		_spec.Macros.AddRange(macros);
		if (d < _names.Count)
			_names.RemoveAt(d);
		ListenAlone = false;
		Rebuild(true);
		Select(-1);
	}

	// Swaps an operator with its neighbour, unless one reads the other: an operator reads only those before it
	private bool Move(int i, int j)
	{
		var ops = _spec.Operators;
		if (j < 0 || j >= ops.Count)
			return false;
		var (early, late) = i < j ? (i, j) : (j, i);
		if (ops[late].Modulators.Any(m => m.Operator == early) || ops[late].Drivers.Contains(early))
			return false;
		int Swap(int x) => x == i ? j : x == j ? i : x;
		(ops[i], ops[j]) = (ops[j], ops[i]);
		for (var k = 0; k < ops.Count; k++)
			ops[k] = ops[k] with
			{
				Modulators = [.. ops[k].Modulators.Select(m => m with { Operator = Swap(m.Operator) })],
				Drivers = [.. ops[k].Drivers.Select(Swap)],
			};
		var outputs = _spec.Outputs.Select(t => t with { Operator = Swap(t.Operator) }).ToArray();
		_spec.Outputs.Clear();
		_spec.Outputs.AddRange(outputs);
		var macros = _spec.Macros.Select(m => m with { Operator = m.Operator < 0 ? -1 : Swap(m.Operator) }).ToArray();
		_spec.Macros.Clear();
		_spec.Macros.AddRange(macros);
		(_names[i], _names[j]) = (_names[j], _names[i]);
		Rebuild(true);
		Select(j);
		return true;
	}

	/// <summary>
	/// Moves operator <paramref name="from"/> to <paramref name="to"/> in the patch's order, a place at a time, stopping
	/// where it would pass an operator it reads or that reads it (an operator reads only those before it). Returns where
	/// it ended up.
	/// </summary>
	public int MoveTo(int from, int to)
	{
		var ops = _spec.Operators;
		if ((uint)from >= (uint)ops.Count)
			return from;
		to = Math.Clamp(to, 0, ops.Count - 1);
		var at = from;
		var step = Math.Sign(to - from);
		while (at != to && Move(at, at + step))
			at += step;
		return at;
	}

	// ---- The inspector

	private void ShowInspector()
	{
		Inspector.Clear();
		if (!IsLoaded)
			return;
		switch (Tab)
		{
			case DesignerTab.Operator when Selected >= 0:
				OperatorItems();
				break;
			case DesignerTab.Operator:
			case DesignerTab.Voice:
				VoiceItems();
				break;
			case DesignerTab.Macros:
				MacroItems();
				break;
		}
	}

	private void Show(InspectorItemViewModel item) => Inspector.Add(item);

	private void OperatorItems()
	{
		var i = Selected;
		var o = Op;
		var earlier = Enumerable.Range(0, i).ToArray();
		Show(InspectorItemViewModel.Heading($"{NameOf(i)} · #{i + 1} of {_spec.Operators.Count}"));
		Show(InspectorItemViewModel.Toggle("Solo", () => ListenAlone, on => ListenAlone = on));
		Show(InspectorItemViewModel.Button("Delete", () => Delete(i)));

		var tap = _spec.Outputs.FindIndex(t => t.Operator == i);
		Show(InspectorItemViewModel.Heading("Heard"));
		Show(InspectorItemViewModel.Toggle("in Out", () => _spec.Outputs.Any(t => t.Operator == i), on =>
		{
			if (on)
				_spec.Outputs.Add(new OutputTap(i, 1f));
			else
				_spec.Outputs.RemoveAll(t => t.Operator == i);
			Rebuild(false);
			ShowInspector();
		}));
		if (tap >= 0)
		{
			Show(InspectorItemViewModel.Number("level", () => Tap(i).Level, v => SetTap(i, t => t with { Level = v }), 0f, 2f, 0.001f));
			Show(InspectorItemViewModel.Number("pan", () => Tap(i).Pan, v => SetTap(i, t => t with { Pan = v }), -1f, 1f));
		}

		if (o.Kind is not (OperatorKind.Noise or OperatorKind.Level or OperatorKind.Modes or OperatorKind.Sample))
		{
			Show(InspectorItemViewModel.Heading("Pitch"));
			Show(InspectorItemViewModel.Choice("follows", ["the note", "a fixed Hz"], () => Op.FixedHz > 0f ? 1 : 0,
				c => SetOp(x => x with { FixedHz = c == 1 ? 440f * x.Ratio : 0f }, structure: true)));
			if (o.FixedHz > 0f)
				Show(InspectorItemViewModel.Scaled("frequency", () => Op.FixedHz, v => SetOp(x => x with { FixedHz = v }), 20f, 16000f, "Hz"));
			else
				Show(InspectorItemViewModel.Number("ratio", () => Op.Ratio, v => SetOp(x => x with { Ratio = Math.Max(v, 0.01f) }), 0.25f, 24f));
			Show(InspectorItemViewModel.Number("detune", () => Op.DetuneCents, v => SetOp(x => x with { DetuneCents = v }), -50f, 50f, 0.5f, "¢"));
			if (o.Kind is OperatorKind.Sine or OperatorKind.Wave)
				Show(InspectorItemViewModel.Number("phase", () => Op.StartPhase, v => SetOp(x => x with { StartPhase = v }), 0f, 0.999f, 0.001f, "cycles"));
		}

		switch (o.Kind)
		{
			case OperatorKind.Sine:
				Show(InspectorItemViewModel.Heading("Sine"));
				Show(InspectorItemViewModel.Number("feedback", () => Op.Feedback, v => SetOp(x => x with { Feedback = v }), 0f, 1.5f, 0.01f, "rad"));
				break;
			case OperatorKind.Wave:
				Show(InspectorItemViewModel.Heading("Wave"));
				Show(InspectorItemViewModel.Choice("shape", ["Saw", "Pulse"], () => (int)Op.Waveform, c => SetOp(x => x with { Waveform = (Waveform)c }, structure: true)));
				if (o.Waveform == Waveform.Pulse)
					Show(InspectorItemViewModel.Number("pulse width", () => Op.PulseWidth, v => SetOp(x => x with { PulseWidth = Math.Clamp(v, 0.05f, 0.95f) }), 0.05f, 0.95f));
				break;
			case OperatorKind.Noise:
				Show(InspectorItemViewModel.Heading("Noise"));
				Show(InspectorItemViewModel.Toggle("pink", () => Op.Pink, on => SetOp(x => x with { Pink = on })));
				break;
			case OperatorKind.String:
				Show(InspectorItemViewModel.Heading("String"));
				Show(InspectorItemViewModel.Scaled("rings", () => Op.DecaySeconds, v => SetOp(x => x with { DecaySeconds = v }), 0.1f, 30f, "s"));
				Show(InspectorItemViewModel.Number("damping", () => Op.Damping, v => SetOp(x => x with { Damping = v }), 0f, 1f));
				Show(InspectorItemViewModel.Number("pick", () => Op.Pick, v => SetOp(x => x with { Pick = v }), 0.02f, 0.5f, 0.005f));
				break;
			case OperatorKind.Modes:
				ModeItems(o);
				break;
		}

		if (o.Kind is not (OperatorKind.String or OperatorKind.Tube or OperatorKind.Modes))
		{
			Show(InspectorItemViewModel.Heading("Level"));
			Show(InspectorItemViewModel.Number("level", () => Op.Level, v => SetOp(x => x with { Level = v }), 0f, 2f));
			Show(InspectorItemViewModel.Number("velocity", () => Op.Velocity, v => SetOp(x => x with { Velocity = v }), 0f, 1f));
		}
		Show(InspectorItemViewModel.Heading("Key scaling"));
		Show(InspectorItemViewModel.Number("key level", () => Op.KeyLevelDb, v => SetOp(x => x with { KeyLevelDb = v }), -12f, 6f, 0.5f, "dB/oct"));
		Show(InspectorItemViewModel.Number("key decay", () => Op.KeyDecay, v => SetOp(x => x with { KeyDecay = v }), 0f, 0.95f));

		if (o.Kind == OperatorKind.Sine)
		{
			Show(InspectorItemViewModel.Heading("Modulated by"));
			for (var m = 0; m < o.Modulators.Length; m++)
			{
				var at = m;
				Show(InspectorItemViewModel.Choice("from", [.. earlier.Select(NameOf)], () => Op.Modulators[at].Operator,
					c => SetOp(x => x with { Modulators = Replaced(x.Modulators, at, x.Modulators[at] with { Operator = c }) }, structure: true)));
				Show(InspectorItemViewModel.Number("index", () => Op.Modulators[at].Index,
					v => SetOp(x => x with { Modulators = Replaced(x.Modulators, at, x.Modulators[at] with { Index = v }) }), 0f, 12f, 0.05f));
				Show(InspectorItemViewModel.Button("Delete modulator", () => SetOp(x => x with { Modulators = [.. x.Modulators.Where((_, k) => k != at)] }, structure: true)));
			}
			if (earlier.Length > 0)
				Show(InspectorItemViewModel.Button("+ Modulator", () => SetOp(x => x with { Modulators = [.. x.Modulators, new Modulation(i - 1, 1f)] }, structure: true)));
		}
		if (o.Kind is OperatorKind.String or OperatorKind.Tube or OperatorKind.Modes)
		{
			Show(InspectorItemViewModel.Heading("Driven by"));
			foreach (var e in earlier)
				Show(InspectorItemViewModel.Toggle(NameOf(e), () => Op.Drivers.Contains(e),
					on => SetOp(x => x with { Drivers = on ? [.. x.Drivers.Append(e).Order()] : [.. x.Drivers.Where(d => d != e)] })));
		}
		if (o.Kind is not (OperatorKind.String or OperatorKind.Tube or OperatorKind.Modes))
			EnvelopeItems("Envelope", () => Op.Envelope, e => SetOp(x => x with { Envelope = e }));
	}

	private void ModeItems(OperatorSpec o)
	{
		for (var m = 0; m < o.Modes.Length; m++)
		{
			var at = m;
			Show(InspectorItemViewModel.Heading($"Mode {m + 1}"));
			Show(InspectorItemViewModel.Choice("follows", ["the note", "a fixed Hz"], () => Op.Modes[at].Hz > 0f ? 1 : 0,
				c => SetOp(x => x with { Modes = Replaced(x.Modes, at, c == 1 ? x.Modes[at] with { Hz = 440f * x.Modes[at].Ratio } : x.Modes[at] with { Hz = 0f }) }, structure: true)));
			if (o.Modes[m].Hz > 0f)
				Show(InspectorItemViewModel.Scaled("frequency", () => Op.Modes[at].Hz, v => SetOp(x => x with { Modes = Replaced(x.Modes, at, x.Modes[at] with { Hz = v }) }), 20f, 16000f, "Hz"));
			else
				Show(InspectorItemViewModel.Number("ratio", () => Op.Modes[at].Ratio, v => SetOp(x => x with { Modes = Replaced(x.Modes, at, x.Modes[at] with { Ratio = Math.Max(v, 0.01f) }) }), 0.5f, 24f));
			Show(InspectorItemViewModel.Scaled("rings", () => Op.Modes[at].DecaySeconds, v => SetOp(x => x with { Modes = Replaced(x.Modes, at, x.Modes[at] with { DecaySeconds = v }) }), 0.002f, 10f, "s"));
			Show(InspectorItemViewModel.Number("level", () => Op.Modes[at].Level, v => SetOp(x => x with { Modes = Replaced(x.Modes, at, x.Modes[at] with { Level = v }) }), 0f, 1.5f));
			if (o.Modes.Length > 1)
				Show(InspectorItemViewModel.Button("Delete mode", () => SetOp(x => x with { Modes = [.. x.Modes.Where((_, k) => k != at)] }, structure: true)));
		}
		if (o.Modes.Length < Mode.Max)
			Show(InspectorItemViewModel.Button("+ Mode", () => SetOp(x => x with { Modes = [.. x.Modes, Mode.At(x.Modes.Length + 2, 0.5f, 0.5f)] }, structure: true)));
	}

	private void EnvelopeItems(string title, Func<EnvelopeShape> get, Action<EnvelopeShape> set)
	{
		Show(InspectorItemViewModel.Heading(title));
		Show(InspectorItemViewModel.Choice("shape", EnvelopeKinds, () => EnvelopeKind(get()), kind =>
		{
			var was = get();
			var decay = Math.Max(was.Decay, 0.05f);
			set(kind switch
			{
				0 => EnvelopeShape.Adsr(0.005f, decay, 0.6f, 0.2f),
				1 => EnvelopeShape.Struck(decay),
				_ => EnvelopeShape.Gate,
			});
			ShowInspector();
		}));
		switch (EnvelopeKind(get()))
		{
			case 0:
				Show(InspectorItemViewModel.Scaled("attack", () => get().Attack, v => set(get() with { Attack = v }), 0.001f, 4f, "s"));
				Show(InspectorItemViewModel.Scaled("decay", () => get().Decay, v => set(get() with { Decay = v }), 0.005f, 10f, "s"));
				Show(InspectorItemViewModel.Number("sustain", () => get().Sustain, v => set(get() with { Sustain = v }), 0f, 1f));
				Show(InspectorItemViewModel.Scaled("release", () => get().Release, v => set(get() with { Release = v }), 0.005f, 10f, "s"));
				break;
			case 1:
				Show(InspectorItemViewModel.Scaled("rings", () => get().Decay, v => set(EnvelopeShape.Struck(v)), 0.001f, 12f, "s"));
				break;
		}
	}

	private static int EnvelopeKind(EnvelopeShape e) => e == EnvelopeShape.Gate ? 2 : e == EnvelopeShape.Struck(e.Decay) ? 1 : 0;

	private void VoiceItems()
	{
		Show(InspectorItemViewModel.Heading("Filter"));
		Show(InspectorItemViewModel.Choice("kind", FilterKinds, () => _spec.Filter is { } f ? (int)f.Kind + 1 : 0, c => SetVoice(s =>
			s.Filter = c == 0 ? null : (s.Filter ?? new FilterSpec(FilterKind.LowPass, 2000f)) with { Kind = (FilterKind)(c - 1) }, structure: true)));
		if (_spec.Filter is not null)
		{
			Show(InspectorItemViewModel.Scaled("cutoff", () => _spec.Filter!.CutoffHz, v => SetVoice(s => s.Filter = s.Filter! with { CutoffHz = v }), 20f, 18000f, "Hz"));
			Show(InspectorItemViewModel.Number("q", () => _spec.Filter!.Q, v => SetVoice(s => s.Filter = s.Filter! with { Q = Math.Max(v, 0.1f) }), 0.3f, 12f, 0.05f));
			Show(InspectorItemViewModel.Number("key track", () => _spec.Filter!.KeyTrack, v => SetVoice(s => s.Filter = s.Filter! with { KeyTrack = v }), 0f, 1f));
			Show(InspectorItemViewModel.Number("velocity", () => _spec.Filter!.Velocity, v => SetVoice(s => s.Filter = s.Filter! with { Velocity = v }), 0f, 1f));
			Show(InspectorItemViewModel.Number("envelope octaves", () => _spec.Filter!.EnvelopeOctaves, v => SetVoice(s =>
				s.Filter = s.Filter! with
				{
					EnvelopeOctaves = v,
					Envelope = s.Filter.Envelope == default ? EnvelopeShape.Struck(0.2f) : s.Filter.Envelope,
				}, structure: (_spec.Filter!.EnvelopeOctaves == 0f) != (v == 0f)), -4f, 6f, 0.1f));
			if (_spec.Filter.EnvelopeOctaves != 0f)
				EnvelopeItems("Filter envelope", () => _spec.Filter!.Envelope, e => SetVoice(s => s.Filter = s.Filter! with { Envelope = e }));
		}
		Show(InspectorItemViewModel.Heading("Voice"));
		Show(InspectorItemViewModel.Number("drive", () => _spec.Drive, v => SetVoice(s => s.Drive = v), 0f, 1f));
		Show(InspectorItemViewModel.Number("gain", () => _spec.GainDb, v => SetVoice(s => s.GainDb = v), -24f, 12f, 0.5f, "dB"));
		Show(InspectorItemViewModel.Number("pan", () => _spec.Pan, v => SetVoice(s => s.Pan = v), -1f, 1f));
		Show(InspectorItemViewModel.Heading("Sends"));
		Show(InspectorItemViewModel.Number("space", () => _spec.SendSpace, v => SetVoice(s => s.SendSpace = v), 0f, 1f));
		Show(InspectorItemViewModel.Number("echo", () => _spec.SendEcho, v => SetVoice(s => s.SendEcho = v), 0f, 1f));
		Show(InspectorItemViewModel.Number("key", () => _spec.SendKey, v => SetVoice(s => s.SendKey = v), 0f, 1f));
		Show(InspectorItemViewModel.Heading("Movement"));
		Show(InspectorItemViewModel.Number("vibrato rate", () => _spec.VibratoHz, v => SetVoice(s => s.VibratoHz = v), 0f, 12f, 0.1f, "Hz"));
		Show(InspectorItemViewModel.Number("vibrato depth", () => _spec.VibratoSemitones, v => SetVoice(s => s.VibratoSemitones = v), 0f, 1f, 0.01f, "st"));
		Show(InspectorItemViewModel.Number("vibrato delay", () => _spec.VibratoDelaySeconds, v => SetVoice(s => s.VibratoDelaySeconds = v), 0f, 2f, 0.01f, "s"));
		Show(InspectorItemViewModel.Number("tremolo rate", () => _spec.TremoloHz, v => SetVoice(s => s.TremoloHz = v), 0f, 12f, 0.1f, "Hz"));
		Show(InspectorItemViewModel.Number("tremolo depth", () => _spec.TremoloDepth, v => SetVoice(s => s.TremoloDepth = v), 0f, 1f));
		Show(InspectorItemViewModel.Number("pitch drop", () => _spec.PitchDropSemitones, v => SetVoice(s =>
		{
			s.PitchDropSemitones = v;
			if (v != 0f && s.PitchDropSeconds <= 0f)
				s.PitchDropSeconds = 0.04f;
		}), 0f, 48f, 0.5f, "st"));
		Show(InspectorItemViewModel.Scaled("drop time", () => Math.Max(_spec.PitchDropSeconds, 0.005f), v => SetVoice(s => s.PitchDropSeconds = v), 0.005f, 1f, "s"));
		Show(InspectorItemViewModel.Heading("Mono"));
		Show(InspectorItemViewModel.Toggle("one voice", () => _spec.Mono, on => SetVoice(s => s.Mono = on, structure: true)));
		if (_spec.Mono)
			Show(InspectorItemViewModel.Number("glide", () => _spec.GlideSeconds, v => SetVoice(s => s.GlideSeconds = v), 0f, 0.5f, 0.005f, "s"));
	}

	private void MacroItems()
	{
		string[] slots = [.. Enumerable.Range(0, Macro.Count).Select(id => _macroName?.Invoke(id) ?? $"macro {id}")];
		for (var k = 0; k < _spec.Macros.Count; k++)
		{
			var at = k;
			var macro = _spec.Macros[k];
			Show(InspectorItemViewModel.Heading(slots[macro.Macro.Id]));
			Show(InspectorItemViewModel.Choice("macro", slots, () => _spec.Macros[at].Macro.Id, c => SetMacro(at, m => m with { Macro = new Macro(c) }, structure: true)));
			Show(InspectorItemViewModel.Choice("moves", Targets, () => (int)_spec.Macros[at].Target, c => SetMacro(at, m => m with
			{
				Target = (MacroTarget)c,
				Operator = (MacroTarget)c == MacroTarget.OperatorLevel ? Math.Max(m.Operator, 0) : -1,
			}, structure: true)));
			if (macro.Target == MacroTarget.OperatorLevel && _spec.Operators.Count > 0)
				Show(InspectorItemViewModel.Choice("operator", [.. _names], () => _spec.Macros[at].Operator, c => SetMacro(at, m => m with { Operator = c })));
			Show(InspectorItemViewModel.Number("at 0", () => _spec.Macros[at].From, v => SetMacro(at, m => m with { From = v }), -6f, 6f, 0.05f));
			Show(InspectorItemViewModel.Number("at 1", () => _spec.Macros[at].To, v => SetMacro(at, m => m with { To = v }), -6f, 6f, 0.05f));
			Show(InspectorItemViewModel.Button("Delete macro", () =>
			{
				_spec.Macros.RemoveAt(at);
				Rebuild(false);
				ShowInspector();
			}));
		}
		Show(InspectorItemViewModel.Button("+ Macro", () =>
		{
			_spec.Macros.Add(new MacroSpec(new Macro(0), MacroTarget.FilterOctaves, -1f, 1f));
			Rebuild(false);
			ShowInspector();
		}));
	}

	private void SetMacro(int at, Func<MacroSpec, MacroSpec> change, bool structure = false)
	{
		_spec.Macros[at] = change(_spec.Macros[at]);
		Rebuild(false);
		if (structure)
			ShowInspector();
	}

	private OutputTap Tap(int op) => _spec.Outputs.Find(t => t.Operator == op);

	private void SetTap(int op, Func<OutputTap, OutputTap> change)
	{
		var at = _spec.Outputs.FindIndex(t => t.Operator == op);
		if (at < 0)
			return;
		_spec.Outputs[at] = change(_spec.Outputs[at]);
		Rebuild(false);
	}

	private static T[] Replaced<T>(T[] items, int at, T item)
	{
		var copy = items.ToArray();
		copy[at] = item;
		return copy;
	}

	/// <summary>Each operator's name, by index, as the code and the file write them.</summary>
	public IReadOnlyList<string> Names => _names;

	/// <summary>Whether the patch is code (its file says so): the designer's edits are heard but not saved until it is data again.</summary>
	public bool IsCode { get; private set => Set(ref field, value); }

	/// <summary>Whether the patch can be made code: data in a file, in a project whose C# the host can write.</summary>
	public bool CanEject { get; private set => Set(ref field, value); }

	/// <summary>Makes the patch code: a member of the patches' own C#, its file pointing at it.</summary>
	public ICommand EjectCommand => field ??= new ActionCommand(() => EjectRequested?.Invoke());

	/// <summary>Makes the code patch data again, from what its code builds.</summary>
	public ICommand BringBackCommand => field ??= new ActionCommand(() => BringBackRequested?.Invoke());

	internal event Action? EjectRequested;

	internal event Action? BringBackRequested;

	/// <summary>Says whether the patch is code, and whether it can be made code.</summary>
	internal void ShowCode(bool isCode, bool canEject)
	{
		IsCode = isCode;
		CanEject = canEject;
	}
}
