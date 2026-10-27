#nullable enable

namespace ConsoleMVC.Generators.Syntax;

/// <summary>
/// A range of characters in a <c>.cvw</c> file, expressed as an absolute offset and a length.
/// </summary>
/// <example><code>var span = new SourceSpan(start: 10, length: 4); // characters 10..13</code></example>
internal readonly struct SourceSpan
{
    /// <summary>
    /// Creates a span.
    /// </summary>
    /// <param name="start">Offset of the first character.</param>
    /// <param name="length">Number of characters (zero for an empty span).</param>
    public SourceSpan(int start, int length)
    {
        Start = start;
        Length = length < 0 ? 0 : length;
    }

    /// <summary>Gets the offset of the first character.</summary>
    public int Start { get; }

    /// <summary>Gets the number of characters.</summary>
    public int Length { get; }

    /// <summary>Gets the offset just past the last character.</summary>
    public int End => Start + Length;

    /// <summary>
    /// Creates a span from a start and an exclusive end offset.
    /// </summary>
    /// <param name="start">Offset of the first character.</param>
    /// <param name="end">Offset just past the last character.</param>
    /// <returns>The span.</returns>
    public static SourceSpan FromBounds(int start, int end) => new(start, end - start);

    /// <inheritdoc />
    public override string ToString() => $"[{Start}..{End})";
}
