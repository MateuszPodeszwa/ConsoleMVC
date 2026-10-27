namespace ConsoleMVC.Rendering;

/// <summary>
/// A block of styled text — the <c>&lt;text&gt;</c> markup element. Text is word-wrapped (or truncated)
/// to the available width and aligned left, centre or right.
/// </summary>
/// <example>
/// <code>
/// var text = new TextElement { Foreground = Colour.Cyan, Bold = true, Align = Alignment.Centre };
/// text.AppendText("Welcome, ");
/// text.AppendValue(model.UserName);
/// </code>
/// </example>
public sealed class TextElement : InlineElement
{
}
