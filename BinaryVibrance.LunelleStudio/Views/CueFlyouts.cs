using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;
using BinaryVibrance.Vysara.Controls.Primitives;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// The cues' flyouts (docs/spec/ui-cues.md), shown from the library's Cues and from the cue strip: making a cue, and a
/// cue's menu (its options, where it starts and what it is for edited; renamed; deleted), each saying what a change does
/// to the game's code in a project with code.
/// </summary>
internal static class CueFlyouts
{
	/// <summary>A flyout at <paramref name="anchor"/> making a cue: its id (a namespace before a '/'), a level or a choice among options, and what it is for.</summary>
	public static void AskForCue(Workbench workbench, FrameworkElement anchor)
	{
		var id = new TextBox { Width = 240f };
		ToolTipService.SetToolTip(id, "Lowercase letters, digits and hyphens, a namespace before a '/': intensity, lava/heat");
		var choice = new ToggleSwitch { OnContent = "A choice", OffContent = "A level, 0 to 1" };
		var options = new TextBox { Width = 240f, Visibility = Visibility.Collapsed };
		ToolTipService.SetToolTip(options, "The options, by commas: Calm, Bright, Dark");
		choice.Toggled += (_, _) => options.Visibility = choice.IsOn ? Visibility.Visible : Visibility.Collapsed;
		var about = new TextBox { Width = 240f };
		ToolTipService.SetToolTip(about, "What it is for, shown as its tooltip and in the game's code");
		var problem = Problem(240f);
		var column = Column("New cue, imported into this song");
		column.Children.Add(id);
		column.Children.Add(choice);
		column.Children.Add(options);
		column.Children.Add(Caption("About"));
		column.Children.Add(about);
		var make = Action("make");
		make.Margin = new Thickness(0f, 8f, 0f, 0f);
		column.Children.Add(make);
		column.Children.Add(problem);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		make.Click += (_, _) =>
		{
			var named = choice.IsOn ? Split(options.Text) : null;
			if (Say(problem, workbench.NewCue(id.Text, named, about.Text)))
				flyout.Hide();
		};
		flyout.ShowAt(anchor);
	}

	/// <summary>
	/// A cue's menu at <paramref name="anchor"/>: its options, where it starts and what it is for, saved together; its
	/// rename in every song; its delete, while no song reads it.
	/// </summary>
	public static void ShowCueMenu(Workbench workbench, CueEntryViewModel cue, FrameworkElement anchor)
	{
		var definition = workbench.ProjectCues.FirstOrDefault(c => c.Id == cue.Id);
		var column = Column(cue.Id);
		if (workbench.CodeWarning(cue.Id) is { Length: > 0 } warning)
			column.Children.Add(new TextBlock { Text = warning, FontSize = 11f, Foreground = Theme.Dim, TextWrapping = TextWrapping.Wrap, Width = 240f, Margin = new Thickness(0f, 0f, 0f, 6f) });
		var problem = Problem(240f);
		var flyout = new Flyout { Content = column, Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		void Done(string? why)
		{
			if (Say(problem, why))
				flyout.Hide();
		}

		TextBox? options = null;
		if (definition?.IsChoice == true)
		{
			column.Children.Add(Caption("Options, by commas"));
			options = new TextBox { Width = 240f, Text = string.Join(", ", definition.Options.Select(o => o.Name)) };
			ToolTipService.SetToolTip(options, workbench.HasCode
				? workbench.CodeWarning(cue.Id, options: true)
				: "Add, reorder or take out options: songs name options, so reordering changes none. One a song names can't be taken out.");
			column.Children.Add(options);
		}
		column.Children.Add(Caption(definition?.IsChoice == true ? "Starts at (an option; empty for the first)" : "Starts at (0 to 1; empty for 0)"));
		var initial = new TextBox
		{
			Width = 240f,
			Text = definition is null ? "" : definition.IsChoice
				? definition.InitialOption > 0 ? definition.Options[definition.InitialOption].Name : ""
				: definition.InitialLevel != 0 ? definition.InitialLevel.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
		};
		column.Children.Add(initial);
		column.Children.Add(Caption("About"));
		var about = new TextBox { Width = 240f, Text = cue.About };
		column.Children.Add(about);
		var save = Action("save");
		save.Margin = new Thickness(0f, 6f, 0f, 8f);
		save.Click += (_, _) => Done(workbench.EditCue(cue.Id, options is null ? null : Split(options.Text), initial.Text, about.Text));
		column.Children.Add(save);

		column.Children.Add(Caption("Rename, in every song"));
		var name = new TextBox { Width = 240f, Text = cue.Id };
		column.Children.Add(name);
		var rename = Action("rename");
		var delete = Action("delete");
		rename.Margin = new Thickness(0f, 6f, 6f, 0f);
		delete.Margin = new Thickness(0f, 6f, 0f, 0f);
		var buttons = new StackPanel { Orientation = Orientation.Horizontal };
		buttons.Children.Add(rename);
		buttons.Children.Add(delete);
		column.Children.Add(buttons);
		column.Children.Add(problem);
		rename.Click += (_, _) => Done(cue.Rename(name.Text));
		delete.Click += (_, _) => Done(cue.Delete());
		flyout.ShowAt(anchor);
	}

	private static string[] Split(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static StackPanel Column(string title)
	{
		var column = new StackPanel { Orientation = Orientation.Vertical };
		column.Children.Add(new TextBlock { Text = title, FontSize = 12f, Foreground = Theme.Dim, Margin = new Thickness(0f, 0f, 0f, 6f) });
		return column;
	}

	private static TextBlock Caption(string text) => new() { Text = text, FontSize = 11f, Foreground = Theme.Faint, Margin = new Thickness(0f, 6f, 0f, 2f) };

	private static Button Action(string text) => new() { Content = new TextBlock { Text = text, FontSize = 12f } };

	private static TextBlock Problem(float width) => new() { FontSize = 11f, Foreground = Theme.Error, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Width = width };

	// Why something couldn't be done, shown; whether it was
	private static bool Say(TextBlock problem, string? why)
	{
		problem.Text = why ?? "";
		problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
		return why is null;
	}
}
