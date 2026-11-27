using System.Windows.Input;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A preset in the library (<see cref="Workbench.Presets"/>): tried on the keyboard with a click, and added to the project
/// with its button, which opens it instead once the project has one of its name.
/// </summary>
public sealed class PresetViewModel : ViewModel
{
	private readonly Workbench _workbench;

	internal PresetViewModel(Preset preset, Workbench workbench)
	{
		Preset = preset;
		_workbench = workbench;
		TryCommand = new ActionCommand(() => workbench.TryPreset(preset));
		AddCommand = new ActionCommand(Add);
	}

	/// <summary>The preset.</summary>
	public Preset Preset { get; }

	/// <summary>Its name.</summary>
	public string Name => Preset.Name;

	/// <summary>The kind of instrument it is: "keys", "bass", "sampled".</summary>
	public string Category => Preset.Category;

	/// <summary>Whether it is a kit.</summary>
	public bool IsKit => Preset.IsKit;

	/// <summary>Whether it is a patch, for its row's icon.</summary>
	public bool IsPatch => !Preset.IsKit;

	/// <summary>It, for a view to find it from the element showing it.</summary>
	public PresetViewModel Self => this;

	/// <summary>Whether the library's filter lets it show.</summary>
	public bool IsVisible
	{
		get;
		internal set => Set(ref field, value);
	} = true;

	/// <summary>Whether the project has a patch or kit of its name, which its button then opens.</summary>
	public bool InProject
	{
		get;
		internal set
		{
			if (Set(ref field, value))
			{
				Changed(nameof(AddText));
				Changed(nameof(AddTip));
			}
		}
	}

	/// <summary>What its button says: "add", or "open" once the project has it.</summary>
	public string AddText => InProject ? "open" : "add";

	/// <summary>What its button does, said.</summary>
	public string AddTip => InProject
		? $"The project has {Name}: open it"
		: IsKit ? $"Copy {Name} into the project, with its samples or the patches it plays, and open it" : $"Copy {Name} into the project's patches and open it";

	/// <summary>Plays it on the keyboard, as the library has it.</summary>
	public ICommand TryCommand { get; }

	/// <summary>Copies it into the project, or opens the project's.</summary>
	public ICommand AddCommand { get; }

	private void Add()
	{
		if (_workbench.AddPreset(Preset) is { } why)
			_workbench.ViewModel.Error = why;
	}
}
