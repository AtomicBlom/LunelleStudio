using System.Windows.Input;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// One change a derived patch's file makes to its base (docs/spec/ui-overrides.md): a path set, removed or inserted, and
/// its revert, which takes the change out so the base shows through there.
/// </summary>
public sealed class OverrideEntryViewModel : ViewModel
{
	internal OverrideEntryViewModel(Workbench workbench, string kind, string path, string value)
	{
		Kind = kind;
		Path = path;
		Value = value;
		RevertCommand = new ActionCommand(() => workbench.ViewModel.Notice = workbench.RevertOverride(kind, path) ?? "");
	}

	/// <summary>"set", "remove" or "insert".</summary>
	public string Kind { get; }

	/// <summary>The path it changes: <c>filter.cutoff</c>, <c>operators[saw2]</c>.</summary>
	public string Path { get; }

	/// <summary>What it sets there, as written, or "" for a removal.</summary>
	public string Value { get; }

	/// <summary>How the designer says it: "filter.cutoff = 1800".</summary>
	public string Text => Kind switch
	{
		"set" => $"{Path} = {Value}",
		"remove" => $"{Path} removed",
		_ => $"{Path} added",
	};

	/// <summary>Takes the change out of the file.</summary>
	public ICommand RevertCommand { get; }
}
