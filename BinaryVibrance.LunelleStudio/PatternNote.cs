namespace BinaryVibrance.LunelleStudio;

/// <summary>One note of a pattern: where it starts and how long, in ticks, its row, and how hard.</summary>
/// <param name="Tick">Its start, in ticks from the pass's start.</param>
/// <param name="Length">How long it sounds, in ticks.</param>
/// <param name="Row">Its row: a pitch, highest first, or a drum piece.</param>
/// <param name="Velocity">How hard, 0 to 1.</param>
public readonly record struct PatternNote(int Tick, int Length, int Row, float Velocity);
