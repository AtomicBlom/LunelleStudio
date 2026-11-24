using BinaryVibrance.Sirrow;
using BinaryVibrance.LunelleStudio.Projects;
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
/// and is kept across every save. What it was doing is kept beside the project.
/// </summary>
public sealed class WorkbenchApp(string projectPath, IWorkbenchExtension? extension = null, IReadOnlyList<string>? arguments = null, string? extensionProject = null) : KaldraApplication
{
	private readonly WorkbenchStateFile _state = WorkbenchStateFile.Beside(projectPath);
	private UIHost _ui = null!;
	private UIPainter _painter = null!;
	private UIView _view = null!;
	private Glow _glow = null!;
	private Workbench _workbench = null!;
	private Task<MusicProject> _opening = null!;
	private WorkbenchProject? _project;
	private bool _opened;
	private WorkbenchState? _restore;
	private double _sinceSave;

	// The music's audio, through SDL 3 (Program registers it): updated once a frame, paused while the window is off
	// screen, closed as the window closes. It keeps the default unfocused volume (1): the music plays on while another
	// window, the editor, has the focus
	private AudioEngine Audio { get; set; } = null!;

	private string Name => Path.GetFileNameWithoutExtension(projectPath);

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
		};
		// The theme chosen last, in whatever project, before the first view is made; and each new choice kept
		var settings = StudioSettings.Load();
		if (settings.Theme is { } theme && !_workbench.SetTheme(theme))
			Console.Error.WriteLine($"[studio] no theme is called '{theme}': showing {Workbench.ThemeName}");
		var fonts = new StudioFonts(_ui.Fonts);
		fonts.Apply();
		_workbench.ThemeChanged += name =>
		{
			fonts.Apply();
			settings.Theme = name;
			settings.Save();
		};
		_workbench.ReportStatus($"opening {Name}…");
		_restore = _state.Load();
		if (catalog is not null && _restore is not null)
		{
			_workbench.RestoreState(_restore);
			_restore = null;
		}
		_opening = MusicProject.OpenAsync(projectPath, new Dictionary<string, string> { ["Configuration"] = Configuration });
		var view = new WorkbenchView(_workbench.ViewModel);
		extension?.Attach(new WorkbenchExtensionContext(_workbench, Audio, view.TransportExtras, arguments ?? []));
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
		TakeProject();
		_project?.Update();
		// What it was doing comes back once there is music to come back to
		if (_restore is not null && _project?.Current is not null)
		{
			_workbench.RestoreState(_restore);
			_restore = null;
		}
		_workbench.HandleInput(Input);
		_workbench.Update(time.DeltaSeconds);
		extension?.Update(time.DeltaSeconds);
		if (_extensionChanged && _workbench.ViewModel.Notice.Length == 0)
			_workbench.ViewModel.Notice = $"{Path.GetFileNameWithoutExtension(extensionProject)} changed: restart to take it up";
		_sinceSave += time.DeltaSeconds;
		if (_sinceSave >= 1.0 && _restore is null)
		{
			_sinceSave = 0;
			_state.Save(_workbench.CaptureState());
		}
	}

	// The project, once it has opened: or why it didn't, on the build pill
	private void TakeProject()
	{
		if (_opened || !_opening.IsCompleted)
			return;
		_opened = true;
		if (_opening.IsCompletedSuccessfully)
		{
			_project = new WorkbenchProject(_opening.Result, _workbench) { Extension = extension };
			return;
		}
		var problem = _opening.Exception?.GetBaseException().Message ?? "it was cancelled";
		_workbench.ReportBuild([$"{Name} couldn't be opened: {problem}"], TimeSpan.Zero);
		Console.Error.WriteLine($"[workbench] {projectPath} couldn't be opened: {problem}");
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
			_extensionWatcher?.Dispose();
			_project?.Dispose();
			_workbench.Dispose();
			extension?.Dispose();
		}
		finally
		{
			Audio.Dispose();
		}
	}

	private FileSystemWatcher? _extensionWatcher;
	private volatile bool _extensionChanged;

	// The extension isn't reloaded: a change to its project is said beside the build pill, so its author knows to restart
	private void WatchExtension()
	{
		if (extension is null || extensionProject is null || Path.GetDirectoryName(extensionProject) is not { } folder)
			return;
		_extensionWatcher = new FileSystemWatcher(folder) { IncludeSubdirectories = true, EnableRaisingEvents = true };
		void Changed(string path)
		{
			var relative = Path.GetRelativePath(folder, path);
			if (!relative.StartsWith("bin", StringComparison.OrdinalIgnoreCase) && !relative.StartsWith("obj", StringComparison.OrdinalIgnoreCase)
				&& Path.GetExtension(path) is ".cs" or ".csproj" or ".xaml" or ".props" or ".targets")
				_extensionChanged = true;
		}
		_extensionWatcher.Changed += (_, e) => Changed(e.FullPath);
		_extensionWatcher.Created += (_, e) => Changed(e.FullPath);
		_extensionWatcher.Deleted += (_, e) => Changed(e.FullPath);
		_extensionWatcher.Renamed += (_, e) => Changed(e.FullPath);
	}

	/// <summary>The configuration this was built in, which the project and its extension are built in too, so their builds share its Kaldra.</summary>
	public static string Configuration =>
#if DEBUG
		"Debug";
#else
		"Release";
#endif
}
