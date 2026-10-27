namespace ConsoleMVC.Rendering;

/// <summary>
/// A spinner and its label on one line, e.g. <c>⠋ Connecting</c>: the <c>&lt;spinner&gt;</c> markup element. It turns
/// while it is shown in a <see cref="LiveRegion"/> (<see cref="ILiveDisplay.Start"/>); drawn on its own it shows
/// <see cref="Frame"/>. Set <see cref="Done"/> to show <see cref="DoneSymbol"/> instead, once the work has finished.
/// </summary>
/// <example>
/// <code>
/// var spinner = new SpinnerElement { Label = "Connecting" };
/// await using (var live = display.Start(spinner))
/// {
///     await client.ConnectAsync();
///     live.Update(() => { spinner.Label = "Connected"; spinner.Done = true; });
/// }
/// </code>
/// </example>
public sealed class SpinnerElement : Element, IAnimatedElement
{
    /// <summary>Gets or sets the text after the spinner.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the frames. Defaults to <see cref="SpinnerStyle.Dots"/>.</summary>
    public SpinnerStyle Style { get; set; } = SpinnerStyle.Dots;

    /// <summary>Gets or sets the frame shown, counted from 0; any number wraps around the style's frames.</summary>
    /// <example><code>spinner.Frame = Model.Tick; // a view that refreshes turns its spinner itself</code></example>
    public int Frame { get; set; }

    /// <summary>Gets or sets whether the work is finished: <see cref="DoneSymbol"/> is shown instead of a frame.</summary>
    public bool Done { get; set; }

    /// <summary>Gets or sets what is shown once <see cref="Done"/>. Defaults to <c>✓</c>.</summary>
    public string DoneSymbol { get; set; } = "✓";

    /// <summary>Gets or sets the colour of the spinner (and of the done symbol).</summary>
    public Colour Colour { get; set; }

    /// <summary>Gets or sets the colour of the label.</summary>
    public Colour LabelColour { get; set; }

    /// <summary>Moves to the next frame; a <see cref="LiveRegion"/> calls this at each tick.</summary>
    /// <example><code>spinner.Advance();</code></example>
    public void Advance()
    {
        var count = Style.Frames.Count;
        Frame = (int)(((long)Frame % count + count + 1) % count);
    }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) => Math.Min(Line().Width, Math.Max(maxWidth, 0));

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width) => [Line().Clip(Math.Max(width, 0))];

    private RenderedLine Line()
    {
        var symbol = Done ? TextSanitiser.Sanitise(DoneSymbol ?? "").Replace('\n', ' ') : CurrentFrame();
        var label = TextSanitiser.Sanitise(Label ?? "").Replace('\n', ' ');

        var spans = new List<Span> { new(symbol, TextStyle.Plain.WithForeground(Colour)) };
        if (label.Length > 0)
            spans.Add(new Span(" " + label, TextStyle.Plain.WithForeground(LabelColour)));

        return new RenderedLine(spans);
    }

    private string CurrentFrame()
    {
        var count = Style.Frames.Count;
        return Style.Frames[(int)(((long)Frame % count + count) % count)];
    }
}
