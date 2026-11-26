using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Format;

namespace BinaryVibrance.LunelleStudio;

// A derived patch in the designer (docs/spec/ui-overrides.md): what it is based on, each change its file makes, and
// each change reverted, the base showing through there again
public sealed partial class Workbench
{
	// The designer told what the open patch's file changes over its base: none for a whole patch
	private void ShowOverrides(JObject? file)
	{
		var designer = ViewModel.Designer;
		designer.Overrides.Clear();
		var based = file?["base"]?.Str;
		designer.BasedOn = based ?? "";
		designer.IsDerived = based is not null && _patchBelow is not null;
		if (!designer.IsDerived)
			return;
		foreach (var path in file!["remove"]?.Items ?? [])
			if (path.Str is { } text)
				designer.Overrides.Add(new OverrideEntryViewModel(this, "remove", text, ""));
		foreach (var insert in file["insert"]?.Items ?? [])
			if (insert["into"]?.Str is { } into && insert["item"] is JObject item
				&& OverrideSchema.Patch.Under(into.Split('.')[^1]) is { Key: { } key } && key(item) is { } named)
				designer.Overrides.Add(new OverrideEntryViewModel(this, "insert", $"{into}[{named}]", JWriter.Write(item)));
		if (file["set"] is JObject sets)
			foreach (var member in sets.Members)
				designer.Overrides.Add(new OverrideEntryViewModel(this, "set", member.Key, JWriter.Write(member.Value)));
	}

	/// <summary>
	/// Takes one change out of the open derived patch's file (overrides.md §4: a revert), so its base shows through there;
	/// the designer shows the patch as it now resolves. One undoable step. Why it can't be, or null.
	/// </summary>
	public string? RevertOverride(string kind, string path)
	{
		if (_patchFile is null || _patchBelow is not { } below)
			return "The patch open isn't derived from another.";
		SavePatch();
		if (JReader.Parse(Files.ReadText(_patchFile)) is not JObject file)
			return "The patch's file can't be read.";
		var changed = kind switch
		{
			"set" => (file["set"] as JObject)?.Remove(path) == true,
			"remove" => (file["remove"] as JArray)?.Values.RemoveAll(v => v.Str == path) > 0,
			_ => (file["insert"] as JArray)?.Values.RemoveAll(i => i["into"]?.Str is { } into && i["item"] is JObject item
				&& OverrideSchema.Patch.Under(into.Split('.')[^1]) is { Key: { } key } && $"{into}[{key(item)}]" == path) > 0,
		};
		if (!changed)
			return $"The patch's file doesn't {kind} {path} now.";
		foreach (var empty in new[] { "set", "remove", "insert" })
			if (file[empty] is { } node && (node is JObject { Members.Count: 0 } || node is JArray { Values.Count: 0 }))
				file.Remove(empty);
		Seal();
		Files.WriteText(_patchFile, JWriter.Write(file) + "\n");
		Seal();
		// The patch as it now resolves: its base with what is left of its changes
		var whole = (JObject)below.Clone();
		Overrides.Apply(whole, file, OverrideSchema.Patch, _patchFile, "", [], []);
		whole.Set("format", new JString(PatchFile.Format));
		var spec = PatchFile.Read(whole.ToString(), out var names);
		var name = (uint)_benchPatch < (uint)_catalog.Patches.Count ? _catalog.Patches[_benchPatch].Name : spec.Name;
		spec.Name = name;
		ViewModel.Designer.Load(spec.ToPatch(), _catalog.MacroName, keepEdits: false, names: names);
		ShowOverrides(file);
		return null;
	}
}
