using System.Windows.Input;

namespace Loom.Audio.Workbench;

/// <summary>A piece of a file, to copy.</summary>
public sealed class FilePieceViewModel(string name, Action copy)
{
	/// <summary>What it is: "class OpenRoad", "Riff", "section cruise".</summary>
	public string Name { get; } = name;

	/// <summary>Copies it.</summary>
	public ICommand CopyCommand { get; } = new ActionCommand(copy);
}
