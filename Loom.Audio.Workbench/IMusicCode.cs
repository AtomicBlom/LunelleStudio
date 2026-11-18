namespace Loom.Audio.Workbench;

/// <summary>
/// The music's C# as the workbench changes it: a member added to a song's or the patches' own half of its partial class
/// (an eject) or taken out of it (a piece brought back as data), and its files' pieces to copy. A project's host supplies
/// it (it needs Roslyn, which the workbench doesn't reference); without one, nothing ejects.
/// </summary>
public interface IMusicCode
{
	/// <summary>
	/// Adds <paramref name="member"/> (C#, tab-indented) to <paramref name="type"/>'s own half: the source file declaring it
	/// that isn't generated, or a new one beside <paramref name="beside"/> named for the type.
	/// </summary>
	/// <returns>The file, and the line the member starts on.</returns>
	(string File, int Line) AddMember(string type, string beside, string member);

	/// <summary>Takes <paramref name="member"/> out of <paramref name="type"/>'s own half, returning its text, or null when it isn't there.</summary>
	string? RemoveMember(string type, string member);

	/// <summary>The names of the members <paramref name="type"/>'s own half declares.</summary>
	IReadOnlyCollection<string> Members(string type);

	/// <summary>Where <paramref name="member"/> of <paramref name="type"/> is written, or null.</summary>
	(string File, int Line)? Find(string type, string member);

	/// <summary>The music's C# files: each song's and the patches' own halves, in full.</summary>
	IReadOnlyList<string> Files { get; }

	/// <summary>A C# file's pieces to copy: its class, then each member, each as its text (a member's indent taken off).</summary>
	IReadOnlyList<(string Name, string Text)> Pieces(string file);
}
