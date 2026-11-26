using System.Windows.Input;

namespace BinaryVibrance.LunelleStudio;

/// <summary>A pattern of the project's in the library (docs/spec/ui-patterns.md): its use puts it into the part chosen.</summary>
public sealed class PatternEntryViewModel : ViewModel
{
	internal PatternEntryViewModel(Workbench workbench, string id)
	{
		Id = id;
		UseCommand = new ActionCommand(() => workbench.ViewModel.Notice = workbench.UsePattern(id) ?? "");
	}

	/// <summary>Its id: <c>drums/groove</c>.</summary>
	public string Id { get; }

	/// <summary>Has the part chosen play it.</summary>
	public ICommand UseCommand { get; }

	/// <summary>Itself, for a row to name.</summary>
	public PatternEntryViewModel Self => this;
}
