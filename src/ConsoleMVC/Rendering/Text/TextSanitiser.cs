using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Removes characters that could manipulate the terminal from text before it is rendered.
/// </summary>
/// <remarks>
/// <para>
/// Model data often comes from users, files or networks. Without sanitisation a value such as
/// <c>"\u001b[2J"</c> would clear the screen, and other escape sequences can recolour the terminal,
/// move the cursor, rewrite the window title or, on some terminals, inject input. Bidirectional
/// override characters can visually reorder text to disguise its meaning.
/// </para>
/// <para>
/// The sanitiser therefore removes whole escape sequences (<c>ESC [ … J</c>, <c>ESC ] … BEL</c> and the like, so none
/// of their text is left behind), all other C0 and C1 control characters and Unicode bidirectional overrides, keeps
/// line feeds, converts <c>\r\n</c> and lone <c>\r</c> to <c>\n</c>, and expands tabs to <see cref="TabWidth"/>
/// spaces.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var safe = TextSanitiser.Sanitise(userInput); // "Bob\u001b[2J" → "Bob"
/// </code>
/// </example>
public static class TextSanitiser
{
    /// <summary>The number of spaces a tab character expands to.</summary>
    public const int TabWidth = 4;

    /// <summary>
    /// Returns a copy of <paramref name="text"/> that is safe to write to a terminal.
    /// </summary>
    /// <param name="text">The untrusted text.</param>
    /// <returns>The sanitised text; an empty string when <paramref name="text"/> is <see langword="null"/>.</returns>
    public static string Sanitise(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        if (!NeedsSanitising(text))
            return text;

        var builder = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            switch (c)
            {
                case '\n':
                    builder.Append('\n');
                    break;
                case '\r':
                    // \r\n → \n, lone \r → \n
                    builder.Append('\n');
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    break;
                case '\t':
                    builder.Append(' ', TabWidth);
                    break;
                case '\u001b' or '\u009b' or '\u009d' or '\u0090' or '\u0098' or '\u009e' or '\u009f':
                    i = EndOfSequence(text, i);
                    break;
                default:
                    if (!IsUnsafe(c))
                        builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>The longest control sequence skipped; past it the rest is treated as text (still with controls removed).</summary>
    private const int MaxSequenceLength = 512;

    /// <summary>
    /// Returns the index of the last character of the escape sequence starting at <paramref name="start"/> (an
    /// <c>ESC</c> or a C1 introducer): a CSI (<c>ESC [</c>, parameters, intermediates and a final byte), a string
    /// sequence (<c>ESC ]</c> OSC, <c>ESC P</c> DCS, <c>ESC X</c>, <c>ESC ^</c>, <c>ESC _</c>) up to its terminator (BEL or
    /// <c>ESC \</c>), or a two-character sequence (<c>ESC c</c>). A sequence that is not finished within
    /// <see cref="MaxSequenceLength"/> characters ends at its introducer, so text after it is not swallowed.
    /// </summary>
    private static int EndOfSequence(string text, int start)
    {
        var i = start;
        var kind = text[i];
        if (kind == '\u001b')
        {
            if (i + 1 >= text.Length)
                return i;

            kind = text[i + 1] switch
            {
                '[' => '\u009b',
                ']' => '\u009d',
                'P' => '\u0090',
                'X' => '\u0098',
                '^' => '\u009e',
                '_' => '\u009f',
                _ => '\0'
            };

            i++;
            if (kind == '\0')
            {
                // ESC, intermediates (0x20-0x2F), then one final character (0x30-0x7E)
                while (i < text.Length && text[i] is >= ' ' and <= '/' && i - start < MaxSequenceLength)
                    i++;
                return i < text.Length && text[i] is >= '0' and <= '~' ? i : i - 1;
            }
        }

        var limit = Math.Min(text.Length, start + MaxSequenceLength);
        if (kind == '\u009b')
        {
            // CSI: parameters (0x30-0x3F), intermediates (0x20-0x2F), then a final byte (0x40-0x7E)
            var j = i + 1;
            while (j < limit && text[j] is >= '0' and <= '?')
                j++;
            while (j < limit && text[j] is >= ' ' and <= '/')
                j++;
            return j < limit && text[j] is >= '@' and <= '~' ? j : i;
        }

        // A string sequence: up to BEL, ST (0x9C) or ESC \
        for (var j = i + 1; j < limit; j++)
        {
            if (text[j] is '\u0007' or '\u009c')
                return j;
            if (text[j] == '\u001b' && j + 1 < text.Length && text[j + 1] == '\\')
                return j + 1;
        }

        return i;
    }

    /// <summary>Fast check so that the common case (already-safe text) allocates nothing.</summary>
    private static bool NeedsSanitising(string text)
    {
        foreach (var c in text)
        {
            if (c is '\r' or '\t' || (c != '\n' && IsUnsafe(c)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> for C0/C1 controls, DEL, and bidirectional embedding,
    /// override and isolate characters.
    /// </summary>
    private static bool IsUnsafe(char c) =>
        c < 0x20
        || c is >= '\u007F' and <= '\u009F'
        || c is >= '‪' and <= '‮'
        || c is >= '⁦' and <= '⁩';
}
