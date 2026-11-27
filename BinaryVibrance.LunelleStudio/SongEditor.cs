using System.Text.Json.Nodes;

using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A song file being edited: its JSON as the edits leave it, built into a song with no compile (<see cref="SongFile.Load"/>),
/// written back a moment after the last edit, each edit undoable for the session and each part revertible to the file
/// as it was opened. A file changed by something else while open is read again, and the undo history with it cleared.
/// </summary>
public sealed class SongEditor
{
	/// <summary>How long after the last edit the file is written, so a drag writes it once.</summary>
	public const double SaveAfterSeconds = 0.4;

	private readonly Stack<string> _undo = new();
	private readonly Stack<string> _redo = new();
	private JsonObject _root;
	private string _opened;
	private string _written;
	private double _sinceEdit;
	private bool _dirty;

	private readonly IMusicFiles _files;

	private SongEditor(string path, string text, IMusicFiles files)
	{
		Path = path;
		_files = files;
		_root = Parse(text);
		_opened = Text;
		_written = text;
	}

	/// <summary>The file.</summary>
	public string Path { get; }

	/// <summary>The song as the edits leave it, as its file is written.</summary>
	public string Text => SongFile.Write(_root);

	/// <summary>Whether there is an edit to undo, or one undone to do again.</summary>
	public bool CanUndo => _undo.Count > 0;

	/// <inheritdoc cref="CanUndo"/>
	public bool CanRedo => _redo.Count > 0;

	/// <summary>Whether an edit waits to be written.</summary>
	public bool IsDirty => _dirty;

	/// <summary>Opens <paramref name="path"/>, in <paramref name="files"/> (the disk when none is given).</summary>
	/// <exception cref="IOException">It can't be read.</exception>
	/// <exception cref="FormatException">It isn't JSON.</exception>
	public static SongEditor Open(string path, IMusicFiles? files = null)
	{
		files ??= DiskFiles.Instance;
		return new(path, files.ReadText(path), files);
	}

	private static JsonObject Parse(string text)
	{
		try
		{
			return JsonNode.Parse(text, documentOptions: new() { AllowTrailingCommas = true, CommentHandling = System.Text.Json.JsonCommentHandling.Skip }) as JsonObject
				?? throw new FormatException("A song file is a JSON object.");
		}
		catch (System.Text.Json.JsonException ex)
		{
			throw new FormatException($"Not JSON: {ex.Message}", ex);
		}
	}

	/// <summary>
	/// The song, built from the file as edited, its names bound by <paramref name="bindings"/>; named
	/// <paramref name="name"/> (its id) where the file names it nothing, as a format 2 song file doesn't.
	/// </summary>
	public Song Build(SongBindings bindings, string? name = null)
	{
		if (name is null || System.Text.Json.Nodes.JsonNode.Parse(Text) is not System.Text.Json.Nodes.JsonObject root || root["name"] is not null)
			return SongFile.Load(Text, bindings);
		root["name"] = name;
		return SongFile.Load(root.ToJsonString(), bindings);
	}

	/// <summary>
	/// The part a placement plays, as the file has it (<c>sections[section].parts[index]</c>), or null when the file has
	/// none there (a part a hook added) or the part is code.
	/// </summary>
	public JsonObject? Part(int section, int index) => Part(_root, section, index);

	private static JsonObject? Part(JsonObject root, int section, int index) =>
		root["sections"] is JsonArray sections && (uint)section < (uint)sections.Count
		&& sections[section]?["parts"] is JsonArray parts && (uint)index < (uint)parts.Count
		&& parts[index] is JsonObject part && part["code"] is null && part["part"] is null
			? part
			: null;

	/// <summary>Whether the part differs from the file as it was opened.</summary>
	public bool IsEdited(int section, int index) =>
		Part(section, index)?.ToJsonString() != Part(Parse(_opened), section, index)?.ToJsonString();

	/// <summary>Makes an edit: <paramref name="change"/> changes the JSON; undoable, written a moment later.</summary>
	public void Edit(Action<JsonObject> change)
	{
		ArgumentNullException.ThrowIfNull(change);
		var before = Text;
		change(_root);
		if (Text == before)
			return;
		// A gesture's edits (a drag) undo as one: only its first keeps what came before
		if (!_inGesture || !_gestureKept)
			_undo.Push(before);
		_gestureKept = _inGesture;
		_redo.Clear();
		Touch();
	}

	/// <summary>Undoes the last edit.</summary>
	/// <returns>Whether there was one.</returns>
	public bool Undo() => Step(_undo, _redo);

	/// <summary>Does again the last edit undone.</summary>
	/// <returns>Whether there was one.</returns>
	public bool Redo() => Step(_redo, _undo);

	private bool Step(Stack<string> from, Stack<string> to)
	{
		if (from.Count == 0)
			return false;
		to.Push(Text);
		_root = Parse(from.Pop());
		Touch();
		return true;
	}

	/// <summary>Puts a part back as the file had it when opened; an edit itself, so it undoes.</summary>
	/// <returns>Whether there was a part to put back.</returns>
	public bool RevertPart(int section, int index)
	{
		if (Part(Parse(_opened), section, index) is not { } original || Part(section, index) is null)
			return false;
		Edit(root => root["sections"]![section]!["parts"]![index] = original.DeepClone());
		return true;
	}

	private void Touch()
	{
		_dirty = true;
		_sinceEdit = 0;
	}

	/// <summary>Once a frame: writes the file once the edits have paused.</summary>
	/// <returns>Whether it was written.</returns>
	public bool Update(double seconds)
	{
		if (!_dirty)
			return false;
		_sinceEdit += seconds;
		if (_sinceEdit < SaveAfterSeconds)
			return false;
		Save();
		return true;
	}

	/// <summary>Writes the file now, if an edit waits.</summary>
	public void Save()
	{
		if (!_dirty)
			return;
		var text = Text;
		_files.WriteText(Path, text);
		_written = text;
		_dirty = false;
	}

	/// <summary>
	/// Whether the file on disk is no longer what this wrote or read (something else changed it); if so it is read again,
	/// and the history cleared, since its edits were to another file.
	/// </summary>
	public bool ReloadIfChanged()
	{
		string disk;
		try
		{
			disk = _files.ReadText(Path);
		}
		catch (IOException)
		{
			return false;
		}
		if (disk == _written || _dirty)
			return false;
		_root = Parse(disk);
		_opened = Text;
		_written = disk;
		_undo.Clear();
		_redo.Clear();
		return true;
	}

	private bool _inGesture;
	private bool _gestureKept;

	/// <summary>Starts a gesture (a drag): its edits, until <see cref="EndGesture"/>, undo as one.</summary>
	public void BeginGesture()
	{
		_inGesture = true;
		_gestureKept = false;
	}

	/// <summary>Ends a gesture.</summary>
	public void EndGesture()
	{
		_inGesture = false;
		_gestureKept = false;
	}

	/// <summary>The part at <c>sections[section].parts[index]</c> as the file has it, code or not, or null.</summary>
	public JsonObject? RawPart(int section, int index) =>
		_root["sections"] is JsonArray sections && (uint)section < (uint)sections.Count
		&& sections[section]?["parts"] is JsonArray parts && (uint)index < (uint)parts.Count
			? parts[index] as JsonObject
			: null;
}
