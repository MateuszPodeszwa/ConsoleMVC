namespace ConsoleMVC.Rendering;

/// <summary>
/// Base class for every renderable markup element (<c>&lt;text&gt;</c>, <c>&lt;box&gt;</c>, …).
/// </summary>
/// <remarks>
/// <para>
/// Layout is a two-step, width-driven process. A parent first asks a child how wide it would like to be
/// (<see cref="MeasureWidth"/>), then gives it a concrete width and receives the finished lines
/// (<see cref="Layout"/>). Lines returned by <see cref="Layout"/> are never wider than the width given.
/// </para>
/// <para>
/// To add a new element type, derive from <see cref="Element"/> (or <see cref="ContainerElement"/>
/// for elements with children) and implement both methods. Elements are plain objects with no
/// dependency on the console, so they can be unit-tested by inspecting the returned lines.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class BannerElement : Element
/// {
///     public override int MeasureWidth(int maxWidth) => Math.Min(maxWidth, 10);
///     public override IReadOnlyList&lt;RenderedLine&gt; Layout(int width) =>
///         [new RenderedLine([new Span(new string('*', width), TextStyle.Plain)])];
/// }
/// </code>
/// </example>
public abstract class Element
{
    /// <summary>
    /// Returns the width, in columns, that this element would like to occupy, never exceeding
    /// <paramref name="maxWidth"/>. Elements that simply fill whatever space they are given return 0.
    /// </summary>
    /// <param name="maxWidth">The maximum width available.</param>
    /// <returns>The preferred width.</returns>
    public abstract int MeasureWidth(int maxWidth);

    /// <summary>
    /// Lays the element out at the given width and returns the resulting lines.
    /// </summary>
    /// <param name="width">The width available to the element, in columns.</param>
    /// <returns>The rendered lines, each at most <paramref name="width"/> columns wide.</returns>
    public abstract IReadOnlyList<RenderedLine> Layout(int width);

    /// <summary>
    /// Gets a value indicating whether this element's width is relative to its container (a percentage).
    /// An auto-sized container holding such an element fills the width available to it, so the percentage
    /// is applied to that width once rather than to a width already derived from the child.
    /// </summary>
    protected internal virtual bool HasRelativeWidth => false;
}
