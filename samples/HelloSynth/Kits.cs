using BinaryVibrance.Lunelle.Chip;

namespace HelloSynth;

/// <summary>
/// The drum kits: each a file (Kits/*.kit.json, embedded) whose pieces name patches in Patches/, so the demo needs no
/// samples.
/// </summary>
public static class Kits
{
	/// <summary>Kick, snare, and closed and open hats that choke each other.</summary>
	public static DrumKit DemoKit => DrumKit.Load(Read, "demo-kit.kit.json", PatchNamed);

	private static Patch? PatchNamed(string name) =>
		Patches.Files.FirstOrDefault(p => p.Name == name) is { Build: { } build } ? build() : null;

	// A kit's file, embedded under its own name
	private static byte[] Read(string name)
	{
		using var stream = typeof(Kits).Assembly.GetManifestResourceStream(name)
			?? throw new FileNotFoundException($"No embedded resource '{name}'.", name);
		using var copy = new MemoryStream();
		stream.CopyTo(copy);
		return copy.ToArray();
	}
}
