using System.Globalization;
using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Measures how many terminal cells (columns) text occupies. East Asian wide characters and most
/// emoji occupy two cells; combining marks and format characters occupy none.
/// </summary>
/// <remarks>
/// The wide-character ranges are adapted from Markus Kuhn's public-domain <c>wcwidth</c>
/// implementation (https://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c), extended with the
/// emoji blocks that modern terminals render at double width.
/// </remarks>
/// <example>
/// <code>
/// int a = TextWidth.Of("Hello"); // 5
/// int b = TextWidth.Of("日本");   // 4
/// string c = TextWidth.Truncate("Hello world", 5); // "Hell…"
/// </code>
/// </example>
public static class TextWidth
{
    /// <summary>The character appended by <see cref="Truncate"/> when text is shortened.</summary>
    public const string Ellipsis = "…";

    /// <summary>
    /// Inclusive code point ranges rendered at double width, sorted ascending for binary search.
    /// </summary>
    private static readonly (int Start, int End)[] WideRanges =
    [
        (0x1100, 0x115F),   // Hangul Jamo initial consonants
        (0x231A, 0x231B),   // Watch, hourglass
        (0x2329, 0x232A),   // Angle brackets
        (0x23E9, 0x23EC),   // Media control symbols
        (0x23F0, 0x23F0),
        (0x23F3, 0x23F3),
        (0x25FD, 0x25FE),
        (0x2614, 0x2615),
        (0x2648, 0x2653),   // Zodiac
        (0x267F, 0x267F),
        (0x2693, 0x2693),
        (0x26A1, 0x26A1),
        (0x26AA, 0x26AB),
        (0x26BD, 0x26BE),
        (0x26C4, 0x26C5),
        (0x26CE, 0x26CE),
        (0x26D4, 0x26D4),
        (0x26EA, 0x26EA),
        (0x26F2, 0x26F3),
        (0x26F5, 0x26F5),
        (0x26FA, 0x26FA),
        (0x26FD, 0x26FD),
        (0x2705, 0x2705),
        (0x270A, 0x270B),
        (0x2728, 0x2728),
        (0x274C, 0x274C),
        (0x274E, 0x274E),
        (0x2753, 0x2755),
        (0x2757, 0x2757),
        (0x2795, 0x2797),
        (0x27B0, 0x27B0),
        (0x27BF, 0x27BF),
        (0x2B1B, 0x2B1C),
        (0x2B50, 0x2B50),
        (0x2B55, 0x2B55),
        (0x2E80, 0x303E),   // CJK radicals, punctuation
        (0x3041, 0x33FF),   // Hiragana, Katakana, CJK compatibility
        (0x3400, 0x4DBF),   // CJK extension A
        (0x4E00, 0x9FFF),   // CJK unified ideographs
        (0xA000, 0xA4CF),   // Yi
        (0xA960, 0xA97F),   // Hangul Jamo extended A
        (0xAC00, 0xD7A3),   // Hangul syllables
        (0xF900, 0xFAFF),   // CJK compatibility ideographs
        (0xFE10, 0xFE19),   // Vertical forms
        (0xFE30, 0xFE6F),   // CJK compatibility forms
        (0xFF00, 0xFF60),   // Fullwidth forms
        (0xFFE0, 0xFFE6),
        (0x16FE0, 0x16FE4),
        (0x17000, 0x18AFF), // Tangut
        (0x1B000, 0x1B2FF), // Kana supplement
        (0x1F004, 0x1F004),
        (0x1F0CF, 0x1F0CF),
        (0x1F18E, 0x1F18E),
        (0x1F191, 0x1F19A),
        (0x1F200, 0x1F251),
        (0x1F300, 0x1F64F), // Misc symbols and pictographs, emoticons
        (0x1F680, 0x1F6FF), // Transport and map symbols
        (0x1F7E0, 0x1F7EB),
        (0x1F90C, 0x1F9FF), // Supplemental symbols and pictographs
        (0x1FA70, 0x1FAFF), // Symbols and pictographs extended A
        (0x20000, 0x2FFFD), // CJK extensions B–F
        (0x30000, 0x3FFFD)  // CJK extension G
    ];

    /// <summary>
    /// Returns the number of terminal cells occupied by <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to measure. Should already be sanitised (see <see cref="TextSanitiser"/>).</param>
    /// <returns>The display width in columns.</returns>
    public static int Of(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var width = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
            width += OfElement(elements.GetTextElement());

        return width;
    }

    /// <summary>
    /// Returns the number of terminal columns one user-perceived character (an extended grapheme cluster) occupies.
    /// </summary>
    /// <remarks>
    /// A cluster is drawn as one glyph, so only its first character counts, with one exception: the emoji
    /// presentation selector <c>U+FE0F</c> turns a narrow symbol such as ❤ into a two-column emoji (❤️). Emoji joined
    /// with zero-width joiners (👨‍👩‍👧), skin tones (👍🏽), flags (🇬🇧) and combining accents therefore all measure as one character.
    /// </remarks>
    /// <param name="element">One text element, e.g. from <see cref="StringInfo.GetTextElementEnumerator(string)"/>.</param>
    /// <returns>0, 1 or 2.</returns>
    /// <example><code>TextWidth.OfElement("👍🏽"); // 2</code></example>
    public static int OfElement(string element)
    {
        if (string.IsNullOrEmpty(element))
            return 0;

        var first = Rune.GetRuneAt(element, 0);
        var width = Of(first);

        // Regional indicator pairs (flags) are one cluster; each indicator alone is narrow
        if (first.Value is >= 0x1F1E6 and <= 0x1F1FF && element.Length > first.Utf16SequenceLength)
            return 2;

        if (width == 1 && element.Contains('\uFE0F'))
            return 2;

        return width;
    }

    /// <summary>
    /// Returns the number of terminal cells occupied by a single Unicode scalar value (0, 1 or 2).
    /// </summary>
    /// <param name="rune">The scalar value to measure.</param>
    /// <returns>The display width in columns.</returns>
    public static int Of(Rune rune)
    {
        var value = rune.Value;

        // Fast path for printable ASCII
        if (value is >= 0x20 and < 0x7F)
            return 1;

        if (value < 0x20 || value is >= 0x7F and < 0xA0)
            return 0;

        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
            return 0;

        // Zero width space and variation selectors
        if (value is 0x200B or >= 0xFE00 and <= 0xFE0F)
            return 0;

        return IsWide(value) ? 2 : 1;
    }

    /// <summary>
    /// Shortens <paramref name="text"/> so that it fits in <paramref name="width"/> columns,
    /// replacing the removed part with <see cref="Ellipsis"/>. Text that already fits is returned unchanged.
    /// </summary>
    /// <param name="text">The text to shorten.</param>
    /// <param name="width">The maximum width in columns.</param>
    /// <returns>Text whose display width is at most <paramref name="width"/>.</returns>
    public static string Truncate(string text, int width)
    {
        if (width <= 0)
            return "";

        if (Of(text) <= width)
            return text;

        // Leave one column for the ellipsis
        return TakeColumns(text, width - 1, out _) + Ellipsis;
    }

    /// <summary>
    /// Returns the longest prefix of <paramref name="text"/> that fits in <paramref name="columns"/>.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="columns">The maximum width of the prefix.</param>
    /// <param name="consumed">The number of UTF-16 code units in the returned prefix.</param>
    /// <returns>The prefix.</returns>
    internal static string TakeColumns(string text, int columns, out int consumed)
    {
        var width = 0;
        consumed = 0;

        // Whole clusters only, so an emoji sequence or an accented letter is never split
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var elementWidth = OfElement(element);
            if (width + elementWidth > columns)
                break;

            width += elementWidth;
            consumed += element.Length;
        }

        return text[..consumed];
    }

    /// <summary>Binary search of <see cref="WideRanges"/>.</summary>
    private static bool IsWide(int value)
    {
        int low = 0, high = WideRanges.Length - 1;

        while (low <= high)
        {
            var mid = (low + high) >> 1;
            var (start, end) = WideRanges[mid];

            if (value < start)
                high = mid - 1;
            else if (value > end)
                low = mid + 1;
            else
                return true;
        }

        return false;
    }
}
