namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// One of a set where one is chosen (a song, a patch, a kit, a cue's option, a section), for a button lit while it is
/// the one: setting <see cref="IsSelected"/> asks its owner to choose it, and the owner says which is chosen.
/// </summary>
public sealed class ChoiceViewModel : ViewModel
{
	private readonly Action<int> _choose;
	private bool _isSelected;

	internal ChoiceViewModel(int index, string name, Action<int> choose)
	{
		Index = index;
		Name = name;
		_choose = choose;
	}

	/// <summary>Its place in its set.</summary>
	public int Index { get; }

	/// <summary>Its name.</summary>
	public string Name { get; }

	/// <summary>It, for a view to find it from the element showing it (a drag out of the library).</summary>
	public ChoiceViewModel Self => this;

	/// <summary>
	/// Whether it is the one chosen. Setting it true asks for it to be chosen, which shows once its owner has chosen it;
	/// setting it false does nothing, since one is let go only by choosing another.
	/// </summary>
	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (value && !_isSelected)
				_choose(Index);
		}
	}

	/// <summary>Whether the library's filter lets it show.</summary>
	public bool IsVisible
	{
		get;
		internal set => Set(ref field, value);
	} = true;

	internal void Show(bool selected) => Set(ref _isSelected, selected, nameof(IsSelected));
}
