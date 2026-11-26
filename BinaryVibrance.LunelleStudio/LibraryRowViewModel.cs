using System.Windows.Input;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A row of the library by its id (docs/spec/ui-library.md): its folder and its leaf, and, as the library's tree, whether
/// it heads its folder (the first of the folder's rows the filter shows, which draws the folder's header) and whether
/// its folder is folded away. The library (<see cref="WorkbenchViewModel.FilterLibrary"/>) sets both.
/// </summary>
public abstract class LibraryRowViewModel : ViewModel
{
	private Action<string>? _fold;

	/// <summary>Its id: <c>bass/sub</c>.</summary>
	public abstract string Id { get; }

	/// <summary>The folder its id is in (format 2's grouping: <c>bass</c> of <c>bass/sub</c>), or "" for none.</summary>
	public string Folder => Id.LastIndexOf('/') is > 0 and var slash ? Id[..slash] : "";

	/// <summary>Its id's last segment: <c>sub</c> of <c>bass/sub</c>.</summary>
	public string Leaf => Id.LastIndexOf('/') is > 0 and var slash ? Id[(slash + 1)..] : Id;

	/// <summary>Whether the library's filter lets it show, its folder open (or the filter's words finding it).</summary>
	public bool IsVisible
	{
		get;
		internal set => Set(ref field, value);
	} = true;

	/// <summary>Whether it draws its folder's header above it: the first of its folder's rows the filter keeps.</summary>
	public bool ShowsFolder
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Whether its folder is folded away.</summary>
	public bool IsFolded
	{
		get;
		internal set
		{
			if (Set(ref field, value))
				Changed(nameof(FolderGlyph));
		}
	}

	/// <summary>The folder header's mark: ▸ folded, ▾ open.</summary>
	public string FolderGlyph => IsFolded ? "▸" : "▾";

	/// <summary>Folds its folder away, or opens it.</summary>
	public ICommand FoldCommand => field ??= new ActionCommand(() => _fold?.Invoke(Folder));

	// Who folds a folder: the library, once the row is in it
	internal void FoldsBy(Action<string> fold) => _fold = fold;
}
