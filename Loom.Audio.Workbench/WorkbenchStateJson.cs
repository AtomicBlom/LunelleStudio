using System.Text.Json.Serialization;

namespace Loom.Audio.Workbench;

/// <summary>The workbench state's JSON, made at build time so it needs no reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WorkbenchState))]
public sealed partial class WorkbenchStateJson : JsonSerializerContext;
