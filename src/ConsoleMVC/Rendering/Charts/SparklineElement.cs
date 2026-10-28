using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// A series of values drawn as a one-line chart, e.g. <c>▁▂▃▅▇█▆▄</c>: the <c>&lt;sparkline&gt;</c> markup element. Each
/// value takes one cell; when there are more values than cells, the most recent ones are shown.
/// </summary>
/// <example>
/// <code>
/// var line = new SparklineElement { Label = "Queue ", Values = [1, 2, 4, 7, 5, 3], ShowLast = true };
/// renderer.Render(line); // Queue ▁▂▄█▆▃ 3
/// </code>
/// </example>
public sealed class SparklineElement : Element
{
    /// <summary>The eight levels, lowest first.</summary>
    public const string Levels = "▁▂▃▄▅▆▇█";

    /// <summary>Gets or sets the values, oldest first. Values that are not finite numbers leave a gap.</summary>
    /// <example><code>line.Values = ChartValues.From(visitsPerHour);</code></example>
    public IReadOnlyList<double> Values { get; set; } = [];

    /// <summary>Gets or sets the value drawn as the lowest level, or <see langword="null"/> for the smallest value shown.</summary>
    public double? Min { get; set; }

    /// <summary>Gets or sets the value drawn as the highest level, or <see langword="null"/> for the largest value shown.</summary>
    public double? Max { get; set; }

    /// <summary>Gets or sets the text before the chart.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets whether the latest value is shown after the chart.</summary>
    public bool ShowLast { get; set; }

    /// <summary>Gets or sets the .NET format of the latest value. Defaults to <see cref="BarChartElement.DefaultFormat"/>.</summary>
    public string Format { get; set; } = BarChartElement.DefaultFormat;

    /// <summary>Gets or sets the colour of the chart.</summary>
    public Colour Colour { get; set; }

    /// <summary>Gets or sets the colour of the label and the latest value.</summary>
    public Colour LabelColour { get; set; }

    /// <inheritdoc />
    /// <remarks>A sparkline is as wide as its data: one cell per value, plus its label and latest value.</remarks>
    public override int MeasureWidth(int maxWidth)
    {
        if (Values.Count == 0)
            return 0;

        return Math.Min(TextWidth.Of(SafeLabel()) + Values.Count + TextWidth.Of(Tail()), Math.Max(maxWidth, 0));
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        if (Values.Count == 0)
            return [];

        width = Math.Max(width, 0);
        var label = SafeLabel();
        var tail = Tail();
        var cells = Math.Max(width - TextWidth.Of(label) - TextWidth.Of(tail), 1);
        var shown = Values.Skip(Math.Max(Values.Count - cells, 0)).ToArray();

        var finite = shown.Where(double.IsFinite).ToArray();
        var low = Min ?? (finite.Length > 0 ? finite.Min() : 0);
        var high = Max ?? (finite.Length > 0 ? finite.Max() : 0);

        var chart = new string(shown.Select(v => Level(v, low, high)).ToArray());
        var labelStyle = TextStyle.Plain.WithForeground(LabelColour);
        var spans = new List<Span>();
        if (label.Length > 0)
            spans.Add(new Span(label, labelStyle));
        spans.Add(new Span(chart, TextStyle.Plain.WithForeground(Colour)));
        if (tail.Length > 0)
            spans.Add(new Span(tail, labelStyle));

        return [new RenderedLine(spans).Clip(width)];
    }

    private static char Level(double value, double low, double high)
    {
        if (!double.IsFinite(value))
            return ' ';

        if (!(high > low))
            return Levels[3];

        var fraction = (Math.Clamp(value, low, high) - low) / (high - low);
        return Levels[(int)Math.Round(fraction * (Levels.Length - 1), MidpointRounding.AwayFromZero)];
    }

    private string SafeLabel() => TextSanitiser.Sanitise(Label ?? "").Replace('\n', ' ');

    private string Tail() => ShowLast && Values.Count > 0
        ? " " + TextSanitiser.Sanitise(Values[^1].ToString(string.IsNullOrEmpty(Format) ? BarChartElement.DefaultFormat : Format,
            CultureInfo.CurrentCulture)).Replace('\n', ' ')
        : "";
}
