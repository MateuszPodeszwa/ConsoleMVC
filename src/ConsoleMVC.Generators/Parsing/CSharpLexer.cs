#nullable enable

namespace ConsoleMVC.Generators.Parsing;

/// <summary>
/// What <see cref="CSharpLexer.Skip"/> found at a position.
/// </summary>
internal enum TriviaKind
{
    /// <summary>Not the start of a comment or literal.</summary>
    None,

    /// <summary>A <c>//</c> or <c>/* */</c> comment.</summary>
    Comment,

    /// <summary>A string or character literal (including verbatim, interpolated and raw strings).</summary>
    Literal
}

/// <summary>
/// The result of <see cref="CSharpLexer.Skip"/>.
/// </summary>
internal readonly struct SkipResult
{
    /// <summary>
    /// Creates a skip result.
    /// </summary>
    /// <param name="kind">What was found.</param>
    /// <param name="end">Offset just past the comment or literal.</param>
    /// <param name="unterminated"><see langword="true"/> when the construct runs to the end of the text or line.</param>
    /// <param name="description">A human-readable name of the construct, for diagnostics.</param>
    public SkipResult(TriviaKind kind, int end, bool unterminated = false, string description = "")
    {
        Kind = kind;
        End = end;
        Unterminated = unterminated;
        Description = description;
    }

    /// <summary>Gets what was found.</summary>
    public TriviaKind Kind { get; }

    /// <summary>Gets the offset just past the construct.</summary>
    public int End { get; }

    /// <summary>Gets a value indicating whether the construct was not closed.</summary>
    public bool Unterminated { get; }

    /// <summary>Gets a human-readable name of the construct (e.g. "string literal").</summary>
    public string Description { get; }

    /// <summary>A result meaning "nothing to skip".</summary>
    public static SkipResult None(int position) => new(TriviaKind.None, position);
}

/// <summary>
/// A minimal C# lexer that recognises just enough — comments, character literals and every
/// string form (regular, verbatim, interpolated with nested holes, raw) — for the markup parser
/// to scan C# code without mistaking text inside strings or comments for markup or braces.
/// </summary>
/// <example>
/// <code>
/// var result = CSharpLexer.Skip("\"&lt;text&gt;\" + x", 0); // Kind = Literal, End = 8
/// </code>
/// </example>
internal static class CSharpLexer
{
    /// <summary>
    /// If a comment or literal starts at <paramref name="position"/>, returns where it ends.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The offset to inspect.</param>
    /// <returns>The construct's kind and end offset, or <see cref="TriviaKind.None"/>.</returns>
    public static SkipResult Skip(string text, int position)
    {
        var c = text[position];
        var next = Peek(text, position + 1);

        switch (c)
        {
            case '/' when next == '/':
            {
                var end = text.IndexOf('\n', position);
                return new SkipResult(TriviaKind.Comment, end < 0 ? text.Length : end);
            }
            case '/' when next == '*':
            {
                var end = text.IndexOf("*/", position + 2, System.StringComparison.Ordinal);
                return end < 0
                    ? new SkipResult(TriviaKind.Comment, text.Length, unterminated: true, "comment")
                    : new SkipResult(TriviaKind.Comment, end + 2);
            }
            case '\'':
                return SkipCharacter(text, position);
            case '"':
                return SkipString(text, position, interpolated: false, verbatim: false);
            case '@' when next == '"':
                return SkipString(text, position + 1, interpolated: false, verbatim: true);
            case '@' when next == '$' && Peek(text, position + 2) == '"':
                return SkipString(text, position + 2, interpolated: true, verbatim: true);
            case '$':
            {
                var p = position;
                while (Peek(text, p) == '$')
                    p++;

                var verbatim = false;
                if (Peek(text, p) == '@')
                {
                    verbatim = true;
                    p++;
                }

                return Peek(text, p) == '"'
                    ? SkipString(text, p, interpolated: true, verbatim)
                    : SkipResult.None(position);
            }
            default:
                return SkipResult.None(position);
        }
    }

    /// <summary>
    /// Finds the bracket matching the one at <paramref name="openPosition"/>, skipping comments and literals.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="openPosition">Offset of the opening bracket.</param>
    /// <param name="open">The opening bracket character.</param>
    /// <param name="close">The closing bracket character.</param>
    /// <returns>The offset of the matching closing bracket, or -1 when there is none.</returns>
    public static int FindMatching(string text, int openPosition, char open, char close)
    {
        var depth = 0;
        var i = openPosition;

        while (i < text.Length)
        {
            var skip = Skip(text, i);
            if (skip.Kind != TriviaKind.None)
            {
                i = skip.End;
                continue;
            }

            var c = text[i];
            if (c == open)
            {
                depth++;
            }
            else if (c == close && --depth == 0)
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    private static SkipResult SkipCharacter(string text, int position)
    {
        var i = position + 1;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '\'')
                return new SkipResult(TriviaKind.Literal, i + 1);

            if (c == '\n')
                break;

            i++;
        }

        return new SkipResult(TriviaKind.Literal, i > text.Length ? text.Length : i, unterminated: true, "character literal");
    }

    /// <summary>
    /// Skips a string whose (first) opening quote is at <paramref name="quotePosition"/>.
    /// </summary>
    private static SkipResult SkipString(string text, int quotePosition, bool interpolated, bool verbatim)
    {
        var quotes = CountRun(text, quotePosition, '"');

        // Raw string literal: """ ... """ (cannot be verbatim)
        if (!verbatim && quotes >= 3)
        {
            var closing = new string('"', quotes);
            var end = text.IndexOf(closing, quotePosition + quotes, System.StringComparison.Ordinal);
            return end < 0
                ? new SkipResult(TriviaKind.Literal, text.Length, unterminated: true, "raw string literal")
                : new SkipResult(TriviaKind.Literal, end + quotes);
        }

        var i = quotePosition + 1;
        while (i < text.Length)
        {
            var c = text[i];

            if (!verbatim && c == '\\')
            {
                i += 2;
                continue;
            }

            if (!verbatim && c == '\n')
                break;

            if (c == '"')
            {
                if (verbatim && Peek(text, i + 1) == '"')
                {
                    i += 2;
                    continue;
                }

                return new SkipResult(TriviaKind.Literal, i + 1);
            }

            if (interpolated && c == '{')
            {
                if (Peek(text, i + 1) == '{')
                {
                    i += 2;
                    continue;
                }

                i = SkipInterpolationHole(text, i + 1);
                continue;
            }

            i++;
        }

        return new SkipResult(TriviaKind.Literal, i > text.Length ? text.Length : i, unterminated: true, "string literal");
    }

    /// <summary>
    /// Skips the code of an interpolation hole starting after its <c>{</c>; returns the offset after the closing <c>}</c>.
    /// </summary>
    private static int SkipInterpolationHole(string text, int start)
    {
        var depth = 0;
        var i = start;

        while (i < text.Length)
        {
            var skip = Skip(text, i);
            if (skip.Kind != TriviaKind.None)
            {
                i = skip.End;
                continue;
            }

            var c = text[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                if (depth == 0)
                    return i + 1;

                depth--;
            }

            i++;
        }

        return text.Length;
    }

    private static int CountRun(string text, int position, char c)
    {
        var count = 0;
        while (Peek(text, position + count) == c)
            count++;

        return count;
    }

    private static char Peek(string text, int position) =>
        position >= 0 && position < text.Length ? text[position] : '\0';
}
