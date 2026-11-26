using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A project's files as the studio edits them (docs/spec/transactions.md): the files on disk are the last save, and every
/// write since is a step in a log on top of them, which undo and redo walk and <see cref="Save"/> writes. Reads, lists
/// and watches see the edited project, so everything that reads music follows every edit, undo and redo. The log is kept
/// on disk as it grows (<c>.lunelle-studio/journal.jsonl</c>), so a crash loses nothing: opening the project again
/// recovers it. What is under <c>.lunelle-studio/</c> (the workbench's own state) and what is outside the project is
/// written straight through.
/// </summary>
public sealed class ProjectDocument : IMusicFiles
{
	/// <summary>How long after a write another to the same file joins it as one step (§3.1).</summary>
	public static readonly TimeSpan JoinWithin = TimeSpan.FromSeconds(2);

	/// <summary>The journal's path, under the project.</summary>
	public const string JournalPath = ".lunelle-studio/journal.jsonl";

	private readonly IMusicFiles _disk;
	private readonly string _root;
	private readonly Func<DateTime> _clock;
	private readonly Dictionary<string, byte[]?> _edited = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<Transaction> _done = [];
	private readonly List<Transaction> _undone = [];
	private readonly List<(string Folder, Action<string> Changed)> _watches = [];
	private readonly StringBuilder _journal = new();
	private Transaction? _open;
	private int _nextId = 1;
	private bool _replaying;

	private ProjectDocument(IMusicFiles disk, string root, Func<DateTime>? clock)
	{
		_disk = disk;
		_root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		_clock = clock ?? (() => DateTime.UtcNow);
	}

	/// <summary>
	/// The project at <paramref name="root"/> on <paramref name="disk"/>, with the edits its journal holds recovered: each
	/// still undoable. <paramref name="clock"/> times the joining of writes (the real one, by default).
	/// </summary>
	public static ProjectDocument Open(IMusicFiles disk, string root, Func<DateTime>? clock = null)
	{
		ArgumentNullException.ThrowIfNull(disk);
		ArgumentNullException.ThrowIfNull(root);
		var document = new ProjectDocument(disk, root, clock);
		document.Recover();
		return document;
	}

	/// <summary>The project's folder, in full.</summary>
	public string Root => _root;

	/// <summary>Whether the document differs from the disk: something to save.</summary>
	public bool IsDirty => _edited.Count > 0;

	/// <summary>Whether there is a transaction to undo.</summary>
	public bool CanUndo => _done.Count > 0;

	/// <summary>Whether there is a transaction to redo.</summary>
	public bool CanRedo => _undone.Count > 0;

	/// <summary>The files the document has edited, by their paths in the project.</summary>
	public IReadOnlyList<string> EditedFiles => [.. _edited.Keys.Select(Relative).Order(StringComparer.Ordinal)];

	/// <summary>What recovering the journal did, or what it couldn't: a line each, for the studio to say.</summary>
	public IReadOnlyList<string> Recovered { get; private set; } = [];

	/// <summary>
	/// Files the document has edited that were changed on disk since, by their paths in the project: saving replaces
	/// what is on disk (§6). Cleared by a save or a revert.
	/// </summary>
	public IReadOnlyCollection<string> ChangedOutside => _changedOutside;

	private readonly HashSet<string> _changedOutside = new(StringComparer.Ordinal);

	/// <summary>Raised when the document's state changes: an edit, an undo or redo, a save or revert, a change outside.</summary>
	public event Action? StateChanged;

	/// <inheritdoc/>
	public bool Exists(string path) =>
		_edited.TryGetValue(Path.GetFullPath(path), out var content) ? content is not null : _disk.Exists(path);

	/// <inheritdoc/>
	public string ReadText(string path) => Encoding.UTF8.GetString(ReadBytes(path));

	/// <inheritdoc/>
	public byte[] ReadBytes(string path) =>
		_edited.TryGetValue(Path.GetFullPath(path), out var content)
			? content ?? throw new FileNotFoundException($"{path} was deleted, and the deletion isn't saved.", path)
			: _disk.ReadBytes(path);

	/// <inheritdoc/>
	public void WriteText(string path, string text) => WriteBytes(path, Encoding.UTF8.GetBytes(text));

	/// <inheritdoc/>
	public void WriteBytes(string path, byte[] bytes)
	{
		ArgumentNullException.ThrowIfNull(bytes);
		Change(Path.GetFullPath(path), bytes);
	}

	/// <inheritdoc/>
	public void Delete(string path) => Change(Path.GetFullPath(path), null);

	/// <inheritdoc/>
	public IEnumerable<string> List(string folder)
	{
		var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var listed = new HashSet<string>(_disk.List(folder), StringComparer.Ordinal);
		foreach (var (path, content) in _edited)
		{
			if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
				continue;
			var relative = path[root.Length..].Replace('\\', '/');
			if (content is null)
				listed.Remove(relative);
			else
				listed.Add(relative);
		}
		return listed.Order(StringComparer.Ordinal).ToList();
	}

	/// <inheritdoc/>
	public IDisposable Watch(string folder, Action<string> changed)
	{
		var watch = (Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), changed);
		_watches.Add(watch);
		// The disk's changes pass on, but for a file the document has edited, whose edit stands (§6)
		var disk = _disk.Watch(folder, path =>
		{
			var full = Path.GetFullPath(path);
			// The journal is the document's own
			if (string.Equals(full, Path.GetFullPath(Path.Combine(_root, JournalPath)), StringComparison.OrdinalIgnoreCase))
				return;
			lock (_edited)
			{
				if (_edited.ContainsKey(full))
				{
					if (_changedOutside.Add(Relative(full)))
						StateChanged?.Invoke();
					return;
				}
			}
			changed(path);
		});
		return new Unwatch(() =>
		{
			disk.Dispose();
			_watches.Remove(watch);
		});
	}

	/// <summary>
	/// Ends the last step, so the next write is a step of its own (§3.1): the workbench calls it when the user turns to
	/// something else.
	/// </summary>
	public void Seal()
	{
		if (_done.Count > 0)
			_done[^1].Sealed = true;
	}

	/// <summary>
	/// Starts a transaction: every write until the result is disposed is one step to undo and redo together (renaming a
	/// cue across the songs that name it). Transactions don't nest: one begun inside another joins it.
	/// </summary>
	public IDisposable Transaction(string label)
	{
		if (_open is not null)
			return new Unwatch(() => { });
		Seal();
		_open = new Transaction(_nextId++, label, _clock());
		return new Unwatch(() =>
		{
			var open = _open;
			_open = null;
			if (open is { Steps.Count: > 0 })
			{
				open.Sealed = true;
				_done.Add(open);
				StateChanged?.Invoke();
			}
		});
	}

	/// <summary>Undoes the last transaction; whether there was one.</summary>
	public bool Undo()
	{
		if (_open is not null || _done.Count == 0)
			return false;
		var transaction = _done[^1];
		_done.RemoveAt(_done.Count - 1);
		transaction.Sealed = true;
		for (var i = transaction.Steps.Count - 1; i >= 0; i--)
			Put(transaction.Steps[i].Path, transaction.Steps[i].Before);
		_undone.Add(transaction);
		Journal(new JsonObject { ["op"] = "undo", ["tx"] = transaction.Id });
		Notify(transaction.Steps.Select(s => s.Path));
		return true;
	}

	/// <summary>Redoes the last transaction undone; whether there was one.</summary>
	public bool Redo()
	{
		if (_open is not null || _undone.Count == 0)
			return false;
		var transaction = _undone[^1];
		_undone.RemoveAt(_undone.Count - 1);
		foreach (var step in transaction.Steps)
			Put(step.Path, step.After);
		_done.Add(transaction);
		Journal(new JsonObject { ["op"] = "redo", ["tx"] = transaction.Id });
		Notify(transaction.Steps.Select(s => s.Path));
		return true;
	}

	/// <summary>Writes every edit to disk, deletes what was deleted, and clears the log (§4): undo doesn't reach past it.</summary>
	public void Save()
	{
		List<KeyValuePair<string, byte[]?>> edits;
		lock (_edited)
			edits = [.. _edited];
		foreach (var (path, content) in edits)
		{
			// Each file written as it is saved, so its own watch hears the disk say what the document did
			lock (_edited)
				_edited.Remove(path);
			if (content is null)
				_disk.Delete(path);
			else
				_disk.WriteBytes(path, content);
		}
		Clear();
	}

	/// <summary>Drops every edit since the save (§4): the document is the disk again.</summary>
	public void Revert()
	{
		List<string> paths;
		lock (_edited)
		{
			paths = [.. _edited.Keys];
			_edited.Clear();
		}
		Clear();
		Notify(paths);
	}

	private void Clear()
	{
		_done.Clear();
		_undone.Clear();
		_changedOutside.Clear();
		_journal.Clear();
		_disk.Delete(Path.Combine(_root, JournalPath));
		StateChanged?.Invoke();
	}

	// A write or deletion: straight through outside the project or in the workbench's own folder; else a step
	private void Change(string path, byte[]? content)
	{
		if (!Inside(path))
		{
			if (content is null)
				_disk.Delete(path);
			else
				_disk.WriteBytes(path, content);
			return;
		}
		var before = Current(path);
		if (Same(before, content))
			return;
		var now = _clock();
		var target = _open;
		if (target is null && _done.Count > 0 && _done[^1] is { Sealed: false } last && last.Steps.Count == 1
			&& string.Equals(last.Steps[0].Path, path, StringComparison.OrdinalIgnoreCase) && now - last.Last <= JoinWithin)
			target = last;
		if (target is null)
		{
			target = new Transaction(_nextId++, Relative(path), now);
			_done.Add(target);
		}
		target.Last = now;
		var step = target.Steps.Find(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));
		if (step is null)
			target.Steps.Add(new Step(path, before, content));
		else
			step.After = content;
		_undone.Clear();
		Put(path, content);
		Journal(new JsonObject
		{
			["op"] = "do",
			["tx"] = target.Id,
			["path"] = Relative(path),
			["before"] = Text(step?.Before ?? before),
			["after"] = Text(content),
		});
		Notify([path]);
	}

	// The document's content now, or null for none
	private byte[]? Current(string path)
	{
		lock (_edited)
			if (_edited.TryGetValue(path, out var content))
				return content;
		return _disk.Exists(path) ? _disk.ReadBytes(path) : null;
	}

	// Sets the document's content, dropping the edit where it is the disk's again
	private void Put(string path, byte[]? content)
	{
		var disk = _disk.Exists(path) ? _disk.ReadBytes(path) : null;
		lock (_edited)
		{
			if (Same(disk, content))
				_edited.Remove(path);
			else
				_edited[path] = content;
		}
	}

	private bool Inside(string path) =>
		path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
		&& !Relative(path).StartsWith(".lunelle-studio/", StringComparison.Ordinal);

	private string Relative(string path) => Path.GetRelativePath(_root, path).Replace('\\', '/');

	private static bool Same(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);

	private static JsonNode? Text(byte[]? content) => content is null ? null : JsonValue.Create(Encoding.UTF8.GetString(content));

	private void Notify(IEnumerable<string> paths)
	{
		if (!_replaying)
			foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList())
				foreach (var (folder, changed) in _watches.ToList())
					if (path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
						changed(path);
		if (!_replaying)
			StateChanged?.Invoke();
	}

	// A line of the journal, written as the step is (§5)
	private void Journal(JsonObject line)
	{
		if (_replaying)
			return;
		var text = line.ToJsonString() + "\n";
		var path = Path.Combine(_root, JournalPath);
		try
		{
			if (_disk is DiskFiles)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.AppendAllText(path, text);
			}
			else
			{
				_journal.Append(text);
				_disk.WriteText(path, _journal.ToString());
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Console.Error.WriteLine($"[document] the journal couldn't be written: {ex.Message}");
		}
	}

	// The journal replayed over the disk: the edits as they were, still undoable; a file changed on disk since left out
	private void Recover()
	{
		var path = Path.Combine(_root, JournalPath);
		if (!_disk.Exists(path))
			return;
		var said = new List<string>();
		string text;
		try
		{
			text = _disk.ReadText(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Recovered = [$"The unsaved changes couldn't be read: {ex.Message}"];
			return;
		}
		var lines = new List<JsonObject>();
		try
		{
			foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				lines.Add(JsonNode.Parse(line) as JsonObject ?? throw new JsonException("a line isn't an object"));
		}
		catch (JsonException ex)
		{
			Recovered = [$"The unsaved changes couldn't be read ({ex.Message}): the project opens as it was saved."];
			_disk.WriteText(path + ".broken", text);
			_disk.Delete(path);
			return;
		}

		// Files changed on disk since their first step: the journal's steps on them don't apply
		var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var firsts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var line in lines.Where(l => (string?)l["op"] == "do"))
		{
			var file = Path.GetFullPath(Path.Combine(_root, (string)line["path"]!));
			if (!firsts.Add(file))
				continue;
			var before = line["before"]?.GetValue<string>();
			var disk = _disk.Exists(file) ? _disk.ReadText(file) : null;
			if (before != disk)
				stale.Add(file);
		}
		foreach (var file in stale)
			said.Add($"{Relative(file)} was changed outside the studio since its unsaved changes: they are left out.");

		_replaying = true;
		var byId = new Dictionary<int, Transaction>();
		foreach (var line in lines)
		{
			switch ((string?)line["op"])
			{
				case "do":
					var file = Path.GetFullPath(Path.Combine(_root, (string)line["path"]!));
					if (stale.Contains(file))
						break;
					var id = (int)line["tx"]!;
					var after = line["after"]?.GetValue<string>() is { } a ? Encoding.UTF8.GetBytes(a) : null;
					if (!byId.TryGetValue(id, out var transaction))
					{
						byId[id] = transaction = new Transaction(id, Relative(file), _clock()) { Sealed = true };
						_done.Add(transaction);
						_undone.Clear();
					}
					var step = transaction.Steps.Find(s => string.Equals(s.Path, file, StringComparison.OrdinalIgnoreCase));
					if (step is null)
						transaction.Steps.Add(new Step(file, line["before"]?.GetValue<string>() is { } b ? Encoding.UTF8.GetBytes(b) : null, after));
					else
						step.After = after;
					Put(file, after);
					_nextId = Math.Max(_nextId, id + 1);
					break;
				case "undo" when byId.TryGetValue((int)line["tx"]!, out var undone) && _done.Remove(undone):
					for (var i = undone.Steps.Count - 1; i >= 0; i--)
						Put(undone.Steps[i].Path, undone.Steps[i].Before);
					_undone.Add(undone);
					break;
				case "redo" when byId.TryGetValue((int)line["tx"]!, out var redone) && _undone.Remove(redone):
					foreach (var redoStep in redone.Steps)
						Put(redoStep.Path, redoStep.After);
					_done.Add(redone);
					break;
			}
		}
		_replaying = false;
		if (_done.Count > 0 || _undone.Count > 0)
			said.Insert(0, $"Recovered {_edited.Count} file{(_edited.Count == 1 ? "" : "s")} of unsaved changes: save to keep them.");
		Recovered = said;
		// The journal again as the document now has it, so it holds only what still applies
		_disk.Delete(path);
		foreach (var transaction in _done.Concat(_undone.AsEnumerable().Reverse()))
			foreach (var step in transaction.Steps)
				Journal(new JsonObject { ["op"] = "do", ["tx"] = transaction.Id, ["path"] = Relative(step.Path), ["before"] = Text(step.Before), ["after"] = Text(step.After) });
		// In the order they were undone: the first undone was the newest done then
		foreach (var transaction in _undone)
			Journal(new JsonObject { ["op"] = "undo", ["tx"] = transaction.Id });
	}

	private sealed class Unwatch(Action undo) : IDisposable
	{
		public void Dispose() => undo();
	}
}

// One file's change: its content before and after, null for none
internal sealed class Step(string path, byte[]? before, byte[]? after)
{
	public string Path { get; } = path;
	public byte[]? Before { get; } = before;
	public byte[]? After { get; set; } = after;
}

// Steps undone and redone together
internal sealed class Transaction(int id, string label, DateTime last)
{
	public int Id { get; } = id;
	public string Label { get; } = label;
	public DateTime Last { get; set; } = last;
	public bool Sealed { get; set; }
	public List<Step> Steps { get; } = [];
}
