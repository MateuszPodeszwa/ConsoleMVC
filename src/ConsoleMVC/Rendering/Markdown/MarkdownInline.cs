using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Inline Markdown: <c>**bold**</c> and <c>__bold__</c>, <c>*italic*</c> and <c>_italic_</c> (nested), <c>`code`</c>,
/// <c>[text](address)</c>, <c>&lt;https://…&gt;</c> and backslash escapes. A delimiter that is never closed is kept as text.
/// </summary>
/// <example><code>MarkdownInline.Append("Use **bold**", TextStyle.Plain, element, styledText);</code></example>
internal static class MarkdownInline
{
    /// <summary>The deepest emphasis nesting followed; delimiters deeper than this are kept as text.</summary>
    private const int MaxDepth = 8;

    private const string Punctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    /// <summary>Steps of work allowed per character; past it, delimiters are kept as text, so any input stays fast.</summary>
    private const int WorkPerCharacter = 32;

    /// <summary>The text being parsed, where its delimiters can close, and how much work is left.</summary>
    private sealed class State(string text)
    {
        public string Text { get; } = text;

        public int Budget { get; set; } = WorkPerCharacter * text.Length + 1024;

        /// <summary>The last position at which each delimiter (<c>*</c>, <c>**</c>, <c>_</c>, <c>__</c>) can close.</summary>
        public Dictionary<string, int> LastCloser { get; } = new()
        {
            ["*"] = LastClose(text, "*"),
            ["**"] = LastClose(text, "**"),
            ["_"] = LastClose(text, "_"),
            ["__"] = LastClose(text, "__")
        };

        private static int LastClose(string text, string delimiter)
        {
            for (var i = text.Length - delimiter.Length; i > 0; i--)
            {
                if (CanClose(text, i, delimiter))
                    return i;
            }

            return -1;
        }
    }

    /// <summary>Formats <paramref name="text"/> in <paramref name="style"/> and appends it to <paramref name="output"/>.</summary>
    public static void Append(string text, TextStyle style, MarkdownElement options, StyledText output)
    {
        var runs = new List<(string Text, TextStyle Style)>();
        Parse(new State(text), 0, closer: null, style, options, runs, depth: 0, out _);
        foreach (var (run, runStyle) in runs)
            output.Append(run, runStyle);
    }

    /// <summary>
    /// Parses from <paramref name="index"/> until <paramref name="closer"/> (or the end), adding styled runs.
    /// </summary>
    /// <returns>The position after the closer, or the end of the text.</returns>
    private static int Parse(State state, int index, string? closer, TextStyle style, MarkdownElement options,
        List<(string, TextStyle)> output, int depth, out bool closed)
    {
        var s = state.Text;
        var buffer = new StringBuilder();

        void Flush()
        {
            if (buffer.Length > 0)
                output.Add((buffer.ToString(), style));
            buffer.Clear();
        }

        var i = index;
        while (i < s.Length)
        {
            state.Budget--;
            if (closer is not null && CanClose(s, i, closer))
            {
                Flush();
                closed = true;
                return i + closer.Length;
            }

            var c = s[i];

            if (c == '\\' && i + 1 < s.Length && Punctuation.Contains(s[i + 1]))
            {
                buffer.Append(s[i + 1]);
                i += 2;
                continue;
            }

            if (c == '`' && TryCode(s, i, out var code, out var afterCode))
            {
                Flush();
                output.Add((code, style.WithForeground(options.CodeColour)));
                i = afterCode;
                continue;
            }

            if (c == '[' && TryLink(s, i, out var label, out var address, out var afterLink))
            {
                Flush();
                var linkStyle = style.WithForeground(options.LinkColour).WithDecoration(TextDecoration.Underline);
                var labelRuns = new List<(string, TextStyle)>();
                Parse(new State(label), 0, closer: null, linkStyle, options, labelRuns, depth + 1, out _);
                output.AddRange(labelRuns);

                var shownLabel = string.Concat(labelRuns.Select(r => r.Item1));
                if (options.ShowLinkUrls && address.Length > 0 && address != shownLabel)
                    output.Add((" (" + address + ")", style.WithDecoration(TextDecoration.Dim)));

                i = afterLink;
                continue;
            }

            if (c == '<' && TryAutolink(s, i, out var autolink, out var afterAutolink))
            {
                Flush();
                output.Add((autolink, style.WithForeground(options.LinkColour).WithDecoration(TextDecoration.Underline)));
                i = afterAutolink;
                continue;
            }

            if (c is '*' or '_')
            {
                var delimiter = i + 1 < s.Length && s[i + 1] == c ? new string(c, 2) : c.ToString();

                // Only open when the delimiter can close further on and there is work left, so any input stays fast
                if (depth < MaxDepth && state.Budget > 0 && CanOpen(s, i, delimiter)
                    && state.LastCloser[delimiter] > i + delimiter.Length)
                {
                    var inner = new List<(string, TextStyle)>();
                    var emphasis = delimiter.Length == 2 ? TextDecoration.Bold : TextDecoration.Italic;
                    var after = Parse(state, i + delimiter.Length, delimiter, style.WithDecoration(emphasis), options, inner, depth + 1,
                        out var innerClosed);

                    if (innerClosed && inner.Count > 0)
                    {
                        Flush();
                        output.AddRange(inner);
                        i = after;
                        continue;
                    }
                }

                buffer.Append(delimiter);
                i += delimiter.Length;
                continue;
            }

            buffer.Append(c);
            i++;
        }

        Flush();
        closed = false;
        return i;
    }

    /// <summary>An opening delimiter is followed by text, and <c>_</c> does not open inside a word (<c>snake_case</c>).</summary>
    private static bool CanOpen(string s, int i, string delimiter)
    {
        var next = i + delimiter.Length;
        if (next >= s.Length || char.IsWhiteSpace(s[next]))
            return false;

        return delimiter[0] != '_' || i == 0 || !char.IsLetterOrDigit(s[i - 1]);
    }

    /// <summary>A closing delimiter follows text, and <c>_</c> does not close inside a word.</summary>
    private static bool CanClose(string s, int i, string delimiter)
    {
        if (string.CompareOrdinal(s, i, delimiter, 0, delimiter.Length) != 0 || i == 0 || char.IsWhiteSpace(s[i - 1]))
            return false;

        var after = i + delimiter.Length;
        return delimiter[0] != '_' || after >= s.Length || !char.IsLetterOrDigit(s[after]);
    }

    /// <summary>Reads <c>`code`</c> (or <c>``code with ` inside``</c>): the same number of backticks closes it.</summary>
    private static bool TryCode(string s, int i, out string code, out int after)
    {
        var ticks = 0;
        while (i + ticks < s.Length && s[i + ticks] == '`')
            ticks++;

        var fence = new string('`', ticks);
        var end = s.IndexOf(fence, i + ticks, StringComparison.Ordinal);
        code = "";
        after = i;
        if (end < 0)
            return false;

        code = s.Substring(i + ticks, end - i - ticks);
        if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
            code = code[1..^1];

        after = end + ticks;
        return true;
    }

    /// <summary>Reads <c>[label](address)</c>; the address has no spaces.</summary>
    private static bool TryLink(string s, int i, out string label, out string address, out int after)
    {
        label = address = "";
        after = i;

        var depth = 0;
        var close = -1;
        for (var j = i; j < s.Length; j++)
        {
            if (s[j] == '\\')
            {
                j++;
                continue;
            }

            if (s[j] == '[')
                depth++;
            else if (s[j] == ']' && --depth == 0)
            {
                close = j;
                break;
            }
        }

        if (close < 0 || close + 1 >= s.Length || s[close + 1] != '(')
            return false;

        var end = s.IndexOf(')', close + 2);
        if (end < 0)
            return false;

        var target = s.Substring(close + 2, end - close - 2).Trim();
        if (target.Any(char.IsWhiteSpace))
            return false;

        label = s.Substring(i + 1, close - i - 1);
        address = target;
        after = end + 1;
        return true;
    }

    /// <summary>Reads <c>&lt;https://…&gt;</c>, <c>&lt;http://…&gt;</c> or <c>&lt;mailto:…&gt;</c>.</summary>
    private static bool TryAutolink(string s, int i, out string address, out int after)
    {
        address = "";
        after = i;
        var end = s.IndexOf('>', i + 1);
        if (end < 0)
            return false;

        var candidate = s.Substring(i + 1, end - i - 1);
        var isLink = candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                     || candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || candidate.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
        if (!isLink || candidate.Any(char.IsWhiteSpace))
            return false;

        address = candidate;
        after = end + 1;
        return true;
    }
}
