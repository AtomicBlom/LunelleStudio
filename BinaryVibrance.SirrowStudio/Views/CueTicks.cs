using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.SirrowStudio.Views;

/// <summary>
/// The levels a cue is waited on, marked over its slider where they fall, each lit while it holds: so a glance says where
/// parts come in as the cue moves.
/// </summary>
internal sealed class CueTicks : ContentControl
{
	/// <summary>As wide as the slider under it.</summary>
	public const float DrawnWidth = 250f;

	private readonly Canvas _marks = new() { Width = DrawnWidth, Height = 18f };

	public CueTicks()
	{
		IsTabStop = false;
		IsHitTestVisible = false;
		Content = _marks;
	}

	/// <summary>The cue whose readers' levels it marks.</summary>
	public CueViewModel? Cue
	{
		get;
		set
		{
			if (field is { } old)
			{
				old.Readers.CollectionChanged -= OnReadersChanged;
				foreach (var reader in old.Readers)
					reader.PropertyChanged -= OnReaderChanged;
			}
			field = value;
			if (value is not null)
			{
				value.Readers.CollectionChanged += OnReadersChanged;
				foreach (var reader in value.Readers)
					reader.PropertyChanged += OnReaderChanged;
			}
			Draw();
		}
	}

	private void OnReadersChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (var reader in e.NewItems?.OfType<CueReaderViewModel>() ?? [])
			reader.PropertyChanged += OnReaderChanged;
		Draw();
	}

	private void OnReaderChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(CueReaderViewModel.IsMet))
			Draw();
	}

	private void Draw()
	{
		_marks.Children.Clear();
		if (Cue is not { } cue)
			return;
		// A label too near the last one drawn is left off (its tick still shows, and the readers below list it), so none overlap
		var lastLabel = float.NegativeInfinity;
		foreach (var level in cue.Readers.GroupBy(r => r.Level).OrderBy(g => g.Key))
		{
			var met = level.All(r => r.IsMet);
			var x = level.Key * (DrawnWidth - 12f) + 6f;
			var tick = new Border { Width = 1.5f, Height = 7f, Background = Display.Met(met) };
			Canvas.SetLeft(tick, x);
			Canvas.SetTop(tick, 11f);
			_marks.Children.Add(tick);
			if (x - lastLabel < 26f)
				continue;
			lastLabel = x;
			var label = new TextBlock { Text = level.Key.ToString("0.##", CultureInfo.InvariantCulture), FontSize = 10f, Foreground = Display.Met(met) };
			Canvas.SetLeft(label, x - 9f);
			Canvas.SetTop(label, -2f);
			_marks.Children.Add(label);
		}
	}
}
