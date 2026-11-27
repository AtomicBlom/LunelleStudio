using System.Collections.Specialized;
using System.ComponentModel;

using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// Arrange's automation lane (docs/spec/ui-exports.md), under the tracks: an export chosen at its head, and its events
/// over the sections' columns, each a mark labelled with what it does. A click on a mark edits or deletes its event;
/// + event adds one. Its head stays to the left as the lanes scroll across, as a track's does.
/// </summary>
internal sealed class AutomationLane : ContentControl
{
	private const float HeadWidth = 176f;
	private const float LaneHeight = 46f;

	private readonly BinaryVibrance.Vysara.Media.TranslateTransform _stay = new();
	private readonly Border _head = new();
	private readonly Canvas _marks = new() { Height = LaneHeight };
	private bool _building;

	public AutomationLane()
	{
		IsTabStop = false;
		_head.RenderTransform = _stay;
	}

	public WorkbenchViewModel? ViewModel
	{
		get;
		set
		{
			if (field is not null)
			{
				field.PropertyChanged -= OnChanged;
				field.Sections.CollectionChanged -= OnSections;
				field.ArrangeScroll.PropertyChanged -= OnScrolled;
			}
			field = value;
			if (value is not null)
			{
				value.PropertyChanged += OnChanged;
				value.Sections.CollectionChanged += OnSections;
				value.ArrangeScroll.PropertyChanged += OnScrolled;
			}
			Build();
		}
	}

	private void OnChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(WorkbenchViewModel.LaneExport) or nameof(WorkbenchViewModel.LaneMarks)
			or nameof(WorkbenchViewModel.LaneExports) or nameof(WorkbenchViewModel.LanePlaced) or nameof(WorkbenchViewModel.HasTracks))
			Build();
	}

	private void OnSections(object? sender, NotifyCollectionChangedEventArgs e) => Build();

	private void OnScrolled(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ArrangeScroll.X) && ViewModel is { } model)
			_stay.X = model.ArrangeScroll.X;
	}

	private void Build()
	{
		if (_building)
			return;
		_building = true;
		try
		{
			Content = ViewModel is { } model && model.Workbench.CanEditSong ? Lane(model) : null;
		}
		finally
		{
			_building = false;
		}
	}

	private Grid Lane(WorkbenchViewModel model)
	{
		var workbench = model.Workbench;
		var grid = new Grid { Height = LaneHeight };
		grid.ColumnDefinitions.Add(GridLength.Pixels(HeadWidth));
		grid.ColumnDefinitions.Add(GridLength.Auto);

		// The ground: a column per section, edged as the lanes' cells are
		var lefts = new List<float>();
		var width = 0f;
		var ground = new StackPanel { Orientation = Orientation.Horizontal };
		foreach (var section in model.Sections)
		{
			lefts.Add(width);
			width += section.Width;
			ground.Children.Add(new Border
			{
				Width = section.Width,
				Height = LaneHeight,
				BorderBrush = Theme.Line,
				BorderThickness = new Thickness(1f, 0f, 0f, 1f),
				Background = Theme.Sunk,
			});
		}
		var area = new Grid { Width = width };
		area.Children.Add(ground);
		_marks.Children.Clear();
		_marks.Width = width;
		area.Children.Add(_marks);
		Grid.SetColumn(area, 1);
		grid.Children.Add(area);

		var export = model.LaneExport;
		foreach (var mark in model.LaneMarks)
		{
			if (mark.Section < 0 || mark.Section >= model.Sections.Count)
				continue;
			var x = lefts[mark.Section] + LaneCell.Inset + mark.Across * (model.Sections[mark.Section].Width - 2f * LaneCell.Inset);
			var tick = new Border { Width = 2f, Height = LaneHeight - 8f, Background = Theme.Accent, IsHitTestVisible = false };
			Canvas.SetLeft(tick, x - 1f);
			Canvas.SetTop(tick, 4f);
			_marks.Children.Add(tick);
			var label = new Button
			{
				Content = new TextBlock { Text = mark.Label, FontSize = 10.5f, Foreground = Theme.Ink },
				Padding = new Thickness(5f, 1f, 5f, 2f),
				Background = Theme.Raised,
				BorderBrush = Theme.Accent,
				BorderThickness = new Thickness(1f),
			};
			ToolTipService.SetToolTip(label, $"At {mark.At}: {mark.Label}. Click to edit or delete it.");
			var index = mark.Index;
			label.Click += (_, _) =>
			{
				if (export is not null)
					EditEvent(workbench, export, index, label);
			};
			Canvas.SetLeft(label, x + 3f);
			Canvas.SetTop(label, 6f + (mark.Index % 2) * 17f);
			_marks.Children.Add(label);
		}

		// The head: the export shown, and a new event
		var head = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10f, 4f, 8f, 4f) };
		head.Children.Add(new TextBlock { Text = "AUTOMATION", FontSize = 10f, Foreground = Theme.Faint });
		var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 3f, 0f, 0f) };
		var exports = new List<object?> { "none" };
		exports.AddRange(model.LaneExports);
		var choice = new ComboBox { ItemsSource = exports, SelectedIndex = export is null ? 0 : Math.Max(0, model.LaneExports.ToList().IndexOf(export) + 1), Width = 100f, Height = 24f };
		ToolTipService.SetToolTip(choice, model.LaneExports.Count == 0 ? "This song has no exports: record one, from exports" : "The export whose events show here");
		choice.SelectionChanged += (_, _) =>
		{
			if (!_building)
				workbench.ShowLane(choice.SelectedIndex <= 0 ? null : model.LaneExports[choice.SelectedIndex - 1]);
		};
		row.Children.Add(choice);
		if (export is not null)
		{
			var add = new Button { Content = new TextBlock { Text = "+ event", FontSize = 11f }, Padding = new Thickness(6f, 1f, 6f, 2f), Margin = new Thickness(6f, 0f, 0f, 0f) };
			ToolTipService.SetToolTip(add, model.LanePlaced
				? "Add an event to this export"
				: "Add an event to this export. Its bars are placed along the sections in order until it plays live, then where it went.");
			add.Click += (_, _) => EditEvent(workbench, export, null, add);
			row.Children.Add(add);
		}
		head.Children.Add(row);
		_head.Child = head;
		_head.Width = HeadWidth;
		_head.Background = Theme.Panel;
		_head.BorderBrush = Theme.LineStrong;
		_head.BorderThickness = new Thickness(0f, 0f, 1f, 1f);
		if (ViewModel is { } m)
			_stay.X = m.ArrangeScroll.X;
		grid.Children.Add(_head);
		return grid;
	}

	// A flyout editing an event (or making one, with no index): where, what, its value, its length; save, or delete
	private static void EditEvent(Workbench workbench, string export, int? index, FrameworkElement anchor)
	{
		var targets = workbench.LaneTargets();
		var was = index is { } i ? workbench.ExportEvent(export, i) : null;
		var column = new StackPanel { Orientation = Orientation.Vertical, Width = 240f };
		column.Children.Add(new TextBlock { Text = index is null ? $"New event of {export}" : $"Event of {export}", FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		TextBox Field(string caption, string text, string tip)
		{
			column.Children.Add(new TextBlock { Text = caption, FontSize = 11f, Foreground = Theme.Faint, Margin = new Thickness(0f, 4f, 0f, 2f) });
			var box = new TextBox { Text = text, Width = 240f };
			ToolTipService.SetToolTip(box, tip);
			column.Children.Add(box);
			return box;
		}
		var at = Field("At", was?.At ?? "1:1", "A bar and beat (17:1, 9:2.5), or a section's start or end (section lift, section lift#2, end of section lift)");
		column.Children.Add(new TextBlock { Text = "Does", FontSize = 11f, Foreground = Theme.Faint, Margin = new Thickness(0f, 4f, 0f, 2f) });
		var what = new ComboBox
		{
			ItemsSource = targets.Select(t => (object?)t.Label).ToList(),
			SelectedIndex = was?.Target is { } target ? Math.Max(0, targets.ToList().IndexOf(target)) : 0,
			Width = 240f,
		};
		column.Children.Add(what);
		var to = Field("To", was?.To ?? "", "A level or a macro from 0 to 1, a choice's option, a section for a jump; empty lets a macro go");
		var over = Field("Over", was?.Over ?? "", "Ramped over a length (2 beats, 1 bar, 0.5s); empty sets it at once");
		var problem = new TextBlock { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Width = 240f };
		var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0f, 8f, 0f, 0f) };
		var save = new Button { Content = new TextBlock { Text = index is null ? "add" : "save", FontSize = 12f }, Margin = new Thickness(0f, 0f, 6f, 0f) };
		buttons.Children.Add(save);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.TopEdgeAlignedLeft };
		void Done(string? why)
		{
			problem.Text = why ?? "";
			problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
			if (why is null)
				flyout.Hide();
		}
		save.Click += (_, _) => Done(what.SelectedIndex < 0
			? "Choose what it does."
			: workbench.PutExportEvent(export, index, at.Text, targets[what.SelectedIndex], to.Text, over.Text));
		if (index is { } at2)
		{
			var delete = new Button { Content = new TextBlock { Text = "delete", FontSize = 12f } };
			delete.Click += (_, _) => Done(workbench.DeleteExportEvent(export, at2));
			buttons.Children.Add(delete);
		}
		column.Children.Add(buttons);
		column.Children.Add(problem);
		flyout.ShowAt(anchor);
	}
}
