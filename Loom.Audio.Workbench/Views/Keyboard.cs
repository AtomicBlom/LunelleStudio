using System.ComponentModel;

using BinaryVibrance.Sirrow.Synth.Chip;

using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

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
	// The gap left between neighbouring keys, white or black
	private const float Gap = 2f;

	// Which semitones of an octave are white keys
	private static readonly bool[] IsWhite = [true, false, true, false, true, true, false, true, false, true, false, true];

	private readonly Dictionary<int, NoteHandle> _held = [];
	// Each key drawn by its note: its top (a white key's stem between the black keys beside it) and, for a white key, the
	// full-width foot below them, so a faded key shows nothing of another through it; the first's note, and what each
	// was last lit with (so a refresh changes only what moved)
	private readonly Border[] _keys = new Border[Octaves * 12];
	private readonly Border?[] _feet = new Border?[Octaves * 12];
	private readonly BinaryVibrance.Vysara.Media.Brush?[] _lit = new BinaryVibrance.Vysara.Media.Brush?[Octaves * 12];
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
		Array.Clear(_feet);
		if (ViewModel is not { } model)
			return;
		_low = 12 * model.Octave;
		_scale = model.Workbench.ScaleClasses();
		var white = 0;
		for (var k = 0; k < Octaves * 12; k++)
		{
			if (!IsWhite[k % 12])
			{
				Add(k, BlackLeft(white), BlackWidth, BlackHeight, Theme.BlackKey);
				continue;
			}
			// A white key's stem is cut back from the black keys either side of it, a gap short of them
			var left = white * WhiteWidth;
			var right = left + WhiteWidth - Gap;
			var stemLeft = k % 12 > 0 && !IsWhite[k % 12 - 1] ? BlackLeft(white) + BlackWidth + Gap : left;
			var stemRight = k % 12 < 11 && k + 1 < Octaves * 12 && !IsWhite[k % 12 + 1] ? BlackLeft(white + 1) - Gap : right;
			Add(k, stemLeft, stemRight - stemLeft, BlackHeight + Gap, Theme.WhiteKey);
			_keys[k].CornerRadius = new CornerRadius(0f);
			_feet[k] = Foot(k, left, right - left);
			white++;
		}
		Light();
	}

	// Where the black key after the given number of white keys starts
	private static float BlackLeft(int whites) => whites * WhiteWidth - BlackWidth / 2f - Gap / 2f;

	private void Add(int k, float x, float width, float height, BinaryVibrance.Vysara.Media.Brush brush)
	{
		var key = Part(k, x, 0f, width, height, brush);
		_keys[k] = key;
	}

	// A white key's foot, under the black keys' ends, named C where it is one
	private Border Foot(int k, float x, float width)
	{
		var note = _low + k;
		var foot = Part(k, x, BlackHeight + Gap, width, WhiteHeight - BlackHeight - Gap, Theme.WhiteKey);
		if (k % 12 == 0)
			foot.Child = new TextBlock
			{
				Text = $"C{note / 12 - 1}",
				FontSize = 9f,
				Foreground = Theme.Faint,
				VerticalAlignment = VerticalAlignment.Bottom,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0f, 0f, 0f, 4f),
			};
		return foot;
	}

	// One piece of key k, pressing it playing the key's note
	private Border Part(int k, float x, float y, float width, float height, BinaryVibrance.Vysara.Media.Brush brush)
	{
		var note = _low + k;
		var part = new Border
		{
			Width = width,
			Height = height,
			Background = brush,
			CornerRadius = new CornerRadius(0, 0, 3, 3),
			Opacity = _scale[note % 12] ? 1f : 0.45f,
		};
		part.PreviewPointerPressed += (_, _) => Press(note);
		part.PreviewPointerReleased += (_, _) => Release(note);
		part.PointerExited += (_, _) => Release(note);
		Canvas.SetLeft(part, x);
		Canvas.SetTop(part, y);
		Keys.Children.Add(part);
		return part;
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
				if (_feet[k] is { } foot)
					foot.Background = brush;
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
