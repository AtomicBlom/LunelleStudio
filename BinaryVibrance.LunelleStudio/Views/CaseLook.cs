using BinaryVibrance.Vysara;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// A workbench case set apart from its panels (<see cref="ThemeDefinition.Case"/>): what the transport, the rail and the
/// keyboard dock are made of, their lettering and their keys, round the views of the song.
/// </summary>
internal sealed record CaseLook
{
	/// <summary>The case's face, top to bottom, and the edge round each of its parts.</summary>
	public required (HdrColor Top, HdrColor Bottom) Face { get; init; }
	public required HdrColor Line { get; init; }

	/// <summary>Its lettering: words, quieter words, the quietest.</summary>
	public required HdrColor Ink { get; init; }
	public required HdrColor Dim { get; init; }
	public required HdrColor Faint { get; init; }

	/// <summary>What stands on it (the build's pill).</summary>
	public required HdrColor Raised { get; init; }

	/// <summary>Its keys, and under the pointer, and their legends.</summary>
	public required (HdrColor Top, HdrColor Bottom) KeyFace { get; init; }
	public required (HdrColor Top, HdrColor Bottom) KeyFaceOver { get; init; }
	public required HdrColor KeyInk { get; init; }
}
