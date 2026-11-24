using BinaryVibrance.Vysara;
using BinaryVibrance.Vysara.Controls;

namespace BinaryVibrance.LunelleStudio.Views;

/// <summary>
/// The workbench's settings, in the flyout the rail's Settings opens: the theme, a key for each, the one showing lit, each
/// with what it looks like. A choice shows at once and is kept (<see cref="Workbench.SetTheme"/>, which the app saves).
/// </summary>
internal sealed class SettingsPanel : ContentControl
{
	private readonly Workbench _workbench;
	private readonly List<(ToggleButton Key, string Name)> _themes = [];

	public SettingsPanel(Workbench workbench)
	{
		_workbench = workbench;
		IsTabStop = false;
		var column = new StackPanel { Width = 300f, Margin = new Thickness(4f, 2f, 4f, 4f) };
		column.Children.Add(new TextBlock { Text = "SETTINGS", FontSize = 10f, Foreground = Theme.Faint, Margin = new Thickness(0f, 0f, 0f, 8f) });
		column.Children.Add(new TextBlock { Text = "Theme", FontSize = 14f, Foreground = Theme.Ink, Margin = new Thickness(0f, 0f, 0f, 6f) });
		foreach (var theme in ThemeDefinition.All)
		{
			// As wide as the key inside its padding, so the words sit at its left as the key centres them
			var words = new StackPanel { Width = 280f };
			words.Children.Add(new TextBlock { Text = theme.Name, FontSize = 13f });
			words.Children.Add(new TextBlock { Text = theme.About, FontSize = 11f, Opacity = 0.75f, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Normal });
			var key = new ToggleButton
			{
				Content = words,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Padding = new Thickness(10f, 5f, 10f, 6f),
				Margin = new Thickness(0f, 0f, 0f, 4f),
			};
			var name = theme.Name;
			key.Click += (_, _) => Choose(name);
			ToolTipService.SetToolTip(key, $"Use the {name} theme");
			_themes.Add((key, name));
			column.Children.Add(key);
		}
		Content = column;
		Light();
	}

	private void Choose(string name)
	{
		_workbench.SetTheme(name);
		Light();
	}

	// The theme showing lit, the rest not
	private void Light()
	{
		foreach (var (key, name) in _themes)
			key.IsChecked = name == Workbench.ThemeName;
	}
}
