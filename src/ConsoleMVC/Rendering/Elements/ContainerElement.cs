namespace ConsoleMVC.Rendering;

/// <summary>
/// Base class for elements that contain other elements, stacked vertically in order.
/// </summary>
/// <example>
/// <code>
/// var box = new BoxElement();
/// box.Add(new TextElement());
/// box.Add(new SeparatorElement());
/// </code>
/// </example>
public abstract class ContainerElement : Element
{
    private readonly List<Element> _children = [];

    /// <summary>Gets the child elements in render order.</summary>
    public IReadOnlyList<Element> Children => _children;

    /// <summary>
    /// Appends a child element.
    /// </summary>
    /// <param name="child">The element to append.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="child"/> is this element.</exception>
    public void Add(Element child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (ReferenceEquals(child, this))
            throw new ArgumentException("An element cannot contain itself.", nameof(child));

        _children.Add(child);
    }

    /// <summary>
    /// Returns the widest preferred width among the children.
    /// </summary>
    /// <param name="maxWidth">The maximum width available to each child.</param>
    /// <returns>The widest child width, or 0 when there are no children.</returns>
    protected int MeasureChildren(int maxWidth) =>
        _children.Count == 0 ? 0 : _children.Max(c => c.MeasureWidth(maxWidth));

    /// <summary>
    /// Lays out every child at <paramref name="width"/> and concatenates their lines.
    /// </summary>
    /// <param name="width">The width given to each child.</param>
    /// <returns>All child lines, in order.</returns>
    protected List<RenderedLine> LayoutChildren(int width)
    {
        var lines = new List<RenderedLine>();
        foreach (var child in _children)
            lines.AddRange(child.Layout(width));

        return lines;
    }
}
