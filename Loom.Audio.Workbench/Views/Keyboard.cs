using System.ComponentModel;

using Loom.Audio.Synth.Chip;

using SUIsei;
using SUIsei.Controls;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// Two octaves of piano keys (<c>Keyboard.xaml</c>) from <see cref="ViewModel"/>'s octave, drawn in code as the keys move
/// with it: a key down plays its note, up lets it go.
/// </summary>
internal sealed partial class Keyboard : ContentControl
{
	private const float WhiteWidth = 30f;
	private const float WhiteHeight = 110f;
	private const float BlackWidth = 20f;
	private const float BlackHeight = 68f;

	// Which semitones of an octave are white keys
	private static readonly bool[] IsWhite = [true, false, true, false, true, true, false, true, false, true, false, true];

	private readonly Dictionary<int, NoteHandle> _held = [];

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
				field.PropertyChanged -= OnChanged;
			ReleaseAll();
			field = value;
			if (value is not null)
				value.PropertyChanged += OnChanged;
			Make();
		}
	}

	partial void OnComponentRebuilt() => Make();

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(WorkbenchViewModel.Octave))
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
		if (ViewModel is not { } model)
			return;
		var low = 12 * (model.Octave + 1);
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
		if (ViewModel is not { } model)
			return;
		Release(note);
		_held[note] = model.PlayNote(note);
	}

	private void Release(int note)
	{
		if (_held.Remove(note, out var handle))
			ViewModel?.ReleaseNote(handle);
	}
}
