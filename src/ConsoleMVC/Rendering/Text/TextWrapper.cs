using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Splits text into lines that fit a given width, either by word wrapping or by truncation.
/// Hard line breaks (<c>\n</c>) always start a new line.
/// </summary>
/// <example>
/// <code>
/// var lines = TextWrapper.Wrap("The quick brown fox jumps", 10);
/// // ["The quick", "brown fox", "jumps"]
/// </code>
/// </example>
public static class TextWrapper
{
    /// <summary>
    /// Word-wraps <paramref name="text"/> to <paramref name="width"/> columns.
    /// </summary>
    /// <remarks>
    /// Lines break at spaces; the space at a break is dropped. Leading spaces of a paragraph are kept
    /// so indentation survives. Words longer than the width are broken across lines.
    /// </remarks>
    /// <param name="text">Sanitised text, possibly containing <c>\n</c> line breaks.</param>
    /// <param name="width">The maximum line width in columns (values below 1 are treated as 1).</param>
    /// <returns>The wrapped lines; at least one (possibly empty) line.</returns>
    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        width = Math.Max(width, 1);
        var lines = new List<string>();

        foreach (var paragraph in text.Split('\n'))
            WrapParagraph(paragraph, width, lines);

        return lines;
    }

    /// <summary>
    /// Truncates each paragraph of <paramref name="text"/> to <paramref name="width"/> columns,
    /// appending an ellipsis where text is cut.
    /// </summary>
    /// <param name="text">Sanitised text, possibly containing <c>\n</c> line breaks.</param>
    /// <param name="width">The maximum line width in columns.</param>
    /// <returns>One line per paragraph.</returns>
    public static IReadOnlyList<string> Truncate(string text, int width) =>
        text.Split('\n').Select(paragraph => TextWidth.Truncate(paragraph.TrimEnd(' '), width)).ToArray();

    /// <summary>
    /// Greedy word wrap of a single paragraph (no <c>\n</c>), appending the resulting lines to <paramref name="lines"/>.
    /// </summary>
    private static void WrapParagraph(string paragraph, int width, List<string> lines)
    {
        var line = new StringBuilder();
        var lineWidth = 0;
        var pendingSpaces = 0;
        var atParagraphStart = true;
        var position = 0;

        while (position < paragraph.Length)
        {
            if (paragraph[position] == ' ')
            {
                pendingSpaces++;
                position++;
                continue;
            }

            // Read the next word
            var wordEnd = paragraph.IndexOf(' ', position);
            if (wordEnd < 0)
                wordEnd = paragraph.Length;

            var word = paragraph[position..wordEnd];
            var wordWidth = TextWidth.Of(word);
            position = wordEnd;

            if (lineWidth + pendingSpaces + wordWidth <= width)
            {
                // The word (and the spaces before it) fit on the current line
                line.Append(' ', pendingSpaces).Append(word);
                lineWidth += pendingSpaces + wordWidth;
            }
            else
            {
                if (lineWidth > 0 || !atParagraphStart)
                {
                    // Start a new line; the spaces at the break are dropped
                    lines.Add(line.ToString());
                    line.Clear();
                    lineWidth = 0;
                }
                else
                {
                    // Keep paragraph indentation if it leaves room for at least one character
                    var indent = Math.Min(pendingSpaces, width - 1);
                    line.Append(' ', indent);
                    lineWidth = indent;
                }

                // Break words that do not fit on an empty line
                while (lineWidth + TextWidth.Of(word) > width)
                {
                    var chunk = TextWidth.TakeColumns(word, width - lineWidth, out var consumed);
                    if (consumed == 0)
                    {
                        // A double-width character that cannot fit in the remaining single column
                        if (lineWidth == 0)
                        {
                            chunk = word[..(char.IsSurrogatePair(word, 0) ? 2 : 1)];
                            consumed = chunk.Length;
                        }
                    }

                    line.Append(chunk);
                    lines.Add(line.ToString());
                    line.Clear();
                    lineWidth = 0;
                    word = word[consumed..];
                }

                line.Append(word);
                lineWidth += TextWidth.Of(word);
            }

            pendingSpaces = 0;
            atParagraphStart = false;
        }

        // Trailing spaces are dropped; an empty paragraph still produces a line
        lines.Add(line.ToString());
    }
}
