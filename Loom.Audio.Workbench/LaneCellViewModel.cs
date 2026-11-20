using System.Collections.ObjectModel;

namespace Loom.Audio.Workbench;

/// <summary>Where a track's row crosses a section's column in Arrange: the track's parts placed in that section.</summary>
public sealed class LaneCellViewModel : ViewModel
{
	private bool _isCurrent;
	private float _progress;

	internal LaneCellViewModel(int section, float width)
	{
		Section = section;
		Width = width;
	}

	/// <summary>It, for a view built from all of it at once.</summary>
	public LaneCellViewModel Self => this;

	/// <summary>The track whose lane it is in.</summary>
	public TrackViewModel? Track { get; internal init; }

	/// <summary>The section's index in its song.</summary>
	public int Section { get; }

	/// <summary>How wide its column is drawn, as wide as its section on the timeline.</summary>
	public float Width { get; }

	/// <summary>The parts the track plays in the section.</summary>
	public ObservableCollection<PlacementViewModel> Placements { get; } = [];

	/// <summary>Whether its section is the one playing, so the playhead crosses it.</summary>
	public bool IsCurrent
	{
		get => _isCurrent;
		internal set => Set(ref _isCurrent, value);
	}

	/// <summary>How far through its section the song is, 0 to 1, while it plays.</summary>
	public float Progress
	{
		get => _progress;
		internal set
		{
			if (Set(ref _progress, value))
				Changed(nameof(PlayheadX));
		}
	}

	/// <summary>Where the playhead crosses the column, from its left edge.</summary>
	public float PlayheadX => Width * _progress;
}
