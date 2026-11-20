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

	/// <summary>What it plays on: "patch acoustic-guitar", "kit general".</summary>
	public string Instrument { get; internal init; } = "";

	/// <summary>How tall its lane is, and its header with it: room for the most parts it plays in any one section.</summary>
	public float Height => ArrangeLayout.LaneHeight(Cells.Count == 0 ? 1 : Cells.Max(c => c.Placements.Count));

	/// <summary>How far Arrange's lanes are scrolled, which its header follows across.</summary>
	public ArrangeScroll Scroll => _workbench.ViewModel.ArrangeScroll;

	/// <summary>It, for a view built from all of it at once.</summary>
	public TrackViewModel Self => this;

	/// <summary>Its place in the song, which gives it its hue.</summary>
	public int Index { get; internal init; }

	/// <summary>The name of the patch or kit it plays.</summary>
	public string InstrumentName { get; internal init; } = "";

	/// <summary>Whether it plays a kit rather than a patch.</summary>
	public bool IsKit { get; internal init; }

	/// <summary>Renames it; why it can't be, or null.</summary>
	public string? Rename(string name) => _workbench.RenameTrack(Name, name);

	/// <summary>Gives it another of the catalog's instruments.</summary>
	public void SetInstrument(string instrument, bool isKit) => _workbench.SetInstrument(Name, instrument, isKit);

	/// <summary>What it could play instead: the catalog's patches and kits.</summary>
	public List<(string Name, bool IsKit)> Instruments => [.. _workbench.Instruments];

	/// <summary>Takes it out of the song, with everything it plays.</summary>
	public void Delete() => _workbench.DeleteTrack(Name);

	/// <summary>Adds an empty part for it in a section, chosen at once.</summary>
	public void AddPart(int section) => _workbench.AddPart(section, Name);

	/// <summary>Its row in Arrange: a cell per section, each with the parts it plays there.</summary>
	public ObservableCollection<LaneCellViewModel> Cells { get; } = [];
}
