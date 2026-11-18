using System.Collections.ObjectModel;

namespace Loom.Audio.Workbench;

/// <summary>A track of the song: mute, solo and gain (each kept by name through a rebuild), its meter and its placements.</summary>
public sealed class TrackViewModel : ViewModel
{
	private readonly Workbench _workbench;
	private bool _muted;
	private bool _solo;
	private float _gainDb;
	private float _peak;

	internal TrackViewModel(Workbench workbench, string name, bool muted, bool solo, float gainDb)
	{
		_workbench = workbench;
		Name = name;
		_muted = muted;
		_solo = solo;
		_gainDb = gainDb;
	}

	/// <summary>Its name.</summary>
	public string Name { get; }

	/// <summary>Silenced. Setting it acts.</summary>
	public bool Muted
	{
		get => _muted;
		set
		{
			if (Set(ref _muted, value))
				_workbench.SetMute(Name, value);
		}
	}

	/// <summary>Soloed: while any track is, the others are silent. Setting it acts.</summary>
	public bool Solo
	{
		get => _solo;
		set
		{
			if (Set(ref _solo, value))
				_workbench.SetSolo(Name, value);
		}
	}

	/// <summary>Its gain over its level, in decibels, −24 to +6. Setting it acts.</summary>
	public float GainDb
	{
		get => _gainDb;
		set
		{
			value = Math.Clamp(value, -24f, 6f);
			if (Set(ref _gainDb, value))
				_workbench.SetGain(Name, value);
		}
	}

	/// <summary>How loud it was a moment ago, a linear level.</summary>
	public float Peak
	{
		get => _peak;
		internal set => Set(ref _peak, value);
	}

	/// <summary>The parts it plays, in every section.</summary>
	public ObservableCollection<PlacementViewModel> Placements { get; } = [];
}
