namespace ConsoleMVC.Rendering;

/// <summary>
/// A named set of style properties in a <see cref="Theme"/>. Only the properties that are set are applied, so a style
/// can change just a colour and leave everything else alone.
/// </summary>
/// <remarks>
/// Text properties (<see cref="Foreground"/>, <see cref="Bold"/>, …) apply to <c>&lt;text&gt;</c>, <c>&lt;cell&gt;</c> and
/// <c>&lt;option&gt;</c>; <see cref="Foreground"/> is also the colour of <c>&lt;separator&gt;</c> and <c>&lt;errors&gt;</c>.
/// Box properties (<see cref="Border"/>, <see cref="Padding"/>, widths, …) apply to <c>&lt;box&gt;</c> and
/// <c>&lt;table&gt;</c>.
/// </remarks>
/// <example>
/// <code>
/// var brand = new ThemeStyle { Foreground = Colour.Magenta, Bold = true };
/// var card = new ThemeStyle { Border = BorderStyle.Rounded, BorderColour = Colour.Cyan, Padding = 2 };
/// </code>
/// </example>
public sealed record ThemeStyle
{
    /// <summary>Gets the text colour (also the colour of separators and validation messages).</summary>
    public Colour? Foreground { get; init; }

    /// <summary>Gets the text background colour.</summary>
    public Colour? Background { get; init; }

    /// <summary>Gets whether text is bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Gets whether text is dim.</summary>
    public bool? Dim { get; init; }

    /// <summary>Gets whether text is italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>Gets whether text is underlined.</summary>
    public bool? Underline { get; init; }

    /// <summary>Gets the text alignment.</summary>
    public Alignment? Align { get; init; }

    /// <summary>Gets the border of boxes and tables.</summary>
    public BorderStyle? Border { get; init; }

    /// <summary>Gets the border colour of boxes and tables.</summary>
    public Colour? BorderColour { get; init; }

    /// <summary>Gets the title colour of boxes.</summary>
    public Colour? TitleColour { get; init; }

    /// <summary>Gets the padding of boxes and table cells.</summary>
    public int? Padding { get; init; }

    /// <summary>Gets the width of boxes and tables.</summary>
    public Length? Width { get; init; }

    /// <summary>Gets the minimum width of boxes and tables.</summary>
    public int? MinWidth { get; init; }

    /// <summary>Gets the maximum width of boxes and tables.</summary>
    public int? MaxWidth { get; init; }

    /// <summary>
    /// Applies the properties that are set to <paramref name="element"/>; properties the element does not have are ignored.
    /// </summary>
    /// <param name="element">The element to style.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is <see langword="null"/>.</exception>
    /// <example><code>new ThemeStyle { Bold = true }.ApplyTo(text);</code></example>
    public void ApplyTo(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        switch (element)
        {
            case InlineElement inline:
                if (Foreground is { } foreground) inline.Foreground = foreground;
                if (Background is { } background) inline.Background = background;
                if (Bold is { } bold) inline.Bold = bold;
                if (Dim is { } dim) inline.Dim = dim;
                if (Italic is { } italic) inline.Italic = italic;
                if (Underline is { } underline) inline.Underline = underline;
                if (Align is { } align) inline.Align = align;
                break;

            case BoxElement box:
                if (Border is { } boxBorder) box.Border = boxBorder;
                if (BorderColour is { } boxBorderColour) box.BorderColour = boxBorderColour;
                if (TitleColour is { } titleColour) box.TitleColour = titleColour;
                if (Padding is { } boxPadding) box.Padding = boxPadding;
                if (Width is { } boxWidth) box.Width = boxWidth;
                if (MinWidth is { } boxMin) box.MinWidth = boxMin;
                if (MaxWidth is { } boxMax) box.MaxWidth = boxMax;
                break;

            case TableElement table:
                if (Border is { } tableBorder) table.Border = tableBorder;
                if (BorderColour is { } tableBorderColour) table.BorderColour = tableBorderColour;
                if (Padding is { } tablePadding) table.Padding = tablePadding;
                if (Width is { } tableWidth) table.Width = tableWidth;
                if (MinWidth is { } tableMin) table.MinWidth = tableMin;
                if (MaxWidth is { } tableMax) table.MaxWidth = tableMax;
                break;

            case SeparatorElement separator:
                if (Foreground is { } separatorColour) separator.Colour = separatorColour;
                break;

            case ValidationErrorsElement errors:
                if (Foreground is { } errorsColour) errors.Colour = errorsColour;
                break;

            case BreadcrumbsElement breadcrumbs:
                if (Foreground is { } breadcrumbsColour) breadcrumbs.Colour = breadcrumbsColour;
                break;

            case ShortcutsElement shortcuts:
                if (Foreground is { } shortcutsColour) shortcuts.Colour = shortcutsColour;
                break;

            case ProgressElement progress:
                if (Foreground is { } progressColour) progress.Colour = progressColour;
                break;

            case SpinnerElement spinner:
                if (Foreground is { } spinnerColour) spinner.Colour = spinnerColour;
                break;

            case BarChartElement chart:
                if (Foreground is { } chartColour) chart.Colour = chartColour;
                break;

            case MarkdownElement markdown:
                if (Foreground is { } headingColour) markdown.HeadingColour = headingColour;
                break;

            case SparklineElement sparkline:
                if (Foreground is { } sparklineColour) sparkline.Colour = sparklineColour;
                break;
        }
    }
}
