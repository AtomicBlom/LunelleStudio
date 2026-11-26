using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using BinaryVibrance.Lunelle.Format;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

// Cues (docs/spec/ui-cues.md): the project's cues from its cue files, each imported into the open song or not; the cue
// strip showing the song's imports; and making, renaming and deleting cues, each one undoable step across every file it
// changes
public sealed partial class Workbench
{
	/// <summary>The project's cue files' cues, by id: what the cues panel lists, and a song can import.</summary>
	public IReadOnlyList<CueDefinition> ProjectCues { get; private set; } = [];

	internal void ReadProjectCuesForTest() => ReadProjectCues();

	// The project's cues read again from its cue files, and the panel's rows with them
	private void ReadProjectCues()
	{
		var cues = new List<CueDefinition>();
		if (_catalog.Root is { } root)
		{
			var problems = new List<FormatProblem>();
			foreach (var file in Files.List(Path.Combine(root, "Cues")).Where(f => f.EndsWith(EntryIds.CuesSuffix, StringComparison.Ordinal)))
			{
				try
				{
					cues.AddRange(CueDefinition.Read(Files.ReadText(Path.Combine(root, "Cues", file)), "Cues/" + file, problems));
				}
				catch (IOException)
				{
				}
			}
		}
		ProjectCues = [.. cues.OrderBy(c => c.Id, StringComparer.Ordinal)];
		ShowProjectCues();
	}

	// The panel's rows: each cue, whether the open song imports it, and whether it reads it
	private void ShowProjectCues()
	{
		var imports = Imports();
		var read = _song?.Cues.Select(c => c.Name).ToHashSet(StringComparer.Ordinal) ?? [];
		var rows = ViewModel.ProjectCues;
		if (!rows.Select(r => r.Id).SequenceEqual(ProjectCues.Select(c => c.Id)))
		{
			rows.Clear();
			foreach (var cue in ProjectCues)
				rows.Add(new CueEntryViewModel(this, cue));
		}
		foreach (var row in rows)
		{
			row.ShowImported(imports.Contains(row.Id));
			row.IsRead = read.Contains(row.Id);
		}
		ViewModel.HasProjectCues = _catalog.Root is not null;
	}

	// The cues the open song imports, by reference, in order; none for a song with no file
	private List<string> Imports()
	{
		if (_editor is null)
			return [];
		try
		{
			return JsonNode.Parse(_editor.Text)?["cues"] is JsonObject cues ? [.. cues.Select(c => c.Key)] : [];
		}
		catch (System.Text.Json.JsonException)
		{
			return [];
		}
	}

	// The runtime cue a song's reference names: a project cue by its id, a library's by the name it is known by
	private CueBase? CueFor(string reference) =>
		_catalog.Cues.FirstOrDefault(c => c.Name == reference)
		?? (reference.IndexOf(':') is var colon and > 0
			? _catalog.Cues.FirstOrDefault(c => c.Name.EndsWith(reference[colon..], StringComparison.Ordinal))
			: null);

	// The cue strip: the song's imports in order, then any cue it reads that it doesn't import (a song of code)
	private void ShowCues(Song song)
	{
		var wanted = new List<CueBase>();
		foreach (var reference in Imports())
			if (CueFor(reference) is { } cue && !wanted.Contains(cue))
				wanted.Add(cue);
		foreach (var cue in song.Cues)
			if (!wanted.Contains(cue))
				wanted.Add(cue);
		if (!ViewModel.Cues.Select(c => c.Cue).SequenceEqual(wanted))
		{
			ViewModel.Cues.Clear();
			foreach (var cue in wanted)
				ViewModel.Cues.Add(new CueViewModel(this, cue, Player.Get(cue)));
		}
		ShowProjectCues();
	}

	// The song just opened starts at the values it previews its cues at (cues.md §3.1): nothing is set for one it doesn't
	private void PreviewCues()
	{
		if (_editor is null || JsonNode.Parse(_editor.Text)?["cues"] is not JsonObject cues)
			return;
		foreach (var (reference, value) in cues)
		{
			if (value is null || CueFor(reference) is not { } cue)
				continue;
			var at = value.GetValueKind() == System.Text.Json.JsonValueKind.String
				? cue.OptionNames.ToList().IndexOf(value.GetValue<string>()) is >= 0 and var option ? cue.OptionValue(option) : float.NaN
				: (float)value.GetValue<double>();
			if (!float.IsNaN(at))
				SetCue(cue, at);
		}
		foreach (var row in ViewModel.Cues)
			row.Show(Player.Get(row.Cue));
	}

	/// <summary>Imports a cue into the open song (cues.md §3), previewed at its initial value; why it can't be, or null.</summary>
	public string? ImportCue(string id)
	{
		if (_editor is null)
			return "This song has no file to import a cue into.";
		if (Imports().Contains(id))
			return null;
		_editor.Edit(root =>
		{
			if (root["cues"] is not JsonObject cues)
				root["cues"] = cues = [];
			cues[id] = null;
		});
		_editor.Save();
		Rebuild(swap: true);
		return null;
	}

	// Each project cue a condition reads that the song doesn't import yet, imported (cues.md §3: choosing a cue imports it)
	private void ImportCuesIn(string condition)
	{
		if (_editor is null)
			return;
		var imports = Imports();
		var missing = ProjectCues.Select(c => c.Id).Where(id => !imports.Contains(id) && Word(id).IsMatch(condition)).ToList();
		if (missing.Count == 0)
			return;
		_editor.Edit(root =>
		{
			if (root["cues"] is not JsonObject cues)
				root["cues"] = cues = [];
			foreach (var id in missing)
				cues[id] = null;
		});
	}

	/// <summary>Takes a cue out of the open song's imports; refused while the song reads it. Why it can't be, or null.</summary>
	public string? UnimportCue(string id)
	{
		if (_editor is null || !Imports().Contains(id))
			return null;
		if (Reads(_editor.Text, id))
			return $"This song reads {id}: take it out of its conditions, levels and macros first.";
		_editor.Edit(root => (root["cues"] as JsonObject)?.Remove(id));
		_editor.Save();
		Rebuild(swap: true);
		return null;
	}

	/// <summary>
	/// Makes a cue in the project's cue files (cues.md §2), in <c>Cues/&lt;namespace&gt;.cues.json</c> for an id with a
	/// namespace (<c>lava/heat</c>) or the project's own cue file, and imports it into the open song: a level, or a choice
	/// among <paramref name="options"/>. One undoable step. Why it can't be, or null.
	/// </summary>
	public string? NewCue(string id, IReadOnlyList<string>? options = null, string? about = null)
	{
		id = id.Trim();
		if (_catalog.Root is not { } root)
			return "This project has no folder for cue files.";
		if (EntryIds.IdProblem(id) is { } bad)
			return $"{bad}: use lowercase letters, digits and hyphens, a '/' before a namespace's name (lava/heat).";
		if (ProjectCues.Any(c => c.Id == id))
			return $"There is a cue {id} already.";
		if (options is { Count: > 0 } && (options.Count < 2 || options.Any(o => !CueDefinition.IsIdentifier(o)) || options.Distinct().Count() != options.Count))
			return "A choice has two or more options, each named as C# names things (Calm, GoodFuture).";
		var slash = id.LastIndexOf('/');
		var space = slash < 0 ? null : id[..slash];
		var name = slash < 0 ? id : id[(slash + 1)..];
		var file = Path.Combine(root, "Cues", (space ?? ProjectName()).Replace('/', '-') + EntryIds.CuesSuffix);
		JObject cues;
		try
		{
			cues = Files.Exists(file) ? (JObject)JReader.Parse(Files.ReadText(file)) : NewCueFile(space);
		}
		catch (Exception ex) when (ex is FormatError or InvalidCastException or IOException)
		{
			return $"{Path.GetFileName(file)} can't be read: {ex.Message}";
		}
		if ((cues["namespace"]?.Str ?? null) != space)
			return $"{Path.GetFileName(file)} is for another namespace: make the cue in its own file.";
		var cue = new JObject().Set("name", new JString(name));
		if (options is { Count: > 0 })
			cue.Set("options", new JArray(options.Select(o => (JNode)new JString(o))));
		if (!string.IsNullOrWhiteSpace(about))
			cue.Set("about", new JString(about.Trim()));
		if (cues["cues"] is not JArray list)
			cues.Set("cues", list = new JArray());
		list.Values.Add(cue);
		using (Document?.Transaction($"new cue {id}"))
		{
			Files.WriteText(file, JWriter.Write(cues) + "\n");
			ReadProjectCues();
			if (_editor is not null)
			{
				_editor.Edit(song =>
				{
					if (song["cues"] is not JsonObject imports)
						song["cues"] = imports = [];
					imports[id] = null;
				});
				_editor.Save();
			}
		}
		ViewModel.BuildText = $"Made the cue {id}. It plays once the project is read again.";
		return null;
	}

	/// <summary>
	/// Renames a cue (cues.md §2) in its cue file and in every song that names it: its imports, its conditions, its levels,
	/// its macros' follows and its exports' events. One undoable step. Why it can't be, or null.
	/// </summary>
	public string? RenameCue(string id, string newId)
	{
		newId = newId.Trim();
		if (_catalog.Root is not { } root)
			return "This project has no folder for cue files.";
		if (ProjectCues.FirstOrDefault(c => c.Id == id) is not { } cue)
			return $"There is no cue {id}.";
		if (newId == id)
			return null;
		if (EntryIds.IdProblem(newId) is { } bad)
			return bad;
		if (ProjectCues.Any(c => c.Id == newId))
			return $"There is a cue {newId} already.";
		var oldSpace = id.LastIndexOf('/') is var a and > 0 ? id[..a] : null;
		var newSpace = newId.LastIndexOf('/') is var b and > 0 ? newId[..b] : null;
		if (oldSpace != newSpace)
			return "A rename keeps the cue's namespace: make a cue in the other namespace and move the songs to it.";
		_editor?.Save();
		using (Document?.Transaction($"rename cue {id} to {newId}"))
		{
			var path = Path.Combine(root, cue.File.Replace('/', Path.DirectorySeparatorChar));
			var file = (JObject)JReader.Parse(Files.ReadText(path));
			foreach (var item in file["cues"]?.Items ?? [])
				if (item is JObject entry && (oldSpace is null ? entry["name"]?.Str : oldSpace + "/" + entry["name"]?.Str) == id)
					entry.Set("name", new JString(newId[(newId.LastIndexOf('/') + 1)..]));
			Files.WriteText(path, JWriter.Write(file) + "\n");
			foreach (var song in Files.List(Path.Combine(root, "Songs")).Where(f => f.EndsWith(".song.json", StringComparison.Ordinal)))
			{
				var songPath = Path.Combine(root, "Songs", song);
				var text = Files.ReadText(songPath);
				if (!Names(text, id))
					continue;
				if (JReader.Parse(text) is not JObject json)
					continue;
				Rename(json, id, newId);
				Files.WriteText(songPath, JWriter.Write(json) + "\n");
			}
		}
		_editor?.ReloadIfChanged();
		ReadProjectCues();
		Rebuild(swap: true);
		return null;
	}

	/// <summary>Deletes a cue no song reads, from its cue file and every song's imports. One undoable step. Why it can't be, or null.</summary>
	public string? DeleteCue(string id)
	{
		if (_catalog.Root is not { } root)
			return "This project has no folder for cue files.";
		if (ProjectCues.FirstOrDefault(c => c.Id == id) is not { } cue)
			return $"There is no cue {id}.";
		_editor?.Save();
		var songs = Files.List(Path.Combine(root, "Songs")).Where(f => f.EndsWith(".song.json", StringComparison.Ordinal)).ToList();
		var readers = songs.Where(s => Reads(Files.ReadText(Path.Combine(root, "Songs", s)), id)).ToList();
		if (readers.Count > 0)
			return $"{string.Join(", ", readers.Select(s => s[..^".song.json".Length]))} {(readers.Count == 1 ? "reads" : "read")} {id}: take it out of them first.";
		using (Document?.Transaction($"delete cue {id}"))
		{
			var path = Path.Combine(root, cue.File.Replace('/', Path.DirectorySeparatorChar));
			var file = (JObject)JReader.Parse(Files.ReadText(path));
			var space = file["namespace"]?.Str;
			if (file["cues"] is JArray list)
				list.Values.RemoveAll(c => (space is null ? c["name"]?.Str : space + "/" + c["name"]?.Str) == id);
			Files.WriteText(path, JWriter.Write(file) + "\n");
			foreach (var song in songs)
			{
				var songPath = Path.Combine(root, "Songs", song);
				if (JReader.Parse(Files.ReadText(songPath)) is JObject json && json["cues"] is JObject imports && imports.Remove(id))
					Files.WriteText(songPath, JWriter.Write(json) + "\n");
			}
		}
		_editor?.ReloadIfChanged();
		ReadProjectCues();
		Rebuild(swap: true);
		return null;
	}

	private string ProjectName()
	{
		if (_catalog.Root is { } root && Files.Exists(Path.Combine(root, EntryIds.Manifest)))
			try
			{
				if (JReader.Parse(Files.ReadText(Path.Combine(root, EntryIds.Manifest)))["name"]?.Str is { Length: > 0 } name)
					return name;
			}
			catch (FormatError)
			{
			}
		return "cues";
	}

	private static JObject NewCueFile(string? space)
	{
		var file = new JObject().Set("format", new JString("lunelle-cues/1"));
		if (space is not null)
			file.Set("namespace", new JString(space));
		return file.Set("cues", new JArray());
	}

	// A cue's id as a word in text: not part of a longer name
	private static Regex Word(string id) => new($@"(?<![\w/:-]){Regex.Escape(id)}(?![\w/-])");

	// Whether a song's text names the cue anywhere (its imports among them)
	private static bool Names(string text, string id) => Word(id).IsMatch(text);

	// Whether a song reads the cue: in a condition, a level, a macro's follow, or an export's event
	private static bool Reads(string text, string id)
	{
		if (JReader.Parse(text) is not JObject json)
			return false;
		var word = Word(id);
		bool Walk(JNode node, string? key) => node switch
		{
			JObject obj => obj.Members.Any(m => m.Key != "cues" && Walk(m.Value, m.Key)),
			JArray array => array.Values.Any(v => Walk(v, key)),
			JString s when key is "when" or "level" or "follow" or "cue" => word.IsMatch(s.Value),
			_ => false,
		};
		return Walk(json, null) || (json["exports"]?.Items.Any(e => e["cues"]?[id] is not null) ?? false);
	}

	// A cue renamed throughout a song's JSON: its import's key, and every place that reads it
	private static void Rename(JObject song, string id, string newId)
	{
		var word = Word(id);
		void Walk(JNode node, string? key)
		{
			switch (node)
			{
				case JObject obj:
					foreach (var member in obj.Members)
					{
						if (member.Value is JString s && member.Key is "when" or "level" or "follow" or "cue")
							member.Value = new JString(word.Replace(s.Value, newId)).At<JString>(s);
						else
							Walk(member.Value, member.Key);
					}
					break;
				case JArray array:
					foreach (var value in array.Values)
						Walk(value, key);
					break;
			}
		}
		Walk(song, null);
		void RenameKey(JObject? map)
		{
			if (map?.Member(id) is not { } member)
				return;
			var at = map.Members.IndexOf(member);
			map.Members[at] = new JMember(newId, member.Value);
		}
		RenameKey(song["cues"] as JObject);
		foreach (var export in song["exports"]?.Items ?? [])
			RenameKey(export["cues"] as JObject);
	}
}
