using BinaryVibrance.Sirrow.Sdl;
using BinaryVibrance.LunelleStudio;
using BinaryVibrance.LunelleStudio.App;
using BinaryVibrance.LunelleStudio.Projects;
using BinaryVibrance.Kaldra.Core.Hosting;
using BinaryVibrance.Kaldra.Hosting.Desktop;

// lunelle-studio <project.csproj | folder> [--extension <extension.csproj>] [the extension's own options]
//     the workbench over that music project, with its extension: the one its LunelleStudioExtension property names, or
//     the one given (--extension none for none)
// lunelle-studio new <folder>
//     makes a music project there to start from, and opens it
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

string project;
try
{
	project = appArgs switch
	{
		["new", var folder, ..] => ProjectTemplate.Create(folder),
		[var path, ..] => FindProject(path),
		_ => throw new ArgumentException("Give it a music project's .csproj, or its folder; or \"new <folder>\" to make one."),
	};
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
{
	Console.Error.WriteLine($"lunelle-studio: {ex.Message}");
	return 1;
}
Console.WriteLine($"[workbench] {project}");

// The extension, built and loaded before the window opens: it gives the bus the music plays into
IWorkbenchExtension? extension = null;
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
KaldraApplication.Run(new WorkbenchApp(project, extension, [.. appArgs.Skip(appArgs is ["new", ..] ? 2 : 1)], extension is null ? null : extensionProject), args);
return 0;

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
