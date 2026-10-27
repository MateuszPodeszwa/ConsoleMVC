namespace ConsoleMVC.Rendering;

/// <summary>
/// One line showing where the user is: <c>Home › Orders › Details</c>, the <c>&lt;breadcrumbs /&gt;</c> markup element.
/// When the trail is wider than the space, the oldest items are replaced by <c>…</c>, so the current screen stays visible.
/// </summary>
/// <remarks>
/// The element shows any list of labels, so the rendering engine stays independent of MVC; in views the generator
/// connects it to the view's <c>Breadcrumbs</c>. Renders nothing when the list is empty.
/// </remarks>
/// <example>
/// <code>
/// var crumbs = new BreadcrumbsElement { Items = ["Home", "Orders", "Order 42"] };
/// // Home › Orders › Order 42
/// </code>
/// </example>
public sealed class BreadcrumbsElement : Element
{
    /// <summary>The marker put in place of the items that do not fit.</summary>
    public const string Overflow = "…";

    /// <summary>Gets or sets the labels, oldest first; the last one is the current screen.</summary>
    public IReadOnlyList<string> Items { get; set; } = [];

    /// <summary>Gets or sets the text between items. Defaults to <c>" › "</c>.</summary>
    public string Separator { get; set; } = " › ";

    /// <summary>Gets or sets the colour of the earlier items and separators. The current item is bold.</summary>
    public Colour Colour { get; set; }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) =>
        Math.Min(Items.Count == 0 ? 0 : TextWidth.Of(Join(Clean(Items))), Math.Max(maxWidth, 0));

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var available = Math.Max(width, 0);
        var items = Clean(Items);
        if (items.Count == 0 || available == 0)
            return [];

        // Drop the oldest items until the trail fits, keeping at least the current one
        var start = 0;
        while (start < items.Count - 1 && TextWidth.Of(Join(Visible(items, start))) > available)
            start++;

        var shown = Visible(items, start);
        var muted = TextStyle.Plain.WithForeground(Colour);
        var spans = new List<Span>();
        for (var i = 0; i < shown.Count; i++)
        {
            if (i > 0)
                spans.Add(new Span(SafeSeparator, muted));

            var current = i == shown.Count - 1;
            spans.Add(new Span(shown[i], current ? muted.WithDecoration(TextDecoration.Bold) : muted));
        }

        return [new RenderedLine(spans).Clip(available)];
    }

    private string SafeSeparator => TextSanitiser.Sanitise(Separator).Replace('\n', ' ');

    private string Join(IReadOnlyList<string> items) => string.Join(SafeSeparator, items);

    private static List<string> Clean(IReadOnlyList<string> items) =>
        items.Select(i => TextSanitiser.Sanitise(i).Replace('\n', ' ')).Where(i => i.Length > 0).ToList();

    private static List<string> Visible(List<string> items, int start) =>
        start == 0 ? items : [Overflow, .. items.Skip(start)];
}
