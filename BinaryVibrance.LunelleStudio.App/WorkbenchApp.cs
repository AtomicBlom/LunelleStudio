using BinaryVibrance.Sirrow;
using BinaryVibrance.Kaldra.Core.Graph;
using BinaryVibrance.Kaldra.Core.Hosting;
using BinaryVibrance.Kaldra.Core.Materials;
using BinaryVibrance.Kaldra.UI.Vysara;

using BinaryVibrance.Vysara;

namespace BinaryVibrance.LunelleStudio.App;

/// <summary>
/// The workbench over a music project: the window opens at once on an empty workbench (or the extension's own build
/// of the music) while the project is opened and compiled beside it, then plays what it built and takes up each save.
/// A project's extension, loaded before the window opens, gives the bus the music plays into and controls of its own,
/// and is kept across every save. The song and mode it was at are kept beside the project; it always opens stopped.
/// <para>
/// Given a folder of music files alone (<paramref name="filesAlone"/>), nothing is compiled: the folder is read as
/// <see cref="Lunelle.Music.MusicFolder"/> and read again on each save (<see cref="WorkbenchFolder"/>), and it has no
/// extension. Its files are <paramref name="files"/>: the disk, or the browser's storage, where a project is always
/// music files alone (Web/WorkbenchApp.Web.cs). Compiling a project is the desktop's alone (WorkbenchApp.Desktop.cs).
/// </para>
/// </summary>
public sealed partial class WorkbenchApp(string projectPath, IWorkbenchExtension? extension = null, IReadOnlyList<string>? arguments = null, string? extensionProject = null, bool filesAlone = false, IMusicFiles? files = null) : KaldraApplication
{
	private readonly IMusicFiles _files = files ?? DiskFiles.Instance;
	private readonly string? _extensionProject = extensionProject;   // the extension's project, which the desktop watches
	private readonly WorkbenchStateFile _state = filesAlone
		? new WorkbenchStateFile(Path.Combine(projectPath, ".lunelle-studio", "workbench.json"), files)
		: WorkbenchStateFile.Beside(projectPath, files);
	private WorkbenchFolder? _folder;
	private UIHost _ui = null!;
	private UIPainter _painter = null!;
	private UIView _view = null!;
	private Glow _glow = null!;
	private Workbench _workbench = null!;
	private WorkbenchState? _restore;
	private StudioSettings _settings = null!;
	private double _sinceSave;

	// The music's audio, through the output Program registers (SDL 3, or the browser's AudioWorklet): updated once a
	// frame, paused while the window is off screen, closed as the window closes. It keeps the default unfocused volume
	// (1): the music plays on while another window, the editor, has the focus
	private AudioEngine Audio { get; set; } = null!;

	private string Name => filesAlone ? Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectPath))) : Path.GetFileNameWithoutExtension(projectPath);

	// Opens a project with code, compiling it beside the frame; takes it up once it has; closes it (the desktop's alone)
	partial void OpenProject();
	partial void UpdateProject();
	partial void CloseProject();

	// Watches the extension's project for a change, which says to restart (the desktop's alone)
	partial void WatchExtension();

	// Whether a project with code has built its music
	private partial bool ProjectHasMusic();

	// The host's own controls at the transport's end (the browser's export and import)
	partial void AddControls(WorkbenchView view);

	protected override void Configure(AppOptions options)
	{
		options.Title = $"LunelleStudio - {Name}";
		options.Width = 1280;
		options.Height = 800;
	}

	protected override void LoadContent()
	{
		var audio = new AudioOptions();
		Audio = new AudioEngine(AudioOutput.Open(audio), audio);

		Engine.DefineTechnique("background", new TechniqueDescriptor("background"));
		_glow = new Glow(Engine);
		_ui = new UIHost();
		_painter = UIPainter.Create(Engine, _ui);
		_view = _ui.CreateRoot(UILayer.Default);
		// On whole pixels, so a moving playhead always covers whole pixels and the glow round a lit one holds steady (Glow)
		_view.Root.UseLayoutRounding = true;

		// The extension's bus (a game's mix) and its own build of the music, to play until the project has compiled
		var catalog = extension?.Catalog;
		_workbench = new Workbench(Audio, extension?.Output(Audio) ?? Audio.CreateBus("music"), catalog ?? new WorkbenchCatalog())
		{
			CopyText = SetClipboardText,
			Files = _files,
			// Offered to a project of files alone (the browser's always is)
			Presets = StudioPresets.Load(),
		};
		// The theme chosen last, in whatever project, before the first view is made; and each new choice kept
		var settings = _settings = StudioSettings.Load(_files);
		_workbench.RestorePreferences(settings.Workbench);
		if (settings.Theme is { } theme && !_workbench.SetTheme(theme))
			Console.Error.WriteLine($"[studio] no theme is called '{theme}': showing {Workbench.ThemeName}");
		var fonts = new StudioFonts(_ui.Fonts);
		fonts.Apply();
		_workbench.ThemeChanged += name =>
		{
			fonts.Apply();
			settings.Theme = name;
			settings.Save(_files);
		};
		_workbench.ReportStatus($"opening {Name}…");
		_restore = _state.Load();
		if (catalog is not null && _restore is not null)
		{
			_workbench.RestoreState(_restore);
			_restore = null;
		}
		if (filesAlone)
			_folder = WorkbenchFolder.Open(projectPath, _workbench);
		else
			OpenProject();
		var view = new WorkbenchView(_workbench.ViewModel);
		extension?.Attach(new WorkbenchExtensionContext(_workbench, Audio, view.TransportExtras, arguments ?? []));
		AddControls(view);
		_view.Root.Content = view;
		WatchExtension();
		PushInputLayer(new VysaraInputLayer(_ui));

		SetGraph(RenderGraph.Define(g =>
		{
			// The workbench in linear light, then its glow and the display pass onto the screen (Glow)
			var screen = g.Backbuffer;
			var workbench = Glow.Target(g);
			g.Pass("background")
				.Writes(workbench)
				.Draw(static ctx => ctx.FullScreen("background"));
			g.Pass("ui")
				.Blends(workbench)
				.Draw((_painter, _view.Surface), static (ctx, ui) => ui.Item1.Draw(ctx, ui.Item2));
			_glow.Declare(g, workbench, screen);
			g.Present(screen);
		}));
	}

	protected override void Update(FrameTime time)
	{
		UpdateProject();
		_folder?.Update();
		// The song and mode it was at come back once there is music to come back to
		if (_restore is not null && (ProjectHasMusic() || _folder?.Current is not null))
		{
			_workbench.RestoreState(_restore);
			_restore = null;
		}
		_workbench.HandleInput(Input);
		_workbench.Update(time.DeltaSeconds);
		extension?.Update(time.DeltaSeconds);
		_sinceSave += time.DeltaSeconds;
		if (_sinceSave >= 1.0 && _restore is null)
		{
			_sinceSave = 0;
			_state.Save(_workbench.CaptureState());
			SaveSettings();
		}
	}

	// Keeps the workbench's layout for every project, when it has changed
	private void SaveSettings()
	{
		_settings.Workbench = _workbench.CapturePreferences();
		_settings.Save(_files);
	}

	protected override void BeforeRender(FrameTime time)
	{
		_view.Root.LayoutScale = WorkbenchView.FitScale(SurfaceSize, DisplayScale);
		_ui.Tick(time.DeltaSeconds, (float)time.TotalSeconds, SurfaceSize);
		WorkbenchFocus.KeepKeys(_ui.Focus);
		_ui.Render();
		_painter.Prepare();
		_glow.Prepare(SurfaceSize, Workbench.ThemeGlow);
		Audio.Update();
	}

	protected override void OnFocusChanged(bool focused) => Audio.SetFocused(focused);

	protected override void OnSuspending() => Audio.Pause();

	protected override void OnResumed() => Audio.Resume();

	protected override void UnloadContent()
	{
		try
		{
			if (_restore is null)
				_state.Save(_workbench.CaptureState());
			SaveSettings();
			CloseProject();
			_folder?.Dispose();
			_workbench.Dispose();
			extension?.Dispose();
		}
		finally
		{
			Audio.Dispose();
		}
	}

	/// <summary>The configuration this was built in, which the project and its extension are built in too, so their builds share its Kaldra.</summary>
	public static string Configuration =>
#if DEBUG
		"Debug";
#else
		"Release";
#endif
}
