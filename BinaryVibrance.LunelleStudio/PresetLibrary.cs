using System.Text.Json;

using BinaryVibrance.Lunelle.Chip;
using BinaryVibrance.Lunelle.Music;

namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// A sound a preset library offers: a patch or a kit by its name, the kind of instrument it is ("keys", "bass", for a
/// kit "sampled" or "synthesised") and what it says of itself.
/// </summary>
public sealed record Preset(string Name, bool IsKit, string Category, string About);

/// <summary>A file a preset brings into a project: its path from the project's root, its bytes, and the patch it is, if one.</summary>
public sealed record PresetFile(string Path, byte[] Bytes, string? Patch);

/// <summary>
/// Sounds a host ships for any project of files alone to take: a folder of music files (patches in
/// <c>Patches/&lt;category&gt;/</c>, kits in <c>Kits/</c>, sampled in a folder of their own or made of patches, and a
/// <c>lunelle-project.json</c> naming the macros the patches turn), read through a function given a path as
/// <see cref="MusicFolder"/> reads a project. Each preset is built as it is, to try on the keyboard, and gives the files a
/// project takes when it is added (<see cref="FilesFor"/>): a patch its file, a kit its own and, for a kit of patches, each
/// piece's patch.
/// </summary>
public sealed class PresetLibrary
{
	private readonly MusicFolder _folder;
	private readonly IReadOnlyList<string> _files;
	private readonly Func<string, byte[]> _read;
	private readonly Dictionary<string, IReadOnlyList<string>> _kitPatches = new(StringComparer.Ordinal);

	private PresetLibrary(MusicFolder folder, IReadOnlyList<string> files, Func<string, byte[]> read)
	{
		_folder = folder;
		_files = files;
		_read = read;
	}

	/// <summary>Its presets: the patches by category and name, then the kits by name.</summary>
	public IReadOnlyList<Preset> Presets { get; private set; } = [];

	/// <summary>Each file of the library that couldn't be read, and why, as <see cref="MusicFolder.Problems"/> says it.</summary>
	public IReadOnlyList<string> Problems => _folder.Problems;

	/// <summary>The presets among <paramref name="files"/> (paths from the library's root, <c>/</c> between folders), each read through <paramref name="read"/>.</summary>
	public static PresetLibrary Load(IEnumerable<string> files, Func<string, byte[]> read)
	{
		ArgumentNullException.ThrowIfNull(files);
		ArgumentNullException.ThrowIfNull(read);
		var all = files.Select(f => f.Replace('\\', '/')).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
		var library = new PresetLibrary(MusicFolder.Load(all, read), all, read);
		library.Index();
		return library;
	}

	/// <summary>The presets in the folder at <paramref name="directory"/>.</summary>
	/// <exception cref="DirectoryNotFoundException">There is no such folder.</exception>
	public static PresetLibrary FromDirectory(string directory)
	{
		ArgumentNullException.ThrowIfNull(directory);
		var root = Path.GetFullPath(directory);
		if (!Directory.Exists(root))
			throw new DirectoryNotFoundException($"There is no folder {root}.");
		var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'));
		return Load(files, path => File.ReadAllBytes(Path.Combine(root, path)));
	}

	/// <summary>The patch preset named <paramref name="name"/>, built from its file.</summary>
	/// <exception cref="KeyNotFoundException">There is no such patch.</exception>
	public Patch BuildPatch(string name) =>
		_folder.Patches.FirstOrDefault(p => p.Name == name).Build?.Invoke() ?? throw new KeyNotFoundException($"There is no patch preset {name}.");

	/// <summary>The kit preset named <paramref name="name"/>, loaded once (a sampled kit decodes its takes).</summary>
	/// <exception cref="KeyNotFoundException">There is no such kit.</exception>
	public DrumKit BuildKit(string name) =>
		_folder.Kits.FirstOrDefault(k => k.Name == name).Build?.Invoke() ?? throw new KeyNotFoundException($"There is no kit preset {name}.");

	/// <summary>
	/// The files <paramref name="preset"/> brings into a project, by their paths from its root: a patch as
	/// <c>Patches/&lt;name&gt;.patch.json</c>; a kit's manifest where the library has it (<c>Kits/synth.kit.json</c>), with
	/// the rest of its folder when it has one of its own (<c>Kits/general/</c>, its samples and licence), and each patch
	/// its pieces play.
	/// </summary>
	public IReadOnlyList<PresetFile> FilesFor(Preset preset)
	{
		ArgumentNullException.ThrowIfNull(preset);
		if (!preset.IsKit)
			return [PatchFile(preset.Name)];
		var manifest = _folder.KitPath(preset.Name) ?? throw new KeyNotFoundException($"There is no kit preset {preset.Name}.");
		var files = new List<PresetFile>();
		if (Path.GetFileName(manifest).Equals("kit.json", StringComparison.OrdinalIgnoreCase) && manifest.LastIndexOf('/') is > 0 and var cut)
		{
			var folder = manifest[..(cut + 1)];
			files.AddRange(_files.Where(f => f.StartsWith(folder, StringComparison.Ordinal)).Select(f => new PresetFile(f, _read(f), null)));
		}
		else
			files.Add(new PresetFile(manifest, _read(manifest), null));
		files.AddRange(_kitPatches[preset.Name].Distinct(StringComparer.Ordinal).Select(PatchFile));
		return files;
	}

	/// <summary>
	/// The macros <paramref name="preset"/>'s patches turn, by the names the library gives them (<c>"bright": 0</c>), so a
	/// project taking it can name them too; one the library gives no name is left out.
	/// </summary>
	public IReadOnlyDictionary<string, int> MacrosFor(Preset preset)
	{
		ArgumentNullException.ThrowIfNull(preset);
		var macros = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var patch in preset.IsKit ? _kitPatches[preset.Name] : [preset.Name])
		{
			if (_folder.PatchPath(patch) is not { } path)
				continue;
			using var document = Parse(System.Text.Encoding.UTF8.GetString(_read(path)));
			foreach (var macro in Items(document.RootElement, "macros"))
				if (macro.TryGetProperty("macro", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var number)
					&& _folder.MacroName(number) is { } name)
					macros[name] = number;
		}
		return macros;
	}

	private PresetFile PatchFile(string name)
	{
		var path = _folder.PatchPath(name) ?? throw new KeyNotFoundException($"There is no patch preset {name}.");
		return new PresetFile($"Patches/{name}.patch.json", _read(path), name);
	}

	// The presets listed: each kit with the patches its pieces play, which come with it and aren't offered alone
	private void Index()
	{
		var kits = new List<Preset>();
		foreach (var (name, _) in _folder.Kits)
		{
			var path = _folder.KitPath(name)!;
			var patches = new List<string>();
			string? about = null, source = null;
			var sampled = false;
			try
			{
				using var document = Parse(System.Text.Encoding.UTF8.GetString(_read(path)));
				about = Text(document.RootElement, "about");
				source = Text(document.RootElement, "source");
				foreach (var piece in Items(document.RootElement, "pieces"))
					if (Text(piece, "patch") is { } patch)
						patches.Add(patch);
					else
						sampled = true;
			}
			catch (Exception ex) when (ex is JsonException or FormatException)
			{
				continue;
			}
			// A kit whose pieces' patches aren't all here can't be added whole, so it isn't offered
			if (patches.Any(p => _folder.PatchPath(p) is null))
				continue;
			_kitPatches[name] = patches;
			kits.Add(new Preset(name, IsKit: true, sampled ? "sampled" : "synthesised", about ?? source ?? ""));
		}

		var piecePatches = _kitPatches.Values.SelectMany(p => p).ToHashSet(StringComparer.Ordinal);
		var patchPresets = new List<Preset>();
		foreach (var (name, _) in _folder.Patches)
		{
			if (piecePatches.Contains(name))
				continue;
			var path = _folder.PatchPath(name)!;
			var parts = path.Split('/');
			var category = parts.Length >= 3 ? parts[^2] : "patches";
			// The folder has read every patch it lists already, so this reads
			var about = Lunelle.Chip.PatchFile.Read(System.Text.Encoding.UTF8.GetString(_read(path))).About;
			patchPresets.Add(new Preset(name, IsKit: false, category, about));
		}
		Presets =
		[
			.. patchPresets.OrderBy(p => p.Category, StringComparer.Ordinal).ThenBy(p => p.Name, StringComparer.Ordinal),
			.. kits.OrderBy(k => k.Name, StringComparer.Ordinal),
		];
	}

	// JSON as the music files are read: comments and trailing commas let be
	private static JsonDocument Parse(string json) =>
		JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

	private static string? Text(JsonElement element, string name) =>
		element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

	private static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
		element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
}
