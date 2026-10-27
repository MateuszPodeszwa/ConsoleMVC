#nullable enable

using System.Collections.Generic;

namespace ConsoleMVC.Generators.Parsing;

/// <summary>
/// Converts character offsets into zero-based line and column numbers.
/// </summary>
/// <example>
/// <code>
/// var map = new LineMap("ab\ncd");
/// var (line, column) = map.GetPosition(4); // (1, 1)
/// </code>
/// </example>
internal sealed class LineMap
{
    private readonly List<int> _lineStarts = new() { 0 };

    /// <summary>
    /// Builds the map for <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The source text. Lines end with <c>\n</c> (a preceding <c>\r</c> is part of the line).</param>
    public LineMap(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                _lineStarts.Add(i + 1);
        }
    }

    /// <summary>
    /// Returns the zero-based line and column of <paramref name="offset"/>.
    /// </summary>
    /// <param name="offset">A character offset into the text.</param>
    /// <returns>The line and column.</returns>
    public (int Line, int Column) GetPosition(int offset)
    {
        int low = 0, high = _lineStarts.Count - 1;

        while (low < high)
        {
            var mid = (low + high + 1) >> 1;
            if (_lineStarts[mid] <= offset)
                low = mid;
            else
                high = mid - 1;
        }

        return (low, offset - _lineStarts[low]);
    }
}
