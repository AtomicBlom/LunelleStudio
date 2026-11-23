namespace BinaryVibrance.LunelleStudio;

/// <summary>
/// How far Arrange's lanes are scrolled, which the parts that stay put follow: the section headers keep to the top as the
/// lanes scroll down, the track headers to the left as they scroll across.
/// </summary>
public sealed class ArrangeScroll : ViewModel
{
	/// <summary>How far across, in DIPs.</summary>
	public float X
	{
		get;
		internal set => Set(ref field, value);
	}

	/// <summary>How far down, in DIPs.</summary>
	public float Y
	{
		get;
		internal set => Set(ref field, value);
	}
}
