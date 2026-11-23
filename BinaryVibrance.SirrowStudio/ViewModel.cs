using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinaryVibrance.SirrowStudio;

/// <summary>What the workbench's view models share: they say when a value changes, and only then.</summary>
public abstract class ViewModel : INotifyPropertyChanged
{
	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Sets <paramref name="field"/> to <paramref name="value"/> and says so, if it differs; returns whether it did.</summary>
	protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return false;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		return true;
	}

	/// <summary>Says that <paramref name="name"/> changed, for a value held elsewhere.</summary>
	protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
