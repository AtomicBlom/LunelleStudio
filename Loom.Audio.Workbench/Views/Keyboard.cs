using System.ComponentModel;

using Loom.Audio.Synth.Chip;

using SUIsei;
using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// Three octaves of piano keys (<c>Keyboard.xaml</c>), from the octave below <see cref="ViewModel"/>'s to the one above, drawn
/// in code as they move with it: a key down plays its note, up lets it go. Thirty times a second the keys light as the
/// track the keyboard plays sounds them in the song (a kit's pieces on the keys that play them), in its hue, and the keys
/// held lit too; keys whose note isn't in the song's scale are faded. Each C is named.
/// </summary>
internal sealed partial class Keyboard : ContentControl
{
	private const float WhiteWidth = 28f;
	private const float WhiteHeight = 110f;
	private const float BlackWidth = 18f;
	private const float BlackHeight = 68f;
	private const int Octaves = 3;

	// Which semitones of an octave are white keys
	private static readonly bool[] IsWhite = [true, false, true, false, true, true, false, true, false, true, false, true];

	private readonly Dictionary<int, NoteHandle> _held = [];
	// Each key drawn by its note, the first's note, and what each was last lit with (so a refresh changes only what moved)
	private readonly Border[] _keys = new Border[Octaves * 12];
	private readonly SUIsei.Media.Brush?[] _lit = new SUIsei.Media.Brush?[Octaves * 12];
	private int _low;
	private bool[] _scale = new bool[12];

	/// <summary>A keyboard with no keys until it is given a <see cref="ViewModel"/>.</summary>
	public Keyboard() => InitializeComponent();

	/// <summary>The bench it plays: the keys are drawn again when it is set and when its octave moves.</summary>
	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field == value)
				return;
			if (field is not null)
			{
				field.PropertyChanged -= OnChanged;
				field.Refreshed -= Light;
			}
			ReleaseAll();
			field = value;
			if (value is not null)
			{
				value.PropertyChanged += OnChanged;
				value.Refreshed += Light;
			}
			Make();
		}
	}

	partial void OnComponentRebuilt() => Make();

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(WorkbenchViewModel.Octave) or nameof(WorkbenchViewModel.ScaleText))
			Make();
	}

	private void ReleaseAll()
	{
		if (ViewModel is { } model)
			foreach (var note in _held.Values)
				model.ReleaseNote(note);
		_held.Clear();
	}

	private void Make()
	{
		ReleaseAll();
		Keys.Children.Clear();
		Array.Clear(_lit);
		if (ViewModel is not { } model)
			return;
		_low = 12 * model.Octave;
		_scale = model.Workbench.ScaleClasses();
		// White keys first, so the black ones lie over them
		var white = 0;
		for (var k = 0; k < Octaves * 12; k++)
			if (IsWhite[k % 12])
				Add(k, white++ * WhiteWidth, WhiteWidth - 2f, WhiteHeight, Theme.WhiteKey);
		white = 0;
		for (var k = 0; k < Octaves * 12; k++)
		{
			if (IsWhite[k % 12])
			{
				white++;
				continue;
			}
			Add(k, white * WhiteWidth - BlackWidth / 2f - 1f, BlackWidth, BlackHeight, Theme.BlackKey);
		}
		Light();
	}

	private void Add(int k, float x, float width, float height, SUIsei.Media.Brush brush)
	{
		var note = _low + k;
		var key = new Border
		{
			Width = width,
			Height = height,
			Background = brush,
			CornerRadius = new CornerRadius(0, 0, 3, 3),
			Name = $"Key{note}",
			Opacity = _scale[note % 12] ? 1f : 0.45f,
		};
		if (k % 12 == 0)
			key.Child = new TextBlock
			{
				Text = $"C{note / 12 - 1}",
				FontSize = 9f,
				Foreground = Theme.Faint,
				VerticalAlignment = VerticalAlignment.Bottom,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0f, 0f, 0f, 4f),
			};
		key.PreviewPointerPressed += (_, _) => Press(note);
		key.PreviewPointerReleased += (_, _) => Release(note);
		key.PointerExited += (_, _) => Release(note);
		Canvas.SetLeft(key, x);
		Canvas.SetTop(key, 0f);
		_keys[k] = key;
		Keys.Children.Add(key);
	}

	// The keys the focused track sounds, and those held, lit in its hue; the rest as they rest
	private void Light()
	{
		if (ViewModel is not { } model || _keys[0] is null)
			return;
		var focused = model.Workbench.FocusedSounding();
		var hue = focused is { } f ? Theme.TrackHue(f.Index) : Theme.Accent;
		for (var k = 0; k < _keys.Length; k++)
		{
			var note = _low + k;
			var sounding = focused is { } track && (track.IsKit
				// A kit's piece n is on the keys whose note within the octave is n, as the keyboard plays them
				? ((track.Notes >> (note % 12)) & UInt128.One) != UInt128.Zero
				: ((track.Notes >> note) & UInt128.One) != UInt128.Zero);
			var brush = sounding || _held.ContainsKey(note) ? hue : IsWhite[k % 12] ? Theme.WhiteKey : Theme.BlackKey;
			if (!ReferenceEquals(_lit[k], brush))
			{
				_lit[k] = brush;
				_keys[k].Background = brush;
			}
		}
	}

	private void Press(int note)
	{
		if (ViewModel is not { } model)
			return;
		Release(note);
		_held[note] = model.PlayNote(note);
		Light();
	}

	private void Release(int note)
	{
		if (_held.Remove(note, out var handle))
		{
			ViewModel?.ReleaseNote(handle);
			Light();
		}
	}
}
