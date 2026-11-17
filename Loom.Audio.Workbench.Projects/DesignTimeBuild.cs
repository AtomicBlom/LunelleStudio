using System.Diagnostics;
using System.Text.Json;

namespace Loom.Audio.Workbench.Projects;

/// <summary>
/// A project's compiler command line, from a design-time build run out of process: its PrepareResources and Compile
/// targets (its resources and references resolved, then CoreCompile) with the compiler's execution skipped and its
/// arguments handed back, as an IDE's language service gets them. Running MSBuild in another process keeps the SDK's
/// MSBuild (and its runtime) out of this one.
/// </summary>
internal static class DesignTimeBuild
{
	/// <summary>The arguments the compiler would be given for <paramref name="projectPath"/>.</summary>
	/// <exception cref="InvalidOperationException">The design-time build failed; the message has its output.</exception>
	public static async Task<string[]> CompilerArgumentsAsync(string projectPath, IReadOnlyDictionary<string, string> properties, CancellationToken cancellation)
	{
		var start = new ProcessStartInfo("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			WorkingDirectory = Path.GetDirectoryName(projectPath)!,
		};
		foreach (var argument in (string[])
		[
			"msbuild", projectPath, "-nologo", "-t:PrepareResources;Compile",
			"-p:SkipCompilerExecution=true", "-p:ProvideCommandLineArgs=true", "-p:DesignTimeBuild=true",
			"-p:BuildProjectReferences=false", "-getItem:CscCommandLineArgs",
			// CoreCompile lists this among its outputs so a design-time build runs it when everything is up to date (as
			// Roslyn's own MSBuild workspace sets it)
			@"-p:NonExistentFile=__NonExistentSubDir__\__NonExistentFile__",
		])
			start.ArgumentList.Add(argument);
		foreach (var (name, value) in properties)
			start.ArgumentList.Add($"-p:{name}={value}");
		// A project never restored (one just made) is restored first, in the same run
		if (!File.Exists(Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "project.assets.json")))
			start.ArgumentList.Add("-restore");
		using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet couldn't be started.");
		var output = process.StandardOutput.ReadToEndAsync(cancellation);
		var errors = process.StandardError.ReadToEndAsync(cancellation);
		await process.WaitForExitAsync(cancellation);
		var text = await output;
		if (process.ExitCode != 0)
			throw new InvalidOperationException($"The design-time build of {Path.GetFileName(projectPath)} failed:\n{text}{await errors}");
		using var json = JsonDocument.Parse(text[text.IndexOf('{')..]);
		return [.. json.RootElement.GetProperty("Items").GetProperty("CscCommandLineArgs").EnumerateArray()
			.Select(item => item.GetProperty("Identity").GetString()!)];
	}

	/// <summary>An MSBuild property of <paramref name="projectPath"/> as it evaluates, or null when it is empty.</summary>
	/// <exception cref="InvalidOperationException">The project couldn't be evaluated.</exception>
	public static async Task<string?> PropertyAsync(string projectPath, string name, CancellationToken cancellation = default)
	{
		var text = await RunAsync(["msbuild", projectPath, "-nologo", $"-getProperty:{name}"], Path.GetDirectoryName(projectPath)!, cancellation);
		var value = text.Trim();
		return value.Length == 0 ? null : value;
	}

	/// <summary>
	/// Builds <paramref name="projectPath"/> and returns its assembly's path (its TargetPath). Its references are taken as
	/// they were last built rather than built again, since the host's own build made those it shares (Loom), and a
	/// shared project built again, even from the same sources, is another build the host refuses: its sources' paths are
	/// in it, and a build started from "c:\..." rather than "C:\..." compiles it again. Only a project whose references
	/// were never built is built with them.
	/// </summary>
	/// <exception cref="InvalidOperationException">It didn't build; the message has the build's output.</exception>
	public static async Task<string> BuildAsync(string projectPath, string configuration, CancellationToken cancellation = default)
	{
		string[] build = ["build", projectPath, "-nologo", "-v:q", "-c", configuration, "-t:Build", "-getProperty:TargetPath"];
		string text;
		try
		{
			text = await RunAsync([.. build, "-p:BuildProjectReferences=false"], Path.GetDirectoryName(projectPath)!, cancellation);
		}
		catch (InvalidOperationException)
		{
			text = await RunAsync(build, Path.GetDirectoryName(projectPath)!, cancellation);
		}
		var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return lines.Length > 0 && File.Exists(lines[^1]) ? lines[^1] : throw new InvalidOperationException($"{Path.GetFileName(projectPath)} built nothing to load:\n{text}");
	}

	// dotnet, with its output when it succeeds and an exception holding it when it doesn't
	private static async Task<string> RunAsync(string[] arguments, string directory, CancellationToken cancellation)
	{
		var start = new ProcessStartInfo("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			WorkingDirectory = directory,
		};
		foreach (var argument in arguments)
			start.ArgumentList.Add(argument);
		using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet couldn't be started.");
		var output = process.StandardOutput.ReadToEndAsync(cancellation);
		var errors = process.StandardError.ReadToEndAsync(cancellation);
		await process.WaitForExitAsync(cancellation);
		var text = await output;
		if (process.ExitCode != 0)
			throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed:\n{text}{await errors}");
		return text;
	}
}
