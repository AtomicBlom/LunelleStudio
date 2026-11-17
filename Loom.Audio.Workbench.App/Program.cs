using Loom.Audio.Workbench.App;
using Loom.Audio.Workbench.Projects;
using Loom.Core.Hosting;
using Loom.Hosting.Desktop;

// loom-workbench <project.csproj | folder>   the workbench over that music project
// loom-workbench new <folder>                 makes a music project there to start from, and opens it
// The host's own options still apply (--backend and the rest).
#if DEBUG
// SUIsei's diagnostics endpoint, which RoseMCP's live UI tools reach the window's tree through
SUIsei.DiagnosticsEndpoint.Start();
#endif
var appArgs = HostOptions.Parse(args).AppArgs.ToList();
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
	Console.Error.WriteLine($"loom-workbench: {ex.Message}");
	return 1;
}
Console.WriteLine($"[workbench] {project}");
LoomApplication.Run(new WorkbenchApp(project), args);
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
