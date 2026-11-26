using System.Windows.Input;

namespace BinaryVibrance.LunelleStudio;

/// <summary>A pattern of the project's in the library (docs/spec/ui-patterns.md): its use puts it into the part chosen.</summary>
public sealed class PatternEntryViewModel : LibraryRowViewModel
{
	internal PatternEntryViewModel(Workbench workbench, string id)
	{
		Id = id;
		UseCommand = new ActionCommand(() => workbench.ViewModel.Notice = workbench.UsePattern(id) ?? "");
	}

	/// <summary>Its id: <c>drums/groove</c>.</summary>
	public override string Id { get; }

	/// <summary>Why it wouldn't fit the part chosen (a beat over a patch), or null where it would.</summary>
	public string? Misfit
	{
		get;
		internal set
		{
			if (Set(ref field, value))
			{
				Changed(nameof(Fit));
				Changed(nameof(Tip));
			}
		}
	}

	/// <summary>How brightly it shows: dimmed where it wouldn't fit the part chosen.</summary>
	public float Fit => Misfit is null ? 1f : 0.45f;

	/// <summary>Its row's tooltip: its id, and why it wouldn't fit the part chosen.</summary>
	public string Tip => Misfit is null ? $"{Id}: drag it onto a part or a lane in Arrange, Shift to drop a copy" : Misfit;

	/// <summary>Has the part chosen play it.</summary>
	public ICommand UseCommand { get; }

	/// <summary>Itself, for a row to name.</summary>
	public PatternEntryViewModel Self => this;
}
