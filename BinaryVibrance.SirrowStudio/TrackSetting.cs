namespace BinaryVibrance.SirrowStudio;

/// <summary>A track's mute, solo and gain, as a <see cref="WorkbenchState"/> keeps them.</summary>
public sealed class TrackSetting
{
	/// <summary>Silenced.</summary>
	public bool Muted { get; set; }

	/// <summary>Soloed.</summary>
	public bool Solo { get; set; }

	/// <summary>Its gain, in decibels.</summary>
	public float GainDb { get; set; }
}
