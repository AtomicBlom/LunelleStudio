using BinaryVibrance.Vysara;

namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// The themes' typefaces, baked at build time into atlases under <c>fonts\</c> beside the app (the project's
/// <c>Assets/Fonts</c>), each loaded the first time a theme names it. <see cref="Apply"/> gives each family the views
/// name the font the theme gives it, in the UI host's fonts, which lays the text in it out again; a family whose font the
/// theme leaves out, or that wasn't baked, draws in Vysara's own.
/// </summary>
internal sealed class StudioFonts(FontCollection fonts)
{
	private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "fonts");

	private readonly Dictionary<string, FontAtlas?> _loaded = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Give each family the theme's fonts for its weights (<see cref="Workbench.ThemeFonts"/>), and nothing else.</summary>
	public void Apply()
	{
		var themes = Workbench.ThemeFonts;
		foreach (var (family, _, _) in themes)
			fonts.Remove(family);
		foreach (var (family, weight, font) in themes)
		{
			if (font is not null && Load(font) is { } atlas)
				fonts.Set(family, weight, atlas);
		}
	}

	// The atlas baked under `name`, loaded once; null, said once, when it wasn't baked or can't be read
	private FontAtlas? Load(string name)
	{
		if (_loaded.TryGetValue(name, out var atlas))
			return atlas;
		try
		{
			using var json = File.OpenRead(Path.Combine(Folder, $"{name}.font.json"));
			using var png = File.OpenRead(Path.Combine(Folder, $"{name}.0.png"));
			atlas = FontAtlas.Load(json, png);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			Console.Error.WriteLine($"[studio] the font '{name}' couldn't be loaded from {Folder}, so its text draws in Vysara's own: {ex.Message}");
			atlas = null;
		}
		_loaded[name] = atlas;
		return atlas;
	}
}
