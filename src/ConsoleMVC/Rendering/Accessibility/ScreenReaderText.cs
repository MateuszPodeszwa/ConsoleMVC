using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Turns elements into plain text a screen reader reads well: boxes become their title and content, tables become
/// "Header: value" lines, columns are read one after another, separators are silent, and progress bars, spinners and
/// charts are described in words. Elements it does not know are drawn as they are.
/// </summary>
/// <example><code>renderer.Render(ScreenReaderText.Describe(table)); // Name: Ada, Age: 36</code></example>
public static class ScreenReaderText
{
    /// <summary>
    /// Returns an element that draws <paramref name="element"/> as plain lines of text.
    /// </summary>
    /// <param name="element">The element to describe.</param>
    /// <returns>The description; <paramref name="element"/> itself when it is already plain text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is <see langword="null"/>.</exception>
    /// <example><code>var plain = ScreenReaderText.Describe(new ProgressElement { Label = "Copy ", Value = 50 }); // Copy: 50%</code></example>
    public static Element Describe(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        switch (element)
        {
            case BoxElement box:
                var panel = new StackElement();
                if (!string.IsNullOrWhiteSpace(box.Title))
                    panel.Add(Text(box.Title));
                AddChildren(panel, box);
                return panel;

            case ColumnsElement columns:
                var stacked = new StackElement();
                foreach (var column in columns.Columns)
                    AddChildren(stacked, column);
                return stacked;

            case TableElement table:
                return Table(table);

            case SeparatorElement:
                return new StackElement();

            case ProgressElement progress:
                return Text($"{Label(progress.Label, "Progress")}: {Percent(progress.Fraction)}");

            case SpinnerElement spinner:
                var doing = string.IsNullOrWhiteSpace(spinner.Label) ? "Working" : spinner.Label.Trim();
                return Text(spinner.Done ? doing + ": done" : doing);

            case BarChartElement chart:
                var bars = new StackElement();
                foreach (var bar in chart.Bars)
                    bars.Add(Text($"{Label(bar.Label, "Value")}: {Number(bar.Value, chart.Format)}"));
                return bars;

            case SparklineElement sparkline:
                return Sparkline(sparkline);

            case ShortcutsElement shortcuts:
                return shortcuts.Items.Count == 0
                    ? new StackElement()
                    : Text("Shortcuts: " + string.Join(", ", shortcuts.Items.Select(s => $"{s.Key} {s.Description}")));

            case BreadcrumbsElement breadcrumbs:
                return breadcrumbs.Items.Count == 0
                    ? new StackElement()
                    : Text("You are here: " + string.Join(", ", breadcrumbs.Items));

            case MarkdownElement markdown:
                return new MarkdownElement
                {
                    Text = markdown.Text,
                    ShowLinkUrls = markdown.ShowLinkUrls,
                    UseBoxDrawing = false
                };

            case ContainerElement container:
                var children = new StackElement();
                AddChildren(children, container);
                return children;

            default:
                return element;
        }
    }

    private static void AddChildren(StackElement target, ContainerElement source)
    {
        foreach (var child in source.Children)
            target.Add(Describe(child));
    }

    private static Element Table(TableElement table)
    {
        var lines = new StackElement();
        var headers = table.Rows.FirstOrDefault(r => r.IsHeader)?.Cells.Select(c => c.Content.Trim()).ToArray() ?? [];

        foreach (var row in table.Rows.Where(r => !r.IsHeader))
        {
            var values = row.Cells.Select((cell, i) =>
                i < headers.Length && headers[i].Length > 0 ? $"{headers[i]}: {cell.Content.Trim()}" : cell.Content.Trim());
            lines.Add(Text(string.Join(", ", values)));
        }

        return lines;
    }

    private static Element Sparkline(SparklineElement sparkline)
    {
        var values = sparkline.Values.Where(double.IsFinite).ToArray();
        var label = Label(sparkline.Label, "Values");
        if (values.Length == 0)
            return Text($"{label}: no values");

        return Text($"{label}: {values.Length} values from {Number(values.Min(), sparkline.Format)} to " +
                    $"{Number(values.Max(), sparkline.Format)}, latest {Number(values[^1], sparkline.Format)}");
    }

    private static string Label(string? label, string fallback) =>
        string.IsNullOrWhiteSpace(label) ? fallback : label.Trim().TrimEnd(':');

    private static string Percent(double fraction) =>
        ((int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "%";

    private static string Number(double value, string format) =>
        value.ToString(string.IsNullOrEmpty(format) ? BarChartElement.DefaultFormat : format, CultureInfo.CurrentCulture);

    private static TextElement Text(string text)
    {
        var element = new TextElement();
        element.AppendText(text);
        return element;
    }

    /// <summary>Children one after another, with nothing around them.</summary>
    private sealed class StackElement : ContainerElement
    {
        public override int MeasureWidth(int maxWidth) => MeasureChildren(maxWidth);

        public override IReadOnlyList<RenderedLine> Layout(int width) => LayoutChildren(width);
    }
}
