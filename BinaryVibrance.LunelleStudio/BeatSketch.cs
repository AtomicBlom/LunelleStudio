using System.Collections.ObjectModel;
using System.Windows.Input;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// The beat sketcher: a bar of sixteenths per piece of the kit chosen, played on a loop through the sequencer (so it
/// sounds as a song would play it), and written as the <c>Beat.Sixteenths(…)</c> a song would place.
/// </summary>
public sealed class BeatSketch : ViewModel
{
	// The pieces Kit names, written as Kit.Name; another is written as new DrumPiece("id")
	private static readonly HashSet<string> Named =
		["kick", "snare", "snare-rim", "hat-closed", "hat-open", "hat-pedal", "tom-high", "tom-mid", "tom-low", "crash", "ride", "ride-bell"];

	// A groove to start from, where the kit has the pieces
	private static readonly Dictionary<string, string> Start = new()
	{
		["kick"] = "x.......x.x.....",
		["snare"] = "....x.......x...",
		["hat-closed"] = "x.x.x.x.x.x.x.x.",
	};

	internal BeatSketch()
	{
	}

	/// <summary>Raised when what it plays changes, or it starts or stops.</summary>
	internal event Action? Edited;

	/// <summary>The kit's pieces, each a row of steps.</summary>
	public ObservableCollection<BeatRowViewModel> Rows { get; } = [];

	/// <summary>The kit it plays, or null before one is chosen.</summary>
	public DrumKit? Kit { get; private set; }

	/// <summary>The tempo it loops at.</summary>
	public float Tempo
	{
		get;
		set
		{
			if (Set(ref field, Math.Clamp(value, 40f, 240f)))
				Edited?.Invoke();
		}
	} = 120f;

	/// <summary>Whether it plays, on a loop.</summary>
	public bool IsLooping
	{
		get;
		set
		{
			if (Set(ref field, value))
				Edited?.Invoke();
		}
	}

	/// <summary>The beat as a song would place it.</summary>
	public string Code
	{
		get;
		private set => Set(ref field, value);
	} = "";

	/// <summary>Turns every step off.</summary>
	public ICommand ClearCommand => field ??= new ActionCommand(() =>
	{
		foreach (var row in Rows)
			foreach (var step in row.Steps)
				step.Value = '.';
		OnStepsChanged();
	});

	/// <summary>Asks for <see cref="Code"/> to be copied.</summary>
	public ICommand CopyCommand => field ??= new ActionCommand(() => CopyRequested?.Invoke(Code));

	internal event Action<string>? CopyRequested;

	/// <summary>Opens <paramref name="kit"/>: a row per piece, keeping what each piece played if the last kit had it too.</summary>
	internal void Load(DrumKit kit)
	{
		var was = Rows.ToDictionary(r => r.Piece.Id, r => new string([.. r.Steps.Select(s => s.Value)]));
		Kit = kit;
		Rows.Clear();
		foreach (var piece in kit.Pieces)
		{
			var id = piece.Piece.Id;
			var pattern = was.GetValueOrDefault(id) ?? (was.Count == 0 ? Start.GetValueOrDefault(id) : null) ?? "";
			Rows.Add(new BeatRowViewModel(piece.Piece, pattern, OnStepsChanged));
		}
		OnStepsChanged();
	}

	/// <summary>Lights the step playing, or none with −1.</summary>
	internal void ShowStep(int step)
	{
		foreach (var row in Rows)
			foreach (var s in row.Steps)
				s.IsNow = s.Index == step;
	}

	/// <summary>The song that plays it: one bar of its beat on the kit, at its tempo; null when no step plays.</summary>
	internal Song? Song()
	{
		if (Kit is not { } kit || Rows.All(r => r.IsEmpty))
			return null;
		var song = new Song("beat") { Tempo = Tempo };
		var drums = song.Track("kit", kit);
		song.Section("beat", 1).Play(drums, Beat.Sixteenths([.. Rows.Where(r => !r.IsEmpty).Select(r => (r.Piece, r.Pattern))]));
		return song;
	}

	private void OnStepsChanged()
	{
		var rows = Rows.Where(r => !r.IsEmpty).Select(r => $"\t({PieceCode(r.Piece)}, \"{r.Pattern}\")").ToArray();
		Code = rows.Length == 0 ? "Beat.Sixteenths()" : "Beat.Sixteenths(\n" + string.Join(",\n", rows) + ")";
		Edited?.Invoke();
	}

	private static string PieceCode(DrumPiece piece) =>
		Named.Contains(piece.Id) ? "Kit." + PatchCode.Pascal(piece.Id) : $"new DrumPiece(\"{piece.Id}\")";
}
