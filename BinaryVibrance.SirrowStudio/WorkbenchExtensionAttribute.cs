namespace BinaryVibrance.SirrowStudio;

/// <summary>
/// Names the class an assembly's workbench extension is, as <c>[assembly: WorkbenchExtension(typeof(StudioExtras))]</c>:
/// a public class implementing <see cref="IWorkbenchExtension"/> with a constructor that takes nothing.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class WorkbenchExtensionAttribute(Type extension) : Attribute
{
	/// <summary>The extension's class.</summary>
	public Type Extension { get; } = extension ?? throw new ArgumentNullException(nameof(extension));
}
