using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BinaryVibrance.SirrowStudio;

/// <summary>
/// The Files view: the project's music as files, each song's and patch's data and the C# that is theirs, one shown at a
/// time, copied whole or a piece at a time (a class or a member; a track, a section or a part; an operator).
/// </summary>
public sealed class FilesViewModel : ViewModel
{
	private int _shown = -1;

	/// <summary>The files, songs' first, then patches', then C#.</summary>
	public ObservableCollection<FileEntryViewModel> Files { get; } = [];

	/// <summary>Where in <see cref="Files"/> the file shown is, or -1; set, it shows that file.</summary>
	public int ShownIndex
	{
		get => _shown;
		set
		{
			if (value >= 0 && value < Files.Count)
				Files[value].IsSelected = true;
		}
	}

	/// <summary>Raised as <see cref="ShownIndex"/> changes, so a list binds it TwoWay.</summary>
	public event EventHandler? ShownIndexChanged;

	/// <summary>The file shown's name and folder.</summary>
	public string Title { get; private set => Set(ref field, value); } = "Choose a file.";

	/// <summary>The file shown, as it is on disk.</summary>
	public string Text { get; private set => Set(ref field, value); } = "";

	/// <summary>The pieces of the file shown, each copied on its own.</summary>
	public ObservableCollection<FilePieceViewModel> Pieces { get; } = [];

	/// <summary>Copies the file shown, whole.</summary>
	public ICommand CopyFileCommand => field ??= new ActionCommand(() => CopyRequested?.Invoke(Text));

	internal event Action<string>? CopyRequested;

	internal void Shown(FileEntryViewModel entry)
	{
		var at = Files.IndexOf(entry);
		if (Set(ref _shown, at, nameof(ShownIndex)))
			ShownIndexChanged?.Invoke(this, EventArgs.Empty);
	}

	internal void Show(string title, string text, IEnumerable<(string Name, string Text)> pieces)
	{
		Title = title;
		Text = text;
		Pieces.Clear();
		foreach (var (name, piece) in pieces)
			Pieces.Add(new FilePieceViewModel(name, () => CopyRequested?.Invoke(piece)));
	}
}
