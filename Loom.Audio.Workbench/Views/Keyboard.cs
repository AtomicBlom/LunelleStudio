using Loom.Audio.Synth.Chip;

using SUIsei;
using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>Two octaves of piano keys (<c>Keyboard.xaml</c>), made in code: a key down plays its note, up lets it go.</summary>
internal sealed partial class Keyboard : ContentControl
{
	private const float WhiteWidth = 30f;
	private const float WhiteHeight = 110f;
	private const float BlackWidth = 20f;
	private const float BlackHeight = 68f;

	// Which semitones of an octave are white keys, and each white key's index in it
	private static readonly bool[] IsWhite = [true, false, true, false, true, true, false, true, false, true, false, true];

	private readonly WorkbenchViewModel _model;
	private readonly Dictionary<int, NoteHandle> _held = [];

	/// <summary>A keyboard over <paramref name="viewModel"/>'s bench.</summary>
	public Keyboard(WorkbenchViewModel viewModel)
	{
		_model = viewModel;
		InitializeComponent();
		Make();
	}

	partial void OnComponentRebuilt() => Make();

	/// <summary>The keys again, after the octave moves.</summary>
	public void Relabel() => Make();

	private void Make()
	{
		foreach (var note in _held.Values)
			_model.ReleaseNote(note);
		_held.Clear();
		Keys.Children.Clear();
		var low = 12 * (_model.Octave + 1);
		// White keys first, so the black ones lie over them
		var white = 0;
		for (var k = 0; k < 24; k++)
			if (IsWhite[k % 12])
				Add(low + k, white++ * WhiteWidth, WhiteWidth - 2f, WhiteHeight, Theme.WhiteKey);
		white = 0;
		for (var k = 0; k < 24; k++)
		{
			if (IsWhite[k % 12])
			{
				white++;
				continue;
			}
			Add(low + k, white * WhiteWidth - BlackWidth / 2f - 1f, BlackWidth, BlackHeight, Theme.BlackKey);
		}
	}

	private void Add(int note, float x, float width, float height, SUIsei.Media.Brush brush)
	{
		var key = new Border { Width = width, Height = height, Background = brush, CornerRadius = new CornerRadius(0, 0, 3, 3), Name = $"Key{note}" };
		key.PreviewPointerPressed += (_, _) => Press(note);
		key.PreviewPointerReleased += (_, _) => Release(note);
		key.PointerExited += (_, _) => Release(note);
		Canvas.SetLeft(key, x);
		Canvas.SetTop(key, 0f);
		Keys.Children.Add(key);
	}

	private void Press(int note)
	{
		Release(note);
		_held[note] = _model.PlayNote(note);
	}

	private void Release(int note)
	{
		if (_held.Remove(note, out var handle))
			_model.ReleaseNote(handle);
	}
}
