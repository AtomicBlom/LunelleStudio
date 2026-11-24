using System.Text.Json.Nodes;
using System.Windows.Input;

using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// The roll: the part chosen in Arrange as an editor shows it, when the song has a file and the part is data. A line on
/// rows by degree (click to add, drag to lengthen or move, right-click or a second click to delete; flat, natural, sharp,
/// accent and slide on the note chosen), a beat's rows of hits (a click cycles x X o), a strum's strokes (D U d u x _).
/// Bars are added and taken off whole, so an edit can't break the notation. Every edit is the workbench's to make, so it
/// is heard at the next bar, undone, and written to the file.
/// </summary>
public sealed class RollViewModel : ViewModel
{
	private Action<Action<JsonObject>>? _edit;
	private Action<int>? _gesture;
	private JsonObject? _part;
	private NotationText? _line;
	private (int Start, int Row, int Grab, bool Resize)? _drag;

	internal RollViewModel(Workbench workbench)
	{
		_workbench = workbench;
		UndoCommand = new ActionCommand(() => workbench.UndoEdit());
		RedoCommand = new ActionCommand(() => workbench.RedoEdit());
		RevertCommand = new ActionCommand(() => workbench.RevertPart());
		AddBarCommand = new ActionCommand(() => Bars(add: true));
		RemoveBarCommand = new ActionCommand(() => Bars(add: false));
		FlatCommand = new ActionCommand(() => ChangeNote(n => n with { Accidental = Math.Max(-2, n.Accidental - 1) }));
		NaturalCommand = new ActionCommand(() => ChangeNote(n => n with { Accidental = 0 }));
		SharpCommand = new ActionCommand(() => ChangeNote(n => n with { Accidental = Math.Min(2, n.Accidental + 1) }));
		AccentCommand = new ActionCommand(() => ChangeNote(n => n with { Accent = !n.Accent }));
		SlideCommand = new ActionCommand(() => ChangeNote(n => n with { Slide = !n.Slide }));
		DeleteCommand = new ActionCommand(DeleteSelected);
	}

	/// <summary>What kind of part it shows, or none.</summary>
	public RollKind Kind { get; private set => Set(ref field, value); }

	/// <summary>Whether it shows a part it can edit.</summary>
	public bool IsEditable => Kind != RollKind.None;

	/// <summary>Whether it shows a line, whose notes take flats, sharps and slides as a beat's hits and a strum's strokes don't.</summary>
	public bool IsLine => Kind == RollKind.Line;

	/// <summary>How the roll is worked with the pointer, for the kind of part it shows.</summary>
	public string Hint => Kind switch
	{
		RollKind.Line => "Click to add a note; click above or below where notes start to stack a chord. Drag a note's end to lengthen it (and its chord), or its body to move it. Right-click or double-click to delete. The section's chord notes are highlighted, the root brightest.",
		RollKind.Beat => "Click a step to cycle it: x hit, X accent, o ghost note, off.",
		RollKind.Strum => "Click a step to cycle it: D down, U up, d and u soft, x muted, _ stop, off. Each stroke plays the chord at that step.",
		_ => "",
	};

	/// <summary>The part's name and section, and how it is written.</summary>
	public string Title { get; private set => Set(ref field, value); } = "";

	/// <summary>Whether Shift is held on the computer's keyboard: the wheel then scrolls the roll across.</summary>
	internal bool ShiftHeld => _workbench.ViewModel.ShiftHeld;

	/// <summary>Why the part can't be edited here, or that it has been.</summary>
	public string Status { get; private set => Set(ref field, value); } = "";

	/// <summary>How many steps the part has, and a bar.</summary>
	public int Steps { get; private set; }

	/// <inheritdoc cref="Steps"/>
	public int StepsPerBar { get; private set; }

	/// <summary>Each row's label, top first: a line's degrees ("5'", "1"), a beat's pieces.</summary>
	public IReadOnlyList<string> Rows { get; private set; } = [];

	/// <summary>A line's notes: where each starts, how long it is, its row from the top, its token.</summary>
	public IReadOnlyList<(int Step, int Length, int Row, string Token)> Notes { get; private set; } = [];

	/// <summary>A beat's or a strum's steps, by row from the top then step: "." for none.</summary>
	public IReadOnlyList<IReadOnlyList<string>> Cells { get; private set; } = [];

	/// <summary>Where the note chosen starts, or -1.</summary>
	public int Selected { get; private set; } = -1;

	/// <summary>The row of the note chosen (by <see cref="NoteToken.Row"/>), a chord's notes told apart; meaningless while <see cref="Selected"/> is -1.</summary>
	public int SelectedRow { get; private set; }

	/// <summary>Whether the note shown at <paramref name="step"/> on <paramref name="row"/> (rows from the top) is the one chosen.</summary>
	public bool IsSelected(int step, int row) => step == Selected && _highRow - row == SelectedRow;

	/// <summary>Counts up each time what it shows changes, for the view to draw again.</summary>
	public int Version { get; private set => Set(ref field, value); }

	/// <summary>Whether an edit can be undone, or done again.</summary>
	public bool CanUndo { get; private set => Set(ref field, value); }

	/// <inheritdoc cref="CanUndo"/>
	public bool CanRedo { get; private set => Set(ref field, value); }

	/// <summary>The toolbar's commands.</summary>
	public ICommand UndoCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand RedoCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand RevertCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand AddBarCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand RemoveBarCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand FlatCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand NaturalCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand SharpCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand AccentCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand SlideCommand { get; }

	/// <inheritdoc cref="UndoCommand"/>
	public ICommand DeleteCommand { get; }

	private int _highRow;

	/// <summary>Shows nothing to edit, saying why.</summary>
	internal void ShowNone(string why, string title = "")
	{
		_part = null;
		_line = null;
		_drag = null;
		Kind = RollKind.None;
		Title = title;
		Status = why;
		Bump();
	}

	/// <summary>The step the roll was last asked to show, which the view scrolls to: the bar a click on a lane chose, or 0.</summary>
	public int FocusStep { get; private set; }

	/// <summary>How many times the roll has been asked to show a step; the view scrolls to <see cref="FocusStep"/> as it changes.</summary>
	public int Focused { get; private set => Set(ref field, value); }

	/// <summary>The pieces of the kit a beat's track plays, each a row though the part doesn't play it yet.</summary>
	internal IReadOnlyList<string> KitPieces { get; set; } = [];

	/// <summary>
	/// For a line, whether a step's note on a row (by <see cref="NoteToken.Row"/>) is in the chord sounding there: 2 its root,
	/// 1 another of its notes, 0 neither; and which chord that is, so steps under one chord are told apart from the next.
	/// </summary>
	internal (Func<int, object> ChordAt, Func<int, int, int> Tone)? Chords { get; set; }

	/// <summary>A line's rows that are in the chord over each stretch of steps it sounds for: from, to (past the last), row from the top, whether the root.</summary>
	public IReadOnlyList<(int From, int To, int Row, bool Root)> ChordTones { get; private set; } = [];

	// The rows in each chord's notes, a stretch of steps at a time
	private IReadOnlyList<(int From, int To, int Row, bool Root)> FindChordTones()
	{
		if (Chords is not { } chords || Steps == 0)
			return [];
		var found = new List<(int, int, int, bool)>();
		for (var from = 0; from < Steps;)
		{
			var chord = chords.ChordAt(from);
			var to = from + 1;
			while (to < Steps && Equals(chords.ChordAt(to), chord))
				to++;
			for (var r = 0; r < Rows.Count; r++)
				if (chords.Tone(from, _highRow - r) is var tone and > 0)
					found.Add((from, to, r, tone == 2));
			from = to;
		}
		return found;
	}

	/// <summary>
	/// Shows a part, <paramref name="edit"/> making a change to it (in the song's file, heard at the next bar), and
	/// <paramref name="gesture"/> grouping a drag's edits into one to undo (1 to start, 0 to end).
	/// </summary>
	internal void Show(string title, JsonObject part, int stepsPerBar, bool edited, bool canUndo, bool canRedo,
		Action<Action<JsonObject>> edit, Action<int> gesture, int? focusStep = null)
	{
		var same = _part is not null && Title == title;
		if (focusStep is { } focus)
		{
			FocusStep = focus;
			Focused++;
		}
		_part = part;
		_edit = edit;
		_gesture = gesture;
		Title = title;
		StepsPerBar = stepsPerBar;
		Status = edited ? "Edited · Ctrl Z to undo" : "";
		CanUndo = canUndo;
		CanRedo = canRedo;
		if (!same)
		{
			Selected = -1;
			_drag = null;
		}
		if (part["line"] is not null)
		{
			Kind = RollKind.Line;
			_line = NotationText.Line(part["notes"]?.GetValue<string>() ?? "");
			Steps = _line.Count;
			var notes = new List<(int, int, int, NoteToken)>();
			for (var s = 0; s < _line.Count; s++)
				if (NoteToken.ParseStack(_line[s]) is { } stack)
					foreach (var note in stack)
						notes.Add((s, RollEdits.LengthOf(_line, s), note.Row, note));
			// Three octaves of degrees, the part's own in the middle, widened to hold any note past them: room to scroll to
			var low = Math.Min(-7, notes.Count > 0 ? notes.Min(n => n.Item3) - 2 : -7);
			_highRow = Math.Max(20, notes.Count > 0 ? notes.Max(n => n.Item3) + 2 : 20);
			Rows = [.. Enumerable.Range(0, _highRow - low + 1).Select(r => NoteToken.OnRow(_highRow - r).ToString())];
			Notes = [.. notes.Select(n => (n.Item1, n.Item2, _highRow - n.Item3, n.Item4.ToString()))];
			Cells = [];
		}
		else if (part["beat"] is not null && part["rows"] is JsonObject rows)
		{
			Kind = RollKind.Beat;
			_line = null;
			// The kit's every piece, those the part doesn't play yet empty, so a hit on one is a click away
			var written = rows.Select(r => (IReadOnlyList<string>)[.. NotationText.Characters(r.Value?.GetValue<string>() ?? "").Steps]).ToList();
			var steps = written.Count > 0 ? written[0].Count : 0;
			Rows = [.. rows.Select(r => r.Key), .. KitPieces.Where(p => !rows.ContainsKey(p))];
			Cells = [.. written, .. KitPieces.Where(p => !rows.ContainsKey(p)).Select(_ => (IReadOnlyList<string>)[.. Enumerable.Repeat(".", steps)])];
			Steps = steps;
			Notes = [];
		}
		else if (part["strum"] is not null)
		{
			Kind = RollKind.Strum;
			_line = null;
			var strokes = NotationText.Characters(part["notes"]?.GetValue<string>() ?? "");
			Rows = ["strokes"];
			Cells = [[.. strokes.Steps]];
			Steps = strokes.Count;
			Notes = [];
		}
		else
		{
			ShowNone("This kind of part can't be edited in the roll.");
			return;
		}
		Bump();
	}

	private void Bump()
	{
		ChordTones = Kind == RollKind.Line ? FindChordTones() : [];

		Changed(nameof(IsEditable));
		Changed(nameof(IsLine));
		Changed(nameof(Hint));
		Version++;
	}

	/// <summary>A press on <paramref name="step"/> and <paramref name="row"/> (from the top), as the view hit it.</summary>
	public void Press(int step, int row, bool secondary, bool twice)
	{
		if ((uint)step >= (uint)Steps || (uint)row >= (uint)Rows.Count)
			return;
		switch (Kind)
		{
			case RollKind.Beat:
				var piece = Rows[row];
				EditRow(piece, text => text[step] = RollEdits.NextHit(text[step]));
				return;
			case RollKind.Strum:
				EditNotes(text => text[step] = RollEdits.NextStroke(text[step]), characters: true);
				return;
			case RollKind.Line when _line is { } line:
				var noteRow = _highRow - row;
				var start = RollEdits.NoteAt(line, step, noteRow);
				if (start >= 0 && (secondary || (twice && start == Selected && noteRow == SelectedRow)))
				{
					Selected = -1;
					EditNotes(text => RollEdits.Delete(text, start, noteRow), characters: false);
					return;
				}
				_gesture?.Invoke(1);
				if (start >= 0)
				{
					(Selected, SelectedRow) = (start, noteRow);
					var last = start + RollEdits.LengthOf(line, start) - 1;
					// The last step of a note grabs its end, to lengthen it (and its chord); anywhere else grabs it to move
					_drag = (start, noteRow, step - start, step == last);
					Bump();
					return;
				}
				// Where notes start already, the new one joins them as a chord
				var joins = NoteToken.ParseStack(line[step]) is not null;
				(Selected, SelectedRow) = (step, noteRow);
				_drag = (step, noteRow, 0, !joins);
				EditNotes(text => RollEdits.Place(text, step, noteRow), characters: false);
				return;
		}
	}

	/// <summary>
	/// The pointer dragged to <paramref name="step"/> and <paramref name="row"/> with the button down: a note grabbed by its
	/// end lengthens along its row, and moves once the drag leaves the row; grabbed elsewhere it moves.
	/// </summary>
	public void DragTo(int step, int row)
	{
		if (_drag is not { } drag || _line is null)
			return;
		step = Math.Clamp(step, 0, Steps - 1);
		row = Math.Clamp(row, 0, Rows.Count - 1);
		var toRow = _highRow - row;
		if (drag.Resize && toRow == drag.Row)
		{
			if (step >= drag.Start)
				EditNotes(text => RollEdits.Resize(text, drag.Start, step), characters: false);
			return;
		}
		var to = Math.Clamp(step - drag.Grab, 0, Steps - 1);
		if (to == drag.Start && drag.Row == toRow)
			return;
		EditNotes(text => RollEdits.Move(text, drag.Start, to, toRow, drag.Row), characters: false);
		_drag = (to, toRow, drag.Grab, false);
		(Selected, SelectedRow) = (to, toRow);
		Bump();
	}

	/// <summary>The button let go: a drag's edits end, to undo as one.</summary>
	public void Release()
	{
		if (_drag is null)
			return;
		_drag = null;
		_gesture?.Invoke(0);
	}

	private void ChangeNote(Func<NoteToken, NoteToken> change)
	{
		if (Kind != RollKind.Line || _line is null || Selected < 0 || NoteToken.ParseStack(_line[Selected]) is null)
			return;
		var (start, row) = (Selected, SelectedRow);
		EditNotes(text => RollEdits.Change(text, start, change, row), characters: false);
	}

	private void DeleteSelected()
	{
		if (Kind != RollKind.Line || _line is null || Selected < 0 || NoteToken.ParseStack(_line[Selected]) is null)
			return;
		var (start, row) = (Selected, SelectedRow);
		Selected = -1;
		EditNotes(text => RollEdits.Delete(text, start, row), characters: false);
	}

	private void Bars(bool add)
	{
		if (_part is null || StepsPerBar <= 0)
			return;
		if (Kind == RollKind.Beat)
		{
			_edit?.Invoke(part =>
			{
				var rows = (JsonObject)part["rows"]!;
				foreach (var key in rows.Select(r => r.Key).ToArray())
				{
					var text = NotationText.Characters(rows[key]!.GetValue<string>());
					if (add)
						text.AddBar(StepsPerBar, ".");
					else if (!text.RemoveBar(StepsPerBar))
						return;
					rows[key] = text.ToString();
				}
			});
			return;
		}
		EditNotes(text =>
		{
			if (add)
				text.AddBar(StepsPerBar, ".");
			else
				text.RemoveBar(StepsPerBar);
		}, characters: Kind == RollKind.Strum);
	}

	private void EditNotes(Action<NotationText> change, bool characters) =>
		_edit?.Invoke(part =>
		{
			var text = characters
				? NotationText.Characters(part["notes"]!.GetValue<string>())
				: NotationText.Line(part["notes"]!.GetValue<string>());
			change(text);
			part["notes"] = text.ToString();
		});

	private void EditRow(string piece, Action<NotationText> change) =>
		_edit?.Invoke(part =>
		{
			var rows = (JsonObject)part["rows"]!;
			// A piece the part doesn't play yet comes in empty, as long as the others
			var text = NotationText.Characters(rows[piece]?.GetValue<string>() ?? new string('.', Steps));
			change(text);
			rows[piece] = text.ToString();
		});

	/// <summary>Whether the part shown can be made code: data, in a song whose C# the host can write.</summary>
	public bool CanEject { get; private set => Set(ref field, value); }

	/// <summary>Whether the part shown is code that can be made data again.</summary>
	public bool CanBringBack { get; private set => Set(ref field, value); }

	/// <summary>Where the part's code is, when it is code: "in OpenRoad.cs, line 12".</summary>
	public string CodeWhere { get; private set => Set(ref field, value); } = "";

	/// <summary>Makes the part's notation code, its condition and name staying data.</summary>
	public ICommand EjectNotationCommand => field ??= new ActionCommand(() => _workbench.EjectSelected(placement: false));

	/// <summary>Makes the whole placement code.</summary>
	public ICommand EjectPlacementCommand => field ??= new ActionCommand(() => _workbench.EjectSelected(placement: true));

	/// <summary>Makes the code part data again, from what its code builds.</summary>
	public ICommand BringBackCommand => field ??= new ActionCommand(() => _workbench.BringBackSelected());

	private Workbench _workbench = null!;

	/// <summary>Says what the part chosen can do between data and code.</summary>
	internal void ShowCode(bool canEject, bool canBringBack, string where)
	{
		CanEject = canEject;
		CanBringBack = canBringBack;
		CodeWhere = where;
	}
}
