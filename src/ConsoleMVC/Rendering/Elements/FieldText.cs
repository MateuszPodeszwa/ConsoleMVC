namespace ConsoleMVC.Rendering;

/// <summary>Text helpers shared by the form fields: safe labels, styles and error messages.</summary>
internal static class FieldText
{
    /// <summary>The label as one line without escape sequences; leading spaces are kept as an indent.</summary>
    /// <param name="label">The label, or <see langword="null"/>.</param>
    public static string SafeLabel(string? label) => TextSanitiser.Sanitise(label).Replace('\n', ' ').TrimEnd();

    /// <summary>The number of spaces <paramref name="safeLabel"/> starts with.</summary>
    public static int Indent(string safeLabel) => safeLabel.Length - safeLabel.TrimStart(' ').Length;

    /// <summary>The style of the keys-to-press hint.</summary>
    public static TextStyle Hint => TextStyle.Plain.WithDecoration(TextDecoration.Dim);

    /// <summary>Writes an error message in red below the field (typed mode).</summary>
    public static void ShowError(IMarkupRenderer renderer, string message)
    {
        var error = new TextElement { Foreground = Colour.Red };
        error.AppendText(message);
        renderer.Render(error);
    }

    /// <summary>The lines of an error message shown inside a field (arrow-key mode), indented like the label.</summary>
    public static IEnumerable<RenderedLine> ErrorLines(string message, int indent, int width)
    {
        var style = TextStyle.Plain.WithForeground(Colour.Red);
        return TextWrapper.Wrap(message, Math.Max(width - indent, 1))
            .Select(row => new RenderedLine([Span.Spaces(indent), new Span(row, style)]).Clip(width));
    }
}
