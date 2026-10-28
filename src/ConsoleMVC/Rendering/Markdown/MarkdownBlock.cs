namespace ConsoleMVC.Rendering;

/// <summary>The kinds of block <see cref="MarkdownParser"/> recognises.</summary>
internal enum MarkdownBlockKind
{
    Heading,
    Paragraph,
    ListItem,
    Quote,
    Code,
    Rule
}

/// <summary>
/// One block of a Markdown document.
/// </summary>
/// <param name="Kind">What the block is.</param>
/// <param name="Text">The inline text (headings, paragraphs, list items, quotes) or the lines of a code block.</param>
/// <param name="Level">The heading level (1 to 6) or the nesting depth of a list item (0 for the outermost).</param>
/// <param name="Marker">A list item's marker: <c>•</c> or its number such as <c>2.</c>.</param>
/// <param name="ContinuesList">Whether a list item follows another item of the same list (no blank line between them).</param>
internal sealed record MarkdownBlock(MarkdownBlockKind Kind, string Text, int Level = 0, string Marker = "", bool ContinuesList = false);
