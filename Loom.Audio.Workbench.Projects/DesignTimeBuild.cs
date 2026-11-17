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
}
