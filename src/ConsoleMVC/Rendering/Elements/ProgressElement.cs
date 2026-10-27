namespace ConsoleMVC.Rendering;

/// <summary>
/// A progress bar on one line, e.g. <c>Import ████████░░░░░░░░  50%</c>: the <c>&lt;progress&gt;</c> markup element. Draw it
/// in a view to show how far something has got, or keep it up to date while an action runs with
/// <see cref="ILiveDisplay.Start"/>.
/// </summary>
/// <example>
/// <code>
/// var bar = new ProgressElement { Label = "Import ", Value = 3, Max = 6 };
/// renderer.Render(bar); // Import █████████████░░░░░░░░░░░░░  50%
/// </code>
/// </example>
public sealed class ProgressElement : Element
{
    /// <summary>The width of the percentage, including the space before it: <c>" 100%"</c>.</summary>
    public const int PercentWidth = 5;

    /// <summary>Gets or sets how much is done, from 0 to <see cref="Max"/>; values outside are clamped.</summary>
    /// <example><code>bar.Value = filesCopied;</code></example>
    public double Value { get; set; }

    /// <summary>Gets or sets the value at which the bar is full. Defaults to 100.</summary>
    /// <example><code>bar.Max = files.Count;</code></example>
    public double Max { get; set; } = 100;

    /// <summary>Gets or sets the text before the bar, e.g. <c>"Import "</c>. It is shortened when the line is too narrow.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the width of the bar itself, in columns. <see langword="null"/> (the default) fills the width left after
    /// the label and the percentage.
    /// </summary>
    /// <example><code>bar.BarWidth = 20;</code></example>
    public int? BarWidth { get; set; }

    /// <summary>Gets or sets whether the percentage is shown after the bar. Defaults to <see langword="true"/>.</summary>
    public bool ShowPercent { get; set; } = true;

    /// <summary>Gets or sets the characters of the bar. Defaults to <see cref="ProgressStyle.Block"/>.</summary>
    public ProgressStyle Style { get; set; } = ProgressStyle.Block;

    /// <summary>Gets or sets the colour of the done part of the bar.</summary>
    public Colour Colour { get; set; }

    /// <summary>Gets or sets the colour of the part still to do.</summary>
    public Colour EmptyColour { get; set; }

    /// <summary>Gets or sets the colour of the label and the percentage.</summary>
    public Colour LabelColour { get; set; }

    /// <summary>
    /// Gets how much is done, from 0 to 1: <see cref="Value"/> divided by <see cref="Max"/>, clamped; 0 when either is
    /// not a finite number or <see cref="Max"/> is not positive.
    /// </summary>
    /// <example><code>if (bar.Fraction >= 1) bar.Label = "Done ";</code></example>
    public double Fraction =>
        double.IsFinite(Value) && double.IsFinite(Max) && Max > 0 ? Math.Clamp(Value / Max, 0, 1) : 0;

    /// <inheritdoc />
    /// <remarks>A bar without a <see cref="BarWidth"/> fills its container, so it asks for no width of its own.</remarks>
    public override int MeasureWidth(int maxWidth)
    {
        if (BarWidth is not { } bar)
            return 0;

        var natural = TextWidth.Of(SafeLabel()) + Math.Max(bar, 1) + (ShowPercent ? PercentWidth : 0);
        return Math.Min(natural, Math.Max(maxWidth, 0));
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        width = Math.Max(width, 0);
        var fraction = Fraction;
        var percent = ShowPercent ? " " + Percent(fraction).PadLeft(PercentWidth - 1) : "";

        // The label gives way first, so the bar and the percentage stay readable on a narrow line
        var label = SafeLabel();
        var labelRoom = Math.Max(width - TextWidth.Of(percent) - Math.Max(BarWidth ?? 1, 1), 0);
        if (TextWidth.Of(label) > labelRoom)
            label = TextWidth.Truncate(label, labelRoom);

        var bar = Math.Max(BarWidth ?? width - TextWidth.Of(label) - TextWidth.Of(percent), 1);
        var filled = (int)Math.Round(fraction * bar, MidpointRounding.AwayFromZero);

        var labelStyle = TextStyle.Plain.WithForeground(LabelColour);
        var spans = new List<Span>();
        if (label.Length > 0)
            spans.Add(new Span(label, labelStyle));
        if (filled > 0)
            spans.Add(new Span(new string(Style.Filled, filled), TextStyle.Plain.WithForeground(Colour)));
        if (bar - filled > 0)
            spans.Add(new Span(new string(Style.Empty, bar - filled), TextStyle.Plain.WithForeground(EmptyColour)));
        if (percent.Length > 0)
            spans.Add(new Span(percent, labelStyle));

        return [new RenderedLine(spans).Clip(width)];
    }

    private string SafeLabel() => TextSanitiser.Sanitise(Label ?? "").Replace('\n', ' ');

    private static string Percent(double fraction) =>
        ((int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
}
