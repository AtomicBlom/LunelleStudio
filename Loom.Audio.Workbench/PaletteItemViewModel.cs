namespace Loom.Audio.Workbench;

/// <summary>One thing the command palette can do: choose a song, a patch or a kit, jump to a section, run a command.</summary>
public sealed class PaletteItemViewModel : ViewModel
{
	private readonly Action _run;
	private bool _isSelected;

	internal PaletteItemViewModel(string category, string text, Action run)
	{
		Category = category;
		Text = text;
		_run = run;
	}

	/// <summary>What kind of thing it is: "Song", "Patch", "Section", "Command".</summary>
	public string Category { get; }

	/// <summary>What it says.</summary>
	public string Text { get; }

	/// <summary>Whether Enter runs it.</summary>
	public bool IsSelected
	{
		get => _isSelected;
		internal set => Set(ref _isSelected, value);
	}

	internal void Run() => _run();

	internal bool Matches(string[] words)
	{
		foreach (var word in words)
			if (!Category.Contains(word, StringComparison.OrdinalIgnoreCase) && !Text.Contains(word, StringComparison.OrdinalIgnoreCase))
				return false;
		return true;
	}
}
