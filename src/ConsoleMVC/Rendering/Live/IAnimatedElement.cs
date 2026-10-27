namespace ConsoleMVC.Rendering;

/// <summary>
/// An element that changes on its own over time, such as <see cref="SpinnerElement"/>. A <see cref="LiveRegion"/> calls
/// <see cref="Advance"/> on every animated element in its tree at each tick, then redraws.
/// </summary>
/// <example>
/// <code>
/// public sealed class BlinkElement : Element, IAnimatedElement
/// {
///     public bool On { get; private set; }
///     public void Advance() => On = !On;
///     public override int MeasureWidth(int maxWidth) => 1;
///     public override IReadOnlyList&lt;RenderedLine&gt; Layout(int width) =>
///         [new RenderedLine([new Span(On ? "*" : " ", TextStyle.Plain)])];
/// }
/// </code>
/// </example>
public interface IAnimatedElement
{
    /// <summary>Moves the element to its next frame.</summary>
    void Advance();
}
