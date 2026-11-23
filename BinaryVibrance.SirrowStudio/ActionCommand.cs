using System.Windows.Input;

namespace BinaryVibrance.SirrowStudio;

/// <summary>A command that always can, and does one thing: what a button in a list's template binds to, having no handler of its own.</summary>
internal sealed class ActionCommand(Action action) : ICommand
{
	/// <inheritdoc/>
	public event EventHandler? CanExecuteChanged
	{
		add { }
		remove { }
	}

	/// <inheritdoc/>
	public bool CanExecute(object? parameter) => true;

	/// <inheritdoc/>
	public void Execute(object? parameter) => action();
}
