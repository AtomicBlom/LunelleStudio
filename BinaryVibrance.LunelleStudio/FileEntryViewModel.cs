namespace BinaryVibrance.LunelleStudio;

/// <summary>A file the Files view lists.</summary>
public sealed class FileEntryViewModel(string name, string kind, string path, Action<FileEntryViewModel> choose) : ViewModel
{
	/// <summary>Its name.</summary>
	public string Name { get; } = name;

	/// <summary>What it is: a song, a patch, C#.</summary>
	public string Kind { get; } = kind;

	/// <summary>Where it is, in full.</summary>
	public string Path { get; } = path;

	/// <summary>Whether it is the one shown.</summary>
	public bool IsSelected
	{
		get;
		set
		{
			if (Set(ref field, value) && value)
				choose(this);
		}
	}
}
