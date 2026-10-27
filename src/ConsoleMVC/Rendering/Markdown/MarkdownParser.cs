using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Splits Markdown into blocks: ATX headings (<c># Title</c>), paragraphs, lists (<c>-</c>, <c>*</c>, <c>+</c> or
/// <c>1.</c>, nested by indentation), block quotes (<c>&gt;</c>), fenced code (<c>```</c> or <c>~~~</c>) and rules
/// (<c>---</c>). Inline formatting is left to <see cref="MarkdownInline"/>.
/// </summary>
/// <example><code>var blocks = MarkdownParser.Parse("# Title\n\nSome *text*.");</code></example>
internal static partial class MarkdownParser
{
    /// <summary>The deepest list nesting kept; deeper items are drawn at this depth.</summary>
    public const int MaxListDepth = 6;

    [GeneratedRegex(@"^(#{1,6})(?:[ \t]+(.*?))?[ \t]*#*[ \t]*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^( *)([-*+]|\d{1,9}[.)])[ \t]+(.*)$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})")]
    private static partial Regex Fence();

    /// <summary>Parses <paramref name="markdown"/> into blocks.</summary>
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ").Split('\n');
        var blocks = new List<MarkdownBlock>();
        var paragraph = new StringBuilder();
        var quote = new StringBuilder();
        MarkdownBlock? item = null;
        var itemText = new StringBuilder();
        var inList = false;

        void FlushParagraph()
        {
            if (paragraph.Length > 0)
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, paragraph.ToString().TrimEnd('\n')));
            paragraph.Clear();
        }

        void FlushQuote()
        {
            if (quote.Length > 0)
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Quote, quote.ToString().TrimEnd('\n')));
            quote.Clear();
        }

        void FlushItem()
        {
            if (item is not null)
                blocks.Add(item with { Text = itemText.ToString().TrimEnd('\n') });
            item = null;
            itemText.Clear();
        }

        void FlushAll(bool endList)
        {
            FlushParagraph();
            FlushQuote();
            FlushItem();
            if (endList)
                inList = false;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushAll(endList: true);
                continue;
            }

            var fence = Fence().Match(line);
            if (fence.Success)
            {
                FlushAll(endList: true);
                var marker = fence.Groups[1].Value;
                var code = new List<string>();
                for (index++; index < lines.Length; index++)
                {
                    if (lines[index].TrimStart().StartsWith(marker, StringComparison.Ordinal) && lines[index].Trim().Trim(marker[0]).Length == 0)
                        break;
                    code.Add(lines[index]);
                }

                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, string.Join('\n', code)));
                continue;
            }

            var heading = Heading().Match(trimmed);
            if (heading.Success && line.Length - line.TrimStart().Length <= 3)
            {
                FlushAll(endList: true);
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Heading, heading.Groups[2].Value, heading.Groups[1].Length));
                continue;
            }

            if (Rule().IsMatch(line))
            {
                FlushAll(endList: true);
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Rule, ""));
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                FlushItem();
                inList = false;
                var content = trimmed[1..];
                if (content.StartsWith(' '))
                    content = content[1..];

                if (content.Trim().Length == 0)
                    quote.Append('\n'); // a blank quoted line starts a new paragraph in the quote
                else
                    AppendLine(quote, content);
                continue;
            }

            var listItem = ListItem().Match(line);
            if (listItem.Success)
            {
                FlushParagraph();
                FlushQuote();
                FlushItem();
                var depth = Math.Min(listItem.Groups[1].Length / 2, MaxListDepth);
                var bullet = listItem.Groups[2].Value;
                var marker = char.IsAsciiDigit(bullet[0]) ? bullet.TrimEnd(')', '.') + "." : "•";
                item = new MarkdownBlock(MarkdownBlockKind.ListItem, "", depth, marker, ContinuesList: inList);
                AppendLine(itemText, listItem.Groups[3].Value);
                inList = true;
                continue;
            }

            // A plain line continues whatever is open: a list item, a quote or a paragraph
            if (item is not null)
                AppendLine(itemText, trimmed);
            else if (quote.Length > 0)
                AppendLine(quote, trimmed);
            else
                AppendLine(paragraph, line.TrimStart());
        }

        FlushAll(endList: true);
        return blocks;
    }

    /// <summary>
    /// Appends a source line to a block's text: a line ending in two spaces or a backslash breaks the line (<c>\n</c>),
    /// otherwise lines are joined with a space.
    /// </summary>
    private static void AppendLine(StringBuilder text, string line)
    {
        if (text.Length > 0 && text[^1] != '\n')
            text.Append(' ');

        if (line.EndsWith("  ", StringComparison.Ordinal))
            text.Append(line.TrimEnd()).Append('\n');
        else if (line.EndsWith('\\') && !line.EndsWith("\\\\", StringComparison.Ordinal))
            text.Append(line[..^1].TrimEnd()).Append('\n');
        else
            text.Append(line.TrimEnd());
    }
}
