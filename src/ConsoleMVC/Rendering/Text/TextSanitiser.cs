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
/// The sanitiser therefore removes all C0 and C1 control characters (including <c>ESC</c>) and
/// Unicode bidirectional overrides, keeps line feeds, converts <c>\r\n</c> and lone <c>\r</c>
/// to <c>\n</c>, and expands tabs to <see cref="TabWidth"/> spaces.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var safe = TextSanitiser.Sanitise(userInput); // "Bob\u001b[2J" → "Bob[2J"
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
                default:
                    if (!IsUnsafe(c))
                        builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
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
