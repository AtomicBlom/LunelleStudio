using Loom.Audio;
using Loom.Audio.Workbench.Projects;
using Loom.Core.Graph;
using Loom.Core.Hosting;
using Loom.Core.Materials;
using Loom.UI.SUIsei;

using SUIsei;

namespace Loom.Audio.Workbench.App;

/// <summary>
/// The workbench over a music project: the window opens at once on an empty workbench while the project is opened and
/// compiled beside it, then plays what it built and takes up each save. What it was doing is kept beside the project.
/// </summary>
public sealed class WorkbenchApp(string projectPath) : LoomApplication
{
	private readonly WorkbenchStateFile _state = WorkbenchStateFile.Beside(projectPath);
	private UIHost _ui = null!;
	private UIPainter _painter = null!;
	private UIView _view = null!;
	private Workbench _workbench = null!;
	private Task<MusicProject> _opening = null!;
	private WorkbenchProject? _project;
	private bool _opened;
	private WorkbenchState? _restore;
	private double _sinceSave;

	private string Name => Path.GetFileNameWithoutExtension(projectPath);

	protected override void Configure(AppOptions options)
	{
		options.Title = $"Loom workbench - {Name}";
		options.Width = 1280;
		options.Height = 800;
		// The default unfocused volume (1): the music plays on while the editor has the focus
		options.Audio = new AudioOptions();
	}

	protected override void LoadContent()
	{
		Engine.DefineTechnique("background", new TechniqueDescriptor("background"));
		_ui = new UIHost();
		_painter = UIPainter.Create(Engine, _ui);
		_view = _ui.CreateRoot(UILayer.Default);

		_workbench = new Workbench(Audio, Audio.CreateBus("music"), new WorkbenchCatalog()) { CopyText = SetClipboardText };
		_workbench.ReportStatus($"opening {Name}…");
		_restore = _state.Load();
		_opening = MusicProject.OpenAsync(projectPath);
		_view.Root.Content = new WorkbenchView(_workbench.ViewModel);
		PushInputLayer(new SUIseiInputLayer(_ui));

		SetGraph(RenderGraph.Define(g =>
		{
			var screen = g.Backbuffer;
			g.Pass("background")
				.Writes(screen)
				.Draw(static ctx => ctx.FullScreen("background"));
			g.Pass("ui")
				.Blends(screen)
				.Draw((_painter, _view.Surface), static (ctx, ui) => ui.Item1.Draw(ctx, ui.Item2));
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
			_project = new WorkbenchProject(_opening.Result, _workbench);
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
		_ui.Render();
		_painter.Prepare();
	}

	protected override void UnloadContent()
	{
		if (_restore is null)
			_state.Save(_workbench.CaptureState());
		_project?.Dispose();
		_workbench.Dispose();
	}
}
