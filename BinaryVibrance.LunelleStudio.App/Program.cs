#if LUNELLESTUDIO_WEB
using BinaryVibrance.Kaldra.Core.Hosting;
using BinaryVibrance.LunelleStudio.App;
using BinaryVibrance.LunelleStudio.App.Web;

// The browser's studio: music files alone, kept in the browser's storage (BrowserFiles), the sample's music the first
// time; a project comes and goes as a .lunelle file, exported and imported from the transport. Kaldra's web host drives
// it from the page once the runtime is up.
await System.Runtime.InteropServices.JavaScript.JSHost.ImportAsync(StudioPage.Module, "../lunelle-studio.js");
// The music's output, an AudioWorklet, which the app opens as it loads (AudioOutput.Open)
await BinaryVibrance.Sirrow.Web.WebAudio.RegisterAsync();
var files = await BrowserFiles.OpenAsync();
BrowserProject.StartIfEmpty(files);
KaldraApplication.Run(new WorkbenchApp(BrowserProject.Folder, filesAlone: true, files: files), args);
#else
using BinaryVibrance.Sirrow.Sdl;
using BinaryVibrance.LunelleStudio;
using BinaryVibrance.LunelleStudio.App;
using BinaryVibrance.LunelleStudio.Projects;
using BinaryVibrance.Kaldra.Core.Hosting;
using BinaryVibrance.Kaldra.Hosting.Desktop;

// lunelle-studio <project.csproj | folder> [--extension <extension.csproj>] [the extension's own options]
//     the workbench over that music project, with its extension: the one its LunelleStudioExtension property names, or
//     the one given (--extension none for none)
// lunelle-studio <folder> --data, or a folder with no .csproj
//     the workbench over the folder's music files alone (MusicFolder), with no C#: nothing is compiled, and there is no
//     extension
// lunelle-studio new <folder> [--data]
//     makes a music project there to start from, and opens it; with --data, music files alone
// The host's own options still apply (--backend and the rest).
#if DEBUG
// Vysara's diagnostics endpoint, which RoseMCP's live UI tools reach the window's tree through
BinaryVibrance.Vysara.DiagnosticsEndpoint.Start();
#endif
var appArgs = HostOptions.Parse(args).AppArgs.ToList();
string? extensionProject = null;
var at = appArgs.IndexOf("--extension");
if (at >= 0 && at + 1 < appArgs.Count)
{
	extensionProject = appArgs[at + 1];
	appArgs.RemoveRange(at, 2);
}

var filesAlone = appArgs.Remove("--data");
string project;
try
{
	project = appArgs switch
	{
		["new", var folder, ..] when filesAlone => ProjectTemplate.CreateData(folder),
		["new", var folder, ..] => ProjectTemplate.Create(folder),
		[var path, ..] when filesAlone || WorkbenchFolder.IsFilesAlone(path) => FindFolder(path),
		[var path, ..] => FindProject(path),
		_ => throw new ArgumentException("Give it a music project's .csproj, or its folder; or \"new <folder>\" to make one."),
	};
	filesAlone = filesAlone || appArgs is not ["new", ..] && Directory.Exists(project);
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
{
	Console.Error.WriteLine($"lunelle-studio: {ex.Message}");
	return 1;
}
Console.WriteLine($"[workbench] {project}");

// The extension, built and loaded before the window opens: it gives the bus the music plays into. Music files alone have
// none, since an extension is C# to build
IWorkbenchExtension? extension = null;
if (filesAlone)
{
	if (extensionProject is not null and not "none")
		Console.Error.WriteLine("[workbench] music files alone have no extension: --extension is left out");
	extensionProject = "none";
}
try
{
	extensionProject = extensionProject switch
	{
		"none" => null,
		null => await WorkbenchExtensions.NamedByAsync(project),
		_ => Path.GetFullPath(extensionProject),
	};
	if (extensionProject is not null)
	{
		Console.WriteLine($"[workbench] building the extension {extensionProject}");
		extension = await WorkbenchExtensions.BuildAndLoadAsync(extensionProject, WorkbenchApp.Configuration);
		Console.WriteLine($"[workbench] extension {extension.GetType().FullName}");
	}
}
catch (InvalidOperationException ex)
{
	Console.Error.WriteLine($"[workbench] the extension couldn't be loaded, so the workbench opens without it: {ex.Message}");
}

SdlAudio.Register(); // the music's output, which the app opens as it loads (AudioOutput.Open)

// What the workbench didn't take (the project, "new" and its folder) is the extension's
KaldraApplication.Run(new WorkbenchApp(project, extension, [.. appArgs.Skip(appArgs is ["new", ..] ? 2 : 1)], extension is null ? null : extensionProject, filesAlone), args);
return 0;

// A folder of music files, or the folder a .csproj is in
static string FindFolder(string path)
{
	path = Path.GetFullPath(path);
	if (File.Exists(path) && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
		return Path.GetDirectoryName(path)!;
	return Directory.Exists(path) ? path : throw new ArgumentException($"{path} is not a folder.");
}

// A .csproj as given, or the one in a folder
static string FindProject(string path)
{
	path = Path.GetFullPath(path);
	if (File.Exists(path) && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
		return path;
	if (!Directory.Exists(path))
		throw new ArgumentException($"{path} is neither a .csproj nor a folder.");
	return Directory.GetFiles(path, "*.csproj") switch
	{
		[var only] => only,
		[] => throw new ArgumentException($"{path} has no .csproj."),
		_ => throw new ArgumentException($"{path} has more than one .csproj: name the one to open."),
	};
}
#endif
