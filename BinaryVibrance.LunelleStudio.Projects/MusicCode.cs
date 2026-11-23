using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>
/// The workbench's <see cref="IMusicCode"/> over a project's C#: a type's own half is the source file declaring it that
/// isn't generated; a member is added before its closing brace in the file's own indentation, or a file is made for it
/// beside the music; a member is taken out with the comment over it.
/// </summary>
public sealed class MusicCode(Func<IReadOnlyList<string>> sources) : IMusicCode
{
	/// <summary>The types whose halves are the music's C#: the songs' classes and the patches'.</summary>
	public IReadOnlyCollection<string> Types { get; set; } = [];

	/// <inheritdoc/>
	public IReadOnlyList<string> Files =>
		[.. Types.Select(t => Declaration(t)?.File).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];

	// The type's own declaration: its file, its tree's text and its node
	private (string File, SourceText Text, TypeDeclarationSyntax Node)? Declaration(string type)
	{
		foreach (var file in sources().Concat(_made).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			if (file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
				continue;
			var text = SourceText.From(File.ReadAllText(file));
			var root = CSharpSyntaxTree.ParseText(text).GetRoot();
			foreach (var node in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
				if (FullName(node) == type)
					return (file, text, node);
		}
		return null;
	}

	private static string FullName(TypeDeclarationSyntax node)
	{
		var parts = new List<string> { node.Identifier.ValueText };
		for (var parent = node.Parent; parent is not null; parent = parent.Parent)
			switch (parent)
			{
				case TypeDeclarationSyntax outer:
					parts.Insert(0, outer.Identifier.ValueText);
					break;
				case BaseNamespaceDeclarationSyntax space:
					parts.Insert(0, space.Name.ToString());
					break;
			}
		return string.Join(".", parts);
	}

	/// <inheritdoc/>
	public (string File, int Line) AddMember(string type, string beside, string member)
	{
		ArgumentNullException.ThrowIfNull(member);
		if (Declaration(type) is not { } found)
		{
			var dot = type.LastIndexOf('.');
			var space = dot > 0 ? type[..dot] : "";
			var name = type[(dot + 1)..];
			var path = Path.Combine(beside, name + ".cs");
			var made = new StringBuilder();
			made.Append("using BinaryVibrance.Lunelle.Chip;\nusing BinaryVibrance.Lunelle.Music;\n\n");
			if (space.Length > 0)
				made.Append($"namespace {space};\n\n");
			made.Append($"// What {name}'s music file can't say, as C#: its generated half calls each member here, so the build fails if one\n");
			made.Append("// goes missing or changes its signature\n");
			made.Append($"static partial class {name}\n{{\n");
			made.Append(Indent(member, "    ", "    ")).Append("\n}\n");
			File.WriteAllText(path, made.ToString());
			_made.Add(path);
			return (path, LineOf(made.ToString(), member.Split('\n')[0].Trim()));
		}
		var (file, text, node) = found;
		var source = text.ToString();
		var unit = IndentUnit(source);
		var classIndent = LeadingWhitespace(text, node.SpanStart);
		var memberIndent = node.Members.Count > 0 ? LeadingWhitespace(text, node.Members[0].SpanStart) : classIndent + unit;
		var close = node.CloseBraceToken.SpanStart;
		var lineStart = text.Lines.GetLineFromPosition(close).Start;
		var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
		var insert = (node.Members.Count > 0 ? newline : "") + Indent(member, memberIndent, unit).Replace("\n", newline, StringComparison.Ordinal) + newline;
		var updated = source[..lineStart] + insert + source[lineStart..];
		File.WriteAllText(file, updated);
		return (file, text.Lines.GetLineFromPosition(lineStart).LineNumber + (node.Members.Count > 0 ? 2 : 1));
	}

	/// <inheritdoc/>
	public string? RemoveMember(string type, string member)
	{
		if (Declaration(type) is not { } found || MemberNamed(found.Node, member) is not { } node)
			return null;
		var (file, text, _) = found;
		var source = text.ToString();
		// The member with the comment over it, from the start of its first line through the end of its last, and a blank
		// line before it if one parts it from the member above
		var start = text.Lines.GetLineFromPosition(node.FullSpan.Start).Start;
		var first = node.GetLeadingTrivia().FirstOrDefault(t => !t.IsKind(SyntaxKind.EndOfLineTrivia) && !t.IsKind(SyntaxKind.WhitespaceTrivia));
		if (first != default)
			start = text.Lines.GetLineFromPosition(first.SpanStart).Start;
		else
			start = text.Lines.GetLineFromPosition(node.SpanStart).Start;
		var end = text.Lines.GetLineFromPosition(node.Span.End).EndIncludingLineBreak;
		var removed = source[start..end];
		var line = text.Lines.GetLineFromPosition(start).LineNumber;
		if (line > 0 && text.Lines[line - 1].ToString().Trim().Length == 0)
			start = text.Lines[line - 1].Start;
		File.WriteAllText(file, source[..start] + source[end..]);
		return Dedent(removed.TrimEnd());
	}

	/// <inheritdoc/>
	public IReadOnlyCollection<string> Members(string type) =>
		Declaration(type) is { } found ? [.. found.Node.Members.Select(NameOf).OfType<string>()] : [];

	/// <inheritdoc/>
	public (string File, int Line)? Find(string type, string member) =>
		Declaration(type) is { } found && MemberNamed(found.Node, member) is { } node
			? (found.File, found.Text.Lines.GetLineFromPosition(node.SpanStart).LineNumber + 1)
			: null;

	/// <inheritdoc/>
	public IReadOnlyList<(string Name, string Text)> Pieces(string file)
	{
		if (!File.Exists(file))
			return [];
		var text = SourceText.From(File.ReadAllText(file));
		var pieces = new List<(string, string)>();
		foreach (var type in CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
		{
			pieces.Add(($"class {type.Identifier.ValueText}", Dedent(Lines(text, type))));
			foreach (var member in type.Members)
				if (NameOf(member) is { } name)
					pieces.Add((name, Dedent(Lines(text, member))));
		}
		return pieces;
	}

	// A node's whole lines, from the start of its first (with the comment over it) to the end of its last
	private static string Lines(SourceText text, SyntaxNode node)
	{
		var comment = node.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
		var start = text.Lines.GetLineFromPosition(comment != default ? comment.SpanStart : node.SpanStart).Start;
		return text.ToString(TextSpan.FromBounds(start, text.Lines.GetLineFromPosition(node.Span.End).End));
	}

	private static MemberDeclarationSyntax? MemberNamed(TypeDeclarationSyntax type, string name) =>
		type.Members.LastOrDefault(m => NameOf(m) == name && !IsDeclarationOnly(m))
		?? type.Members.FirstOrDefault(m => NameOf(m) == name);

	// A partial member's defining half has no body: the half to take out is the implementation
	private static bool IsDeclarationOnly(MemberDeclarationSyntax member) =>
		member is MethodDeclarationSyntax { Body: null, ExpressionBody: null };

	private static string? NameOf(MemberDeclarationSyntax member) => member switch
	{
		MethodDeclarationSyntax m => m.Identifier.ValueText,
		PropertyDeclarationSyntax p => p.Identifier.ValueText,
		FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText,
		_ => null,
	};

	private static string LeadingWhitespace(SourceText text, int position)
	{
		var line = text.Lines.GetLineFromPosition(position).ToString();
		return line[..(line.Length - line.TrimStart().Length)];
	}

	// The file's step of indentation: a tab, or the spaces its first indented line starts with
	private static string IndentUnit(string source)
	{
		foreach (var line in source.Split('\n'))
		{
			if (line.StartsWith('\t'))
				return "\t";
			if (line.StartsWith(' ') && line.Trim().Length > 0)
				return new string(' ', line.Length - line.TrimStart(' ').Length);
		}
		return "    ";
	}

	// Tab-indented text at an indent, each tab its unit
	private static string Indent(string member, string indent, string unit) =>
		string.Join("\n", member.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
			.Select(line => line.Length == 0 ? "" : indent + line.Replace("\t", unit, StringComparison.Ordinal)));

	// The indent the first line has, off every line
	private static string Dedent(string text)
	{
		var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
		return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
	}

	private static int LineOf(string text, string first)
	{
		var lines = text.Split('\n');
		for (var i = 0; i < lines.Length; i++)
			if (lines[i].Trim() == first)
				return i + 1;
		return 1;
	}

	// Files made here, which the project's sources take up only at its next design-time build
	private readonly List<string> _made = [];
}
