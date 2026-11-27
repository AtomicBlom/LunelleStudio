namespace BinaryVibrance.LunelleStudio.Projects;

/// <summary>
/// Whether the .NET SDK a project with code needs is here: <c>dotnet</c> on the PATH (or under <c>DOTNET_ROOT</c>) with at
/// least one SDK beside it. Opening a <c>.csproj</c> runs <c>dotnet msbuild</c> (<see cref="DesignTimeBuild"/>), so
/// without one the studio can only open the project's music files alone.
/// </summary>
public static class DotnetSdk
{
	/// <summary>The SDK's <c>dotnet</c>, or null when there is none.</summary>
	public static string? Find() => Find(Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable("DOTNET_ROOT"));

	/// <summary>The SDK's <c>dotnet</c> on <paramref name="path"/> or under <paramref name="root"/>, or null when there is none.</summary>
	public static string? Find(string? path, string? root)
	{
		var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
		var folders = new List<string>();
		if (!string.IsNullOrWhiteSpace(root))
			folders.Add(root);
		if (!string.IsNullOrWhiteSpace(path))
			folders.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
		foreach (var folder in folders)
		{
			try
			{
				var dotnet = Path.Combine(folder.Trim('"'), name);
				// A runtime installed alone has a dotnet with no SDKs, which can't build
				if (File.Exists(dotnet) && Directory.Exists(Path.Combine(folder.Trim('"'), "sdk"))
					&& Directory.EnumerateDirectories(Path.Combine(folder.Trim('"'), "sdk")).Any())
					return dotnet;
			}
			catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
			{
				// A malformed or unreadable PATH entry is passed over
			}
		}
		return null;
	}

	/// <summary>What to say when a project with code is opened without the SDK.</summary>
	public const string Missing =
		"Opening a project with code needs the .NET SDK (dotnet), which isn't installed or isn't on the PATH. "
		+ "Its music files are opened alone: songs, patches and kits play and can be edited, but no C# is built. "
		+ "Install the SDK from https://dot.net and open the project again for its code.";
}
