using BinaryVibrance.LunelleStudio.Projects;
using Shouldly;

namespace BinaryVibrance.LunelleStudio.Tests;

public class DotnetSdkTests
{
	[Test]
	public void The_sdk_is_found_on_the_path_only_with_an_sdk_beside_it()
	{
		var root = Directory.CreateTempSubdirectory("lunelle-sdk-").FullName;
		try
		{
			var runtimeOnly = Directory.CreateDirectory(Path.Combine(root, "runtime")).FullName;
			var sdk = Directory.CreateDirectory(Path.Combine(root, "sdk-install")).FullName;
			var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
			File.WriteAllText(Path.Combine(runtimeOnly, name), "");
			File.WriteAllText(Path.Combine(sdk, name), "");
			Directory.CreateDirectory(Path.Combine(sdk, "sdk", "11.0.100"));

			DotnetSdk.Find(null, null).ShouldBeNull();
			DotnetSdk.Find(runtimeOnly, null).ShouldBeNull();
			DotnetSdk.Find(string.Join(Path.PathSeparator, Path.Combine(root, "nowhere"), runtimeOnly, sdk), null)
				.ShouldBe(Path.Combine(sdk, name));
			DotnetSdk.Find(null, sdk).ShouldBe(Path.Combine(sdk, name));
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Test]
	public void This_machine_which_builds_the_tests_has_an_sdk() => DotnetSdk.Find().ShouldNotBeNull();
}
