using BinaryVibrance.Sirrow;
using BinaryVibrance.Kaldra.Core.Graph;
using BinaryVibrance.Kaldra.Core.Hosting;
using BinaryVibrance.Kaldra.Core.Materials;
using BinaryVibrance.Kaldra.UI.Vysara;

using BinaryVibrance.Vysara;
using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;

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
	private ProjectDocument? _document;

	// The project's files as the studio edits them (docs/spec/transactions.md), its journal recovered
	private ProjectDocument Document => _document ??= ProjectDocument.Open(_files,
		filesAlone ? projectPath : Path.GetDirectoryName(Path.GetFullPath(projectPath))!);
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
		// Vulkan where it is; on Windows, DirectX 12 where it isn't (the host takes the first backend available then).
		// --backend still chooses
		if (!OperatingSystem.IsBrowser())
			options.EngineSettings.PreferredBackend ??= "Vulkan";
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
			// The project's files as a document: edits undo, Ctrl+S saves them, and a crash loses nothing
			Files = Document,
			// Offered to a project of files alone (the browser's always is)
			Presets = StudioPresets.Load(),
		};
		// The theme chosen last, in whatever project, before the first view is made; and each new choice kept
		var settings = _settings = StudioSettings.Load(_files);
		_workbench.RestorePreferences(settings.Workbench);
		// The browser has its one project; the desktop opens this one again when started with none named
		if (!OperatingSystem.IsBrowser())
			settings.LastProject = Path.GetFullPath(projectPath);
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
		AddSave(view);
		AddExports(view);
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

	// Save, first at the transport's end in every host: "save •" while there is something to save (Ctrl+S saves too)
	private void AddSave(WorkbenchView view)
	{
		var label = new TextBlock { Text = "save", FontSize = 13f, Margin = new Thickness(8, 3, 8, 4) };
		var button = new Button { Content = label, Margin = new Thickness(0, 0, 4, 4) };
		ToolTipService.SetToolTip(button, "Write the edits to the project's files (Ctrl+S); until then they undo, and a crash keeps them");
		button.Click += (_, _) => _workbench.Save();
		void Show() => label.Text = _workbench.ViewModel.IsDirty ? "save •" : "save";
		_workbench.ViewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(WorkbenchViewModel.IsDirty))
				Show();
		};
		Show();
		view.TransportExtras.Children.Add(button);
	}

	// The host's way to keep rendered files: a folder the user chooses (the desktop), or downloads (the browser); what it said
	private partial Task<string> DeliverAsync(IReadOnlyList<(string Name, byte[] Bytes)> files);

	// Exports, at the transport's end: the open song's, each rendered to a WAV (or all of them), and a new one recorded from
	// the cues moved as the song plays (docs/spec/ui-exports.md)
	private void AddExports(WorkbenchView view)
	{
		var button = new Button { Content = new TextBlock { Text = "exports", FontSize = 13f, Margin = new Thickness(8, 3, 8, 4) }, Margin = new Thickness(0, 0, 4, 4) };
		ToolTipService.SetToolTip(button, "Render the song's exports to WAV, the same every time, or record a new one");
		button.Click += (_, _) => ShowExports(button);
		view.TransportExtras.Children.Add(button);
	}

	private void ShowExports(Button anchor)
	{
		var workbench = _workbench;
		var column = new StackPanel { Orientation = Orientation.Vertical };
		var said = new TextBlock { FontSize = 11f, TextWrapping = TextWrapping.Wrap, Width = 260f };
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.Bottom };
		column.Children.Add(new TextBlock { Text = "This song's exports", FontSize = 12f, Margin = new Thickness(0f, 0f, 0f, 6f) });
		var song = workbench.ViewModel.SongName;
		async Task Render(IReadOnlyList<string> exports)
		{
			var files = new List<(string, byte[])>();
			foreach (var export in exports)
			{
				using var wav = new MemoryStream();
				if (workbench.RenderExport(export, wav) is { } why)
				{
					said.Text = $"{export}: {why}";
					return;
				}
				files.Add((SongExport.FileName(song, export), wav.ToArray()));
			}
			said.Text = await DeliverAsync(files);
		}
		var exports = workbench.SongExports;
		if (exports.Count == 0)
			column.Children.Add(new TextBlock { Text = "None yet: record one, or add \"exports\" to the song's file.", FontSize = 11f, TextWrapping = TextWrapping.Wrap, Width = 260f });
		foreach (var (exportName, title) in exports)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 2f, 0f, 0f) };
			row.Children.Add(new TextBlock { Text = title, FontSize = 12f, Width = 190f, VerticalAlignment = VerticalAlignment.Center });
			var live = workbench.LiveExport == exportName;
			row.Children.Add(Button(live ? "stop" : "play", live ? "Stop playing it live" : $"Play {exportName} live: its events applied as the song plays, each on its beat", () =>
			{
				if (live)
				{
					workbench.StopLiveExport();
					workbench.Stop();
				}
				else if (workbench.PlayExport(exportName) is { } why)
				{
					said.Text = why;
					return;
				}
				flyout.Hide();
			}));
			row.Children.Add(Button("render", $"Render {exportName} to {SongExport.FileName(song, exportName)}", () => _ = Render([exportName])));
			column.Children.Add(row);
		}
		if (exports.Count > 1)
			column.Children.Add(Button("render all", "Render every export of this song", () => _ = Render([.. exports.Select(e => e.Name)])));
		if (!workbench.CanRenderExports)
			said.Text = "This project's songs can't be rendered here yet: their code would play as rests.";
		var name = new TextBox { Width = 160f, Text = "take" };
		ToolTipService.SetToolTip(name, "The new export's name: lowercase letters, digits and hyphens");
		var record = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 10f, 0f, 0f) };
		record.Children.Add(name);
		record.Children.Add(Button(workbench.IsRecording ? "stop" : "record",
			"Record: the song plays from its start, and every cue you move, and every track macro you hold in the inspector, becomes an event on the nearest beat; stop writes the export",
			() =>
			{
				said.Text = (workbench.IsRecording ? workbench.StopRecording(name.Text) : workbench.StartRecording()) ?? (workbench.IsRecording ? "Recording: move the cues, or a track's macros." : "Recorded.");
				flyout.Hide();
			}));
		column.Children.Add(record);
		column.Children.Add(said);
		flyout.ShowAt(anchor);
	}

	// A button for the transport's end, where each host puts its own controls (AddControls)
	private static Button Button(string text, string tip, Action click)
	{
		var button = new Button
		{
			Content = new TextBlock { Text = text, FontSize = 13f, Margin = new Thickness(8, 3, 8, 4) },
			Margin = new Thickness(0, 0, 4, 4),
		};
		ToolTipService.SetToolTip(button, tip);
		button.Click += (_, _) => click();
		return button;
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
