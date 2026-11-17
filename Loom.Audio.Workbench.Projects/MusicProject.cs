using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Loom.Audio.Workbench.Projects;

/// <summary>
/// A music project, opened from its .csproj and compiled in-process with Roslyn: what a workbench reloads by in place of
/// <c>dotnet watch</c>. It asks a design-time build for the compiler's command line once (and again when the .csproj or
/// the set of files changes), keeps the compilation, and on every save updates the file in it, runs its source
/// generators, emits to memory and loads the result into a collectible context. Every compile is a full one, so there are
/// no rude edits. The newest build waits in <see cref="TakeBuild"/> for the host's frame.
/// </summary>
public sealed class MusicProject : IDisposable
{
	private static readonly EmitOptions Emit = new(debugInformationFormat: DebugInformationFormat.PortablePdb);

	private readonly FileSystemWatcher _watcher;
	private readonly SemaphoreSlim _compiling = new(1, 1);
	private readonly Lock _dirtyLock = new();
	private readonly HashSet<string> _dirty = new(StringComparer.OrdinalIgnoreCase);
	private Solution _solution = null!;
	private ProjectId _project = null!;
	private Dictionary<string, string> _references = [];
	private ProjectBuild? _pending;
	private int _builds;
	private bool _reloadArguments;
	private CancellationTokenSource? _debounce;

	private MusicProject(string projectPath)
	{
		ProjectPath = projectPath;
		Directory = Path.GetDirectoryName(projectPath)!;
		_watcher = new FileSystemWatcher(Directory)
		{
			IncludeSubdirectories = true,
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
		};
		_watcher.Changed += (_, e) => OnFileChanged(e.FullPath, added: false);
		_watcher.Created += (_, e) => OnFileChanged(e.FullPath, added: true);
		_watcher.Deleted += (_, e) => OnFileChanged(e.FullPath, added: true);
		_watcher.Renamed += (_, e) => OnFileChanged(e.FullPath, added: true);
	}

	/// <summary>The project's .csproj, in full.</summary>
	public string ProjectPath { get; }

	/// <summary>The folder it is in, which is watched.</summary>
	public string Directory { get; }

	/// <summary>Its name: the .csproj's.</summary>
	public string Name => Path.GetFileNameWithoutExtension(ProjectPath);

	/// <summary>Whether a compile is under way.</summary>
	public bool IsCompiling => _compiling.CurrentCount == 0;

	/// <summary>Opens <paramref name="projectPath"/> and compiles it once, the build waiting in <see cref="TakeBuild"/>.</summary>
	/// <exception cref="InvalidOperationException">Its design-time build failed (it may need building once first).</exception>
	public static async Task<MusicProject> OpenAsync(string projectPath, CancellationToken cancellation = default)
	{
		var project = new MusicProject(Path.GetFullPath(projectPath));
		await project.LoadArgumentsAsync(cancellation);
		project.Publish(await project.CompileAsync(cancellation));
		project._watcher.EnableRaisingEvents = true;
		return project;
	}

	/// <summary>
	/// The newest build not yet taken, or null: the host's frame takes it, hands its catalog to the workbench, and releases
	/// the build it replaces. A build overtaken before it was taken is released here.
	/// </summary>
	public ProjectBuild? TakeBuild() => Interlocked.Exchange(ref _pending, null);

	/// <summary>Compiles it now, as a save would, and returns the build (which also waits in <see cref="TakeBuild"/>).</summary>
	public async Task<ProjectBuild> RebuildAsync(CancellationToken cancellation = default)
	{
		await _compiling.WaitAsync(cancellation);
		try
		{
			string[] dirty;
			bool reload;
			lock (_dirtyLock)
			{
				dirty = [.. _dirty];
				_dirty.Clear();
				reload = _reloadArguments;
				_reloadArguments = false;
			}
			if (reload)
				await LoadArgumentsCoreAsync(cancellation);
			else
				foreach (var path in dirty)
					await RefreshAsync(path, cancellation);
			var build = await CompileCoreAsync(cancellation);
			Publish(build);
			return build;
		}
		finally
		{
			_compiling.Release();
		}
	}

	private async Task LoadArgumentsAsync(CancellationToken cancellation)
	{
		await _compiling.WaitAsync(cancellation);
		try
		{
			await LoadArgumentsCoreAsync(cancellation);
		}
		finally
		{
			_compiling.Release();
		}
	}

	// The compiler's command line, made a Roslyn project: its sources, references, analyzers and generators, the
	// editorconfigs they read and its options
	private async Task LoadArgumentsCoreAsync(CancellationToken cancellation)
	{
		var arguments = await DesignTimeBuild.CompilerArgumentsAsync(ProjectPath, cancellation);
		var parsed = CSharpCommandLineParser.Default.Parse(arguments, Directory, sdkDirectory: null);
		var id = ProjectId.CreateNewId(Name);
		DocumentInfo Document(string path) => DocumentInfo.Create(DocumentId.CreateNewId(id), Path.GetFileName(path),
			loader: new FileTextLoader(path, Encoding.UTF8), filePath: path);
		var references = parsed.MetadataReferences.Select(r => MetadataReference.CreateFromFile(r.Reference, r.Properties)).ToArray();
		_references = parsed.MetadataReferences
			.GroupBy(r => Path.GetFileNameWithoutExtension(r.Reference), StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First().Reference, StringComparer.OrdinalIgnoreCase);
		var info = ProjectInfo.Create(id, VersionStamp.Create(), Name, parsed.CompilationName ?? Name, LanguageNames.CSharp,
				filePath: ProjectPath,
				compilationOptions: parsed.CompilationOptions,
				parseOptions: parsed.ParseOptions,
				documents: parsed.SourceFiles.Select(f => Document(f.Path)),
				metadataReferences: references,
				analyzerReferences: parsed.AnalyzerReferences.Select(a => (AnalyzerReference)new AnalyzerFileReference(Path.GetFullPath(a.FilePath, Directory), AnalyzerLoader.Instance)),
				additionalDocuments: parsed.AdditionalFiles.Select(f => Document(f.Path)))
			.WithAnalyzerConfigDocuments(parsed.AnalyzerConfigPaths.Select(Document));
		using var workspace = new AdhocWorkspace();
		_solution = workspace.AddProject(info).Solution;
		_project = id;
	}

	// One file changed on disk: its text in the project read afresh; a file the project doesn't have means the set of
	// files changed, and the command line is asked for again
	private async Task RefreshAsync(string path, CancellationToken cancellation)
	{
		var document = _solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();
		if (document is null)
		{
			await LoadArgumentsCoreAsync(cancellation);
			return;
		}
		var text = await ReadAsync(path, cancellation);
		_solution = _solution.GetDocument(document) is not null
			? _solution.WithDocumentText(document, text)
			: _solution.GetAdditionalDocument(document) is not null
				? _solution.WithAdditionalDocumentText(document, text)
				: _solution.WithAnalyzerConfigDocumentText(document, text);
	}

	// An editor may still hold the file as it saves: a few tries before giving up
	private static async Task<SourceText> ReadAsync(string path, CancellationToken cancellation)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				return SourceText.From(await File.ReadAllTextAsync(path, Encoding.UTF8, cancellation), Encoding.UTF8);
			}
			catch (IOException) when (attempt < 5)
			{
				await Task.Delay(40, cancellation);
			}
		}
	}

	private async Task<ProjectBuild> CompileAsync(CancellationToken cancellation)
	{
		await _compiling.WaitAsync(cancellation);
		try
		{
			return await CompileCoreAsync(cancellation);
		}
		finally
		{
			_compiling.Release();
		}
	}

	private async Task<ProjectBuild> CompileCoreAsync(CancellationToken cancellation)
	{
		var watch = Stopwatch.StartNew();
		var compilation = await _solution.GetProject(_project)!.GetCompilationAsync(cancellation)
			?? throw new InvalidOperationException("The project has no compilation.");
		using var pe = new MemoryStream();
		using var pdb = new MemoryStream();
		var result = compilation.Emit(pe, pdb, options: Emit, cancellationToken: cancellation);
		if (!result.Success)
			return new ProjectBuild(null, [.. result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(Describe)], watch.Elapsed, null);
		if (SharedReferences.Mismatch(_references) is { } mismatch)
			return new ProjectBuild(null, [mismatch], watch.Elapsed, null);
		pe.Position = 0;
		pdb.Position = 0;
		var context = new ProjectLoadContext($"{Name} {++_builds}", _references);
		try
		{
			var assembly = context.LoadFromStream(pe, pdb);
			var (catalog, problem) = CatalogReader.Read(assembly);
			if (catalog is null)
			{
				context.Unload();
				return new ProjectBuild(null, [problem!], watch.Elapsed, null);
			}
			return new ProjectBuild(catalog, [], watch.Elapsed, context);
		}
		catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException or TypeLoadException or MissingMethodException)
		{
			context.Unload();
			return new ProjectBuild(null, [$"{Name} built, and didn't load: {ex.Message}"], watch.Elapsed, null);
		}
	}

	private string Describe(Diagnostic diagnostic)
	{
		var span = diagnostic.Location.GetLineSpan();
		var where = span.Path is { Length: > 0 }
			? $"{Path.GetRelativePath(Directory, span.Path)}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): "
			: "";
		return $"{where}error {diagnostic.Id}: {diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}";
	}

	private void Publish(ProjectBuild build) => Interlocked.Exchange(ref _pending, build)?.Release();

	// A save: the file noted, and a compile after the writes settle (an editor writes a file more than once)
	private void OnFileChanged(string path, bool added)
	{
		var relative = Path.GetRelativePath(Directory, path);
		if (relative.StartsWith("bin", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("obj", StringComparison.OrdinalIgnoreCase)
			|| relative.Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.')))
			return;
		var extension = Path.GetExtension(path);
		if (extension is not (".cs" or ".json" or ".csproj" or ".props" or ".targets" or ".editorconfig"))
			return;
		lock (_dirtyLock)
		{
			if (added || extension is ".csproj" or ".props" or ".targets")
				_reloadArguments = true;
			else
				_dirty.Add(path);
		}
		var debounce = new CancellationTokenSource();
		Interlocked.Exchange(ref _debounce, debounce)?.Cancel();
		_ = Task.Delay(150, debounce.Token).ContinueWith(_ => RebuildAsync(), debounce.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
	}

	/// <summary>Stops watching the project, and lets the build waiting go.</summary>
	public void Dispose()
	{
		_watcher.Dispose();
		_debounce?.Cancel();
		TakeBuild()?.Release();
	}
}
