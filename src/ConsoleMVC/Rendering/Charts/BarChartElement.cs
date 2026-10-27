using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>One bar of a <see cref="BarChartElement"/>: the <c>&lt;bar&gt;</c> markup element.</summary>
/// <example><code>chart.Add(new BarElement { Label = "Mon", Value = 1240 });</code></example>
public sealed class BarElement
{
    /// <summary>Gets or sets the text before the bar.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the bar's value; values below 0 are drawn as an empty bar.</summary>
    public double Value { get; set; }

    /// <summary>Gets or sets the colour of this bar, or <see langword="null"/> for the chart's <see cref="BarChartElement.Colour"/>.</summary>
    public Colour? Colour { get; set; }
}

/// <summary>
/// A horizontal bar chart, one bar per line with its label and value: the <c>&lt;bar-chart&gt;</c> markup element. Bars
/// are scaled to <see cref="Max"/> (or the largest value) and fill the width they are given.
/// </summary>
/// <example>
/// <code>
/// var chart = new BarChartElement { Format = "N0" };
/// chart.Add(new BarElement { Label = "Mon", Value = 1240 });
/// chart.Add(new BarElement { Label = "Tue", Value = 750 });
/// renderer.Render(chart);
/// // Mon ██████████████████████████ 1,240
/// // Tue ███████████████▊             750
/// </code>
/// </example>
public sealed class BarChartElement : Element
{
    /// <summary>The value format used unless <see cref="Format"/> is set: up to two decimals.</summary>
    public const string DefaultFormat = "0.##";

    private readonly List<BarElement> _bars = [];

    /// <summary>Gets the bars, top to bottom.</summary>
    public IReadOnlyList<BarElement> Bars => _bars;

    /// <summary>Gets or sets the value of a full bar, or <see langword="null"/> (the default) for the largest value.</summary>
    /// <example><code>chart.Max = 100; // percentages</code></example>
    public double? Max { get; set; }

    /// <summary>Gets or sets whether each bar's value is shown after it. Defaults to <see langword="true"/>.</summary>
    public bool ShowValues { get; set; } = true;

    /// <summary>Gets or sets the .NET format of the values, e.g. <c>N0</c> or <c>0.0</c>, in the current culture.</summary>
    public string Format { get; set; } = DefaultFormat;

    /// <summary>Gets or sets how the bars are drawn. Defaults to <see cref="BarChartStyle.Block"/>.</summary>
    public BarChartStyle Style { get; set; } = BarChartStyle.Block;

    /// <summary>Gets or sets the colour of the bars (each bar may have its own).</summary>
    public Colour Colour { get; set; }

    /// <summary>Gets or sets the colour of the labels and values.</summary>
    public Colour LabelColour { get; set; }

    /// <summary>
    /// Appends a bar.
    /// </summary>
    /// <param name="bar">The bar.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bar"/> is <see langword="null"/>.</exception>
    /// <example><code>chart.Add(new BarElement { Label = "Wed", Value = 1810 });</code></example>
    public void Add(BarElement bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        _bars.Add(bar);
    }

    /// <inheritdoc />
    /// <remarks>A chart fills the width it is given, so it asks for none of its own.</remarks>
    public override int MeasureWidth(int maxWidth) => 0;

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        if (_bars.Count == 0)
            return [];

        width = Math.Max(width, 0);
        var labels = _bars.Select(b => TextSanitiser.Sanitise(b.Label ?? "").Replace('\n', ' ')).ToArray();
        var values = ShowValues ? _bars.Select(b => FormatValue(b.Value)).ToArray() : null;

        var labelWidth = Math.Min(labels.Max(TextWidth.Of), width / 3);
        var valueWidth = values?.Max(TextWidth.Of) ?? 0;
        var area = Math.Max(width - (labelWidth > 0 ? labelWidth + 1 : 0) - (valueWidth > 0 ? valueWidth + 1 : 0), 1);

        var largest = Max ?? _bars.Select(b => b.Value).Where(double.IsFinite).DefaultIfEmpty(0).Max();
        var labelStyle = TextStyle.Plain.WithForeground(LabelColour);
        var lines = new List<RenderedLine>(_bars.Count);

        for (var i = 0; i < _bars.Count; i++)
        {
            var spans = new List<Span>();
            if (labelWidth > 0)
            {
                var label = TextWidth.Of(labels[i]) > labelWidth ? TextWidth.Truncate(labels[i], labelWidth) : labels[i];
                spans.Add(new Span(label + new string(' ', labelWidth - TextWidth.Of(label) + 1), labelStyle));
            }

            var bar = DrawBar(_bars[i].Value, largest, area);
            spans.Add(new Span(bar, TextStyle.Plain.WithForeground(_bars[i].Colour ?? Colour)));
            spans.Add(Span.Spaces(area - bar.Length));

            if (values is not null)
                spans.Add(new Span(" " + values[i].PadLeft(valueWidth), labelStyle));

            lines.Add(new RenderedLine(spans).Clip(width));
        }

        return lines;
    }

    /// <summary>Draws one bar of up to <paramref name="area"/> cells.</summary>
    private string DrawBar(double value, double largest, int area)
    {
        if (!double.IsFinite(value) || !double.IsFinite(largest) || largest <= 0 || value <= 0)
            return "";

        var exact = Math.Min(value / largest, 1) * area;
        if (!Style.Smooth)
            return new string(Style.Full, (int)Math.Round(exact, MidpointRounding.AwayFromZero));

        var full = (int)Math.Floor(exact);
        var eighths = (int)Math.Round((exact - full) * 8, MidpointRounding.AwayFromZero);
        if (eighths == 8)
        {
            full++;
            eighths = 0;
        }

        return new string(Style.Full, full) + (eighths > 0 ? BarChartStyle.Eighths[eighths].ToString() : "");
    }

    private string FormatValue(double value) =>
        TextSanitiser.Sanitise(value.ToString(string.IsNullOrEmpty(Format) ? DefaultFormat : Format, CultureInfo.CurrentCulture))
            .Replace('\n', ' ');
}
