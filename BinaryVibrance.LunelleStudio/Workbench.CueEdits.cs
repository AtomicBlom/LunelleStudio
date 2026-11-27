using System.Globalization;
using System.Text.RegularExpressions;

using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

// Cues made where they are first named (a condition naming a cue the project hasn't), and a cue's file edited from the
// panel: its options, where it starts, and what it is for (docs/spec/ui-cues.md). Each is one undoable step
public sealed partial class Workbench
{
	// A cue compared with a level, and one checked for an option: "lava/heat > 0.5", "biome is Forest"
	private static readonly Regex Compared = new(@"(?<![\w/:-])([a-z0-9][a-z0-9-]*(?:/[a-z0-9][a-z0-9-]*)*)\s*(?:>=|<=|>|<)");
	private static readonly Regex Checked = new(@"(?<![\w/:-])([a-z0-9][a-z0-9-]*(?:/[a-z0-9][a-z0-9-]*)*)\s+is\s+([A-Za-z_][A-Za-z0-9_]*)");

	/// <summary>
	/// The names a condition reads that are no cue of the project's, each as the condition has it: a level (no options),
	/// or a choice among the options it names; none in a project with no cue files.
	/// </summary>
	public IReadOnlyList<(string Id, IReadOnlyList<string> Options)> UnknownCuesIn(string condition)
	{
		if (_catalog.Root is null || string.IsNullOrWhiteSpace(condition))
			return [];
		bool Known(string name) => name == "pass" || _catalog.Cues.Any(c => c.Name == name) || ProjectCues.Any(c => c.Id == name);
		var found = new List<(string Id, List<string> Options)>();
		foreach (Match match in Compared.Matches(condition))
			if (!Known(match.Groups[1].Value) && found.All(f => f.Id != match.Groups[1].Value))
				found.Add((match.Groups[1].Value, []));
		foreach (Match match in Checked.Matches(condition))
		{
			var id = match.Groups[1].Value;
			if (Known(id))
				continue;
			var at = found.FindIndex(f => f.Id == id);
			if (at < 0)
				found.Add((id, [match.Groups[2].Value]));
			else if (!found[at].Options.Contains(match.Groups[2].Value))
				found[at].Options.Add(match.Groups[2].Value);
		}
		return [.. found.Select(f => (f.Id, (IReadOnlyList<string>)f.Options))];
	}

	/// <summary>
	/// Makes each cue a condition names that the project hasn't (<see cref="UnknownCuesIn"/>), as the condition has it,
	/// and imports it into the open song: a level, or a choice among the options it names (and <c>Other</c>, where it
	/// names one, since a choice has two or more). One undoable step. Why it can't be, or null.
	/// </summary>
	public string? MakeCuesIn(string condition)
	{
		var unknown = UnknownCuesIn(condition);
		if (unknown.Count == 0)
			return null;
		using (Document?.Transaction($"new cue{(unknown.Count > 1 ? "s" : "")} {string.Join(", ", unknown.Select(u => u.Id))}"))
			foreach (var (id, options) in unknown)
				if (NewCue(id, options.Count switch { 0 => null, 1 => [options[0], options[0] == "Other" ? "Else" : "Other"], _ => options }) is { } why)
					return why;
		return null;
	}

	// A cue the project's cue files define, as the runtime would make it: for checking a condition before the project
	// has been read again since the cue was made
	private CueBase? DefinedCue(string name) => ProjectCues.FirstOrDefault(c => c.Id == name) is not { } cue ? null
		: cue.IsChoice ? new ChoiceCue(name, [.. cue.Options.Select(o => o.Name)], cue.Options[cue.InitialOption].Name)
		: new Cue(name, (float)cue.InitialLevel);

	/// <summary>Whether the project has code of its own, so a cue is also a name in it (the generator's <c>Cues.Lava.Heat</c>).</summary>
	public bool HasCode => _catalog.Root is not null && _catalog.Folder is null;

	/// <summary>
	/// What a change to cue <paramref name="id"/> does to the game's code, for a project with code: its name there, and
	/// that code using it must change too; "" for a project of files alone.
	/// </summary>
	public string CodeWarning(string id, bool options = false)
	{
		if (!HasCode)
			return "";
		var name = "Cues." + string.Join(".", id.Split('/').Select(Pascal));
		return options
			? $"The game's code has this cue's options as the enum {name}Option: code naming an option taken out won't build until it changes."
			: $"The game's code names this cue {name}: code using that name won't build until it changes too.";
	}

	private static string Pascal(string segment) => string.Concat(segment.Split('-', StringSplitOptions.RemoveEmptyEntries)
		.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

	/// <summary>
	/// Edits cue <paramref name="id"/>'s file entry: a choice's <paramref name="options"/> (kept options keep what they
	/// say; one taken out is refused while a song names it), where it starts (<paramref name="initial"/>: a level from 0 to
	/// 1 or an option's name; "" for the default), and what it is for. A level stays a level and a choice a choice. One
	/// undoable step. Why it can't be, or null.
	/// </summary>
	public string? EditCue(string id, IReadOnlyList<string>? options, string? initial, string? about)
	{
		if (_catalog.Root is not { } root)
			return "This project has no folder for cue files.";
		if (ProjectCues.FirstOrDefault(c => c.Id == id) is not { } cue)
			return $"There is no cue {id}.";
		if (options is not null && !cue.IsChoice)
			return $"{id} is a level: make a choice as a new cue.";
		if (options is not null && (options.Count < 2 || options.Any(o => !CueDefinition.IsIdentifier(o)) || options.Distinct().Count() != options.Count))
			return "A choice has two or more options, each named as C# names things (Calm, GoodFuture).";
		var kept = options ?? [.. cue.Options.Select(o => o.Name)];
		var initialText = initial?.Trim() ?? "";
		if (initialText.Length > 0 && (cue.IsChoice
			? !kept.Contains(initialText)
			: !double.TryParse(initialText, NumberStyles.Float, CultureInfo.InvariantCulture, out var level) || level is < 0 or > 1))
			return cue.IsChoice ? $"{id} starts at one of its options: {string.Join(", ", kept)}." : $"{id} is a level: it starts from 0 to 1.";
		_editor?.Save();
		var songs = Files.List(Path.Combine(root, "Songs")).Where(f => f.EndsWith(".song.json", StringComparison.Ordinal)).ToList();
		foreach (var gone in cue.Options.Select(o => o.Name).Except(kept))
		{
			var naming = new Regex($@"{Word(id)}\s+is\s+{Regex.Escape(gone)}(?!\w)|""{Regex.Escape(gone)}""");
			var readers = songs.Where(s => Names(Files.ReadText(Path.Combine(root, "Songs", s)), id) && naming.IsMatch(Files.ReadText(Path.Combine(root, "Songs", s)))).ToList();
			if (readers.Count > 0)
				return $"{string.Join(", ", readers.Select(s => s[..^".song.json".Length]))} {(readers.Count == 1 ? "names" : "name")} {id}'s option {gone}: take it out of them first.";
		}
		var path = Path.Combine(root, cue.File.Replace('/', Path.DirectorySeparatorChar));
		if (JReader.Parse(Files.ReadText(path)) is not JObject file)
			return $"{cue.File} can't be read.";
		var space = file["namespace"]?.Str;
		var entry = file["cues"]?.Items.OfType<JObject>().FirstOrDefault(c => (space is null ? c["name"]?.Str : space + "/" + c["name"]?.Str) == id);
		if (entry is null)
			return $"{cue.File} doesn't have {id} now.";
		if (options is not null)
		{
			// An option kept keeps its own entry (its about); a new one is its name
			var was = entry["options"]?.Items ?? [];
			entry.Set("options", new JArray(options.Select(o => was.FirstOrDefault(w => (w.Str ?? w["name"]?.Str) == o) ?? new JString(o))));
		}
		if (initialText.Length == 0)
			entry.Remove("initial");
		else
			entry.Set("initial", cue.IsChoice ? new JString(initialText) : JReader.Parse(initialText));
		if (string.IsNullOrWhiteSpace(about))
			entry.Remove("about");
		else
			entry.Set("about", new JString(about.Trim()));
		Seal();
		Files.WriteText(path, JWriter.Write(file) + "\n");
		Seal();
		ReadProjectCues();
		ViewModel.BuildText = $"Changed the cue {id}. It plays so once the project is read again.";
		return null;
	}

	/// <summary>
	/// Has the open song start cue <paramref name="id"/> at the value it has now on the strip (its preview value, cues.md
	/// §3.1). One undoable step. Why it can't be, or null.
	/// </summary>
	public string? PreviewCueHere(string id)
	{
		if (_editor is null)
			return "This song has no file to keep a cue's value in.";
		if (ViewModel.Cues.FirstOrDefault(c => c.Cue.Name == id) is not { } row)
			return $"The song doesn't import {id}.";
		var cue = row.Cue;
		var value = Player.Get(cue);
		System.Text.Json.Nodes.JsonNode preview = cue.OptionNames.Count > 0
			? cue.OptionNames[Enumerable.Range(0, cue.OptionNames.Count).MinBy(i => MathF.Abs(cue.OptionValue(i) - value))]
			: MathF.Round(value, 3);
		Seal();
		_editor.Edit(song =>
		{
			if (song["cues"] is not System.Text.Json.Nodes.JsonObject cues)
				song["cues"] = cues = [];
			cues[id] = preview;
		});
		_editor.Save();
		Seal();
		return null;
	}
}
