namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// How the player likes the workbench laid out, the same in every project: the app keeps these with its own settings,
/// not beside a project.
/// </summary>
public sealed record WorkbenchPreferences
{
	/// <summary>Whether the library is pinned open as a column.</summary>
	public bool LibraryPinned { get; init; }

	/// <summary>How tall Arrange's part panel is.</summary>
	public float PartPanelHeight { get; init; } = 300f;

	/// <summary>The keyboard's octave.</summary>
	public int Octave { get; init; } = 4;

	/// <summary>The keyboard's velocity.</summary>
	public float Velocity { get; init; } = 0.8f;
}
