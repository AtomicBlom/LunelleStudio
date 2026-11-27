using BinaryVibrance.Lunelle.Format;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A cue of the project's in the cues panel (docs/spec/ui-cues.md): its id, its namespace and name, what it is, and
/// whether the open song imports it; ticking it imports the cue, unticking takes it out (refused while the song reads it).
/// </summary>
public sealed class CueEntryViewModel : LibraryRowViewModel
{
	private readonly Workbench _workbench;
	private bool _imported;

	internal CueEntryViewModel(Workbench workbench, CueDefinition cue)
	{
		_workbench = workbench;
		Id = cue.Id;
		var slash = cue.Id.LastIndexOf('/');
		Namespace = slash < 0 ? "" : cue.Id[..slash];
		Name = slash < 0 ? cue.Id : cue.Id[(slash + 1)..];
		About = cue.About ?? "";
		Kind = cue.IsChoice ? string.Join(" · ", cue.Options.Select(o => o.Name)) : "level";
		MenuCommand = new ActionCommand(() => workbench.ViewModel.RequestCueMenu(this));
	}

	/// <summary>Asks the view for this cue's menu: rename it, or delete it.</summary>
	public System.Windows.Input.ICommand MenuCommand { get; }

	/// <summary>Renames it, in every song (one undoable step); why it can't be, or null.</summary>
	public string? Rename(string newId) => _workbench.RenameCue(Id, newId);

	/// <summary>Deletes it, when no song reads it; why it can't be, or null.</summary>
	public string? Delete() => _workbench.DeleteCue(Id);

	/// <summary>Its id: <c>lava/heat</c>.</summary>
	public override string Id { get; }

	/// <summary>Its namespace, or "" for none.</summary>
	public string Namespace { get; }

	/// <summary>Its name in its namespace.</summary>
	public string Name { get; }

	/// <summary>What it is for, from its file.</summary>
	public string About { get; }

	/// <summary>"level", or a choice's options.</summary>
	public string Kind { get; }

	/// <summary>Whether the open song reads it.</summary>
	public bool IsRead
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>Whether the open song imports it. Setting it imports it, or takes it out.</summary>
	public bool IsImported
	{
		get => _imported;
		set
		{
			if (value == _imported)
				return;
			var why = value ? _workbench.ImportCue(Id) : _workbench.UnimportCue(Id);
			if (why is not null)
			{
				_workbench.ViewModel.Notice = why;
				Changed();
				return;
			}
			Set(ref _imported, value);
		}
	}

	internal void ShowImported(bool imported) => Set(ref _imported, imported, nameof(IsImported));

	/// <summary>Itself, for a row to name.</summary>
	public CueEntryViewModel Self => this;
}
