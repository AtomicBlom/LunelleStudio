using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

using Loom.Audio.Synth.Chip;

using SUIsei;
using SUIsei.Controls;
using SUIsei.Media;
using SUIsei.Shapes;

namespace Loom.Audio.Workbench.Views;

/// <summary>
/// The patch designer's operators as a graph, in render order: a column per depth (an operator one right of the deepest it
/// reads), modulation drawn amber with its index, driving drawn teal and dashed, every heard operator to Out. A click on
/// a node chooses it; a click on Out, the voice.
/// </summary>
internal sealed class OperatorGraph : ContentControl
{
	private const float NodeWidth = 176f;
	private const float NodeHeight = 62f;
	private const float GapX = 70f;
	private const float GapY = 14f;
	private const float Pad = 16f;

	private readonly Canvas _canvas = new();
	private readonly ScrollViewer _scroll = new() { HorizontalScrollMode = ScrollMode.Enabled };
	private readonly DoubleCollection _dash = [4, 3];

	public OperatorGraph()
	{
		IsTabStop = false;
		_scroll.Content = _canvas;
		Content = _scroll;
	}

	/// <summary>The designer whose patch it draws.</summary>
	public PatchDesigner? Designer
	{
		get;
		set
		{
			if (field is { } old)
			{
				old.PropertyChanged -= OnDesignerChanged;
				old.Nodes.CollectionChanged -= OnNodesChanged;
			}
			field = value;
			if (value is not null)
			{
				value.PropertyChanged += OnDesignerChanged;
				value.Nodes.CollectionChanged += OnNodesChanged;
			}
			Draw();
		}
	}

	private void OnDesignerChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(PatchDesigner.Code) or nameof(PatchDesigner.Selected) or nameof(PatchDesigner.ListenAlone))
			Draw();
	}

	private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Draw();

	private void Draw()
	{
		_canvas.Children.Clear();
		if (Designer is not { IsLoaded: true } designer)
			return;
		var spec = designer.Spec;
		var nodes = designer.Nodes;
		if (nodes.Count == 0)
		{
			_canvas.Children.Add(new TextBlock { Text = "No operators yet: + Operator adds one; a Sine is the simplest.", FontSize = 13f, Foreground = Theme.Dim });
			_canvas.Width = 400f;
			_canvas.Height = 40f;
			return;
		}
		var columns = nodes.Max(n => n.Column) + 1;
		var rows = nodes.GroupBy(n => n.Column).Max(g => g.Count());
		var height = Pad * 2f + rows * NodeHeight + (rows - 1) * GapY;
		var columnHeight = nodes.GroupBy(n => n.Column).ToDictionary(g => g.Key, g => g.Count() * NodeHeight + (g.Count() - 1) * GapY);
		(float X, float Y) At(OperatorNodeViewModel n) =>
			(Pad + n.Column * (NodeWidth + GapX), (height - columnHeight[n.Column]) / 2f + n.Row * (NodeHeight + GapY));
		var outX = Pad + columns * (NodeWidth + GapX);
		var outY = height / 2f - 30f;
		_canvas.Width = outX + 96f + Pad;
		_canvas.Height = height;

		// The edges first, under the nodes
		foreach (var node in nodes)
		{
			if (node.Index >= spec.Operators.Count)
				continue;
			var op = spec.Operators[node.Index];
			var (x, y) = At(node);
			for (var m = 0; m < op.Modulators.Length; m++)
			{
				var from = nodes.FirstOrDefault(n => n.Index == op.Modulators[m].Operator);
				if (from is null)
					continue;
				var (fx, fy) = At(from);
				Edge(fx + NodeWidth, fy + NodeHeight / 2f, x, y + 18f + m * 8f, Theme.Accent, false);
				Label(x - 34f, y + 6f + m * 8f, "×" + op.Modulators[m].Index.ToString("0.##", CultureInfo.InvariantCulture), Theme.Accent);
			}
			for (var d = 0; d < op.Drivers.Length; d++)
			{
				var from = nodes.FirstOrDefault(n => n.Index == op.Drivers[d]);
				if (from is null)
					continue;
				var (fx, fy) = At(from);
				Edge(fx + NodeWidth, fy + NodeHeight / 2f, x, y + 24f + d * 8f, Theme.Drive, true);
			}
			if (op.Feedback != 0f)
				Label(x + NodeWidth - 50f, y - 14f, "fb " + op.Feedback.ToString("0.##", CultureInfo.InvariantCulture), Theme.Accent);
		}
		foreach (var tap in spec.Outputs)
		{
			var from = nodes.FirstOrDefault(n => n.Index == tap.Operator);
			if (from is null)
				continue;
			var (fx, fy) = At(from);
			var dim = designer.ListenAlone && tap.Operator != designer.Selected;
			Edge(fx + NodeWidth, fy + NodeHeight / 2f, outX, outY + 30f, dim ? Theme.Line : Theme.Faint, false);
			if (tap.Level != 1f || tap.Pan != 0f)
				Label(fx + NodeWidth + 6f, fy + NodeHeight / 2f - 16f,
					tap.Level.ToString("0.###", CultureInfo.InvariantCulture) + (tap.Pan != 0f ? " ◂▸" + tap.Pan.ToString("0.##", CultureInfo.InvariantCulture) : ""), Theme.Dim);
		}

		foreach (var node in nodes)
		{
			var (x, y) = At(node);
			var index = node.Index;
			var box = new Border
			{
				Width = NodeWidth,
				Height = NodeHeight,
				CornerRadius = new CornerRadius(6),
				Background = Theme.Raised,
				BorderBrush = node.IsSelected ? Theme.Accent : Theme.Line,
				BorderThickness = new Thickness(node.IsSelected ? 2f : 1f),
			};
			var stack = new StackPanel { Margin = new Thickness(10f, 5f, 8f, 5f) };
			stack.Children.Add(new TextBlock { Text = node.Summary, FontSize = 10f, Foreground = Display.Kind(node.Kind) });
			stack.Children.Add(new TextBlock { Text = node.Name + (node.IsAlone ? "  · alone" : ""), FontSize = 13f, Foreground = Theme.Ink, Margin = new Thickness(0f, 2f, 0f, 2f) });
			stack.Children.Add(new TextBlock { Text = $"#{index + 1} · {node.Detail}", FontSize = 10f, Foreground = Theme.Faint, TextTrimming = TextTrimming.CharacterEllipsis });
			var stripe = new Border { Width = 4f, Height = NodeHeight - 14f, CornerRadius = new CornerRadius(2), Background = Display.Kind(node.Kind), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(3f, 0f, 0f, 0f) };
			var cell = new Grid();
			cell.Children.Add(stripe);
			cell.Children.Add(stack);
			box.Child = cell;
			box.PointerPressed += (_, e) =>
			{
				designer.Select(index);
				e.Handled = true;
			};
			Place(box, x, y);
		}
		var output = new Border
		{
			Width = 92f,
			Height = 60f,
			CornerRadius = new CornerRadius(30),
			Background = Theme.Raised,
			BorderBrush = designer.Selected < 0 ? Theme.Accent : Theme.Line,
			BorderThickness = new Thickness(designer.Selected < 0 ? 2f : 1f),
		};
		var words = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		words.Children.Add(new TextBlock { Text = "Out", FontSize = 13f, Foreground = Theme.Ink, HorizontalAlignment = HorizontalAlignment.Center });
		words.Children.Add(new TextBlock { Text = $"{spec.Outputs.Count} heard", FontSize = 10f, Foreground = Theme.Faint, HorizontalAlignment = HorizontalAlignment.Center });
		output.Child = words;
		output.PointerPressed += (_, e) =>
		{
			designer.Select(-1);
			e.Handled = true;
		};
		Place(output, outX, outY);
	}

	private void Edge(float x1, float y1, float x2, float y2, Brush brush, bool dashed)
	{
		var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 1.5f, IsHitTestVisible = false };
		if (dashed)
			line.StrokeDashArray = _dash;
		_canvas.Children.Add(line);
	}

	private void Label(float x, float y, string text, Brush brush) =>
		Place(new TextBlock { Text = text, FontSize = 10f, Foreground = brush, IsHitTestVisible = false }, x, y);

	private void Place(UIElement element, float x, float y)
	{
		Canvas.SetLeft(element, x);
		Canvas.SetTop(element, y);
		_canvas.Children.Add(element);
	}
}
