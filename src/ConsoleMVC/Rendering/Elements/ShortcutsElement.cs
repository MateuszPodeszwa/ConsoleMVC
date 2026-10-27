namespace ConsoleMVC.Rendering;

/// <summary>One keyboard shortcut as it is shown to the user, e.g. <c>F1</c> and <c>Help</c>.</summary>
/// <param name="Key">The key, e.g. <c>F1</c> or <c>Ctrl+X</c>.</param>
/// <param name="Description">What it does.</param>
/// <example><code>new ShortcutHint("F1", "Help");</code></example>
public sealed record ShortcutHint(string Key, string Description);

/// <summary>
/// The application's keyboard shortcuts on one line, e.g. <c>F1 Help  Esc Back</c>: the <c>&lt;shortcuts /&gt;</c>
/// markup element. Views fill <see cref="Items"/> from the shortcuts mapped with <c>app.MapKey</c> that have a
/// description; a layout is a good place for it.
/// </summary>
/// <example>
/// <code>
/// var shortcuts = new ShortcutsElement { Items = [new ShortcutHint("F1", "Help"), new ShortcutHint("Esc", "Back")] };
/// renderer.Render(shortcuts); // F1 Help  Esc Back
/// </code>
/// </example>
public sealed class ShortcutsElement : Element
{
    /// <summary>Gets or sets the shortcuts to show.</summary>
    public IReadOnlyList<ShortcutHint> Items { get; set; } = [];

    /// <summary>Gets or sets the text between shortcuts.</summary>
    public string Separator { get; set; } = "  ";

    /// <summary>Gets or sets the colour of the keys.</summary>
    public Colour KeyColour { get; set; }

    /// <summary>Gets or sets the colour of the descriptions.</summary>
    public Colour Colour { get; set; }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) =>
        Items.Count == 0 ? 0 : Math.Min(Layout(int.MaxValue / 2)[0].Width, Math.Max(maxWidth, 0));

    /// <inheritdoc />
    /// <remarks>Shortcuts that do not fit are left out from the end; nothing is drawn when there are none.</remarks>
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        if (Items.Count == 0)
            return [];

        var keyStyle = TextStyle.Plain.WithForeground(KeyColour).WithDecoration(TextDecoration.Bold);
        var textStyle = TextStyle.Plain.WithForeground(Colour);
        var separator = TextSanitiser.Sanitise(Separator).Replace('\n', ' ');
        var spans = new List<Span>();
        var used = 0;

        foreach (var item in Items)
        {
            var key = TextSanitiser.Sanitise(item.Key).Replace('\n', ' ');
            var text = TextSanitiser.Sanitise(item.Description).Replace('\n', ' ');
            var gap = spans.Count == 0 ? "" : separator;
            var needed = TextWidth.Of(gap) + TextWidth.Of(key) + 1 + TextWidth.Of(text);
            if (spans.Count > 0 && used + needed > width)
                break;

            if (gap.Length > 0)
                spans.Add(new Span(gap, textStyle));
            spans.Add(new Span(key, keyStyle));
            spans.Add(new Span(" " + text, textStyle));
            used += needed;
        }

        return [new RenderedLine(spans).Clip(Math.Max(width, 0))];
    }
}
