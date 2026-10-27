namespace ConsoleMVC.Rendering;

/// <summary>
/// A list of validation messages — the <c>&lt;errors /&gt;</c> markup element. Shows every error, or only the
/// errors of one field when <see cref="For"/> is set. Renders nothing when there are no errors.
/// </summary>
/// <remarks>
/// The element reads any <see cref="IReadOnlyDictionary{TKey, TValue}"/> of field name to messages, so the rendering
/// engine does not depend on MVC; in views the generator connects it to the view's <c>ModelState</c>.
/// </remarks>
/// <example>
/// <code>
/// var errors = new ValidationErrorsElement { Source = modelState, For = "Email" };
/// // • The Email field is not a valid e-mail address.
/// </code>
/// </example>
public sealed class ValidationErrorsElement : Element
{
    /// <summary>The marker written before each message.</summary>
    public const string Bullet = "• ";

    /// <summary>Gets or sets the errors to show, keyed by field name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Source { get; set; }

    /// <summary>Gets or sets the field whose errors are shown; <see langword="null"/> or empty shows all errors.</summary>
    public string? For { get; set; }

    /// <summary>Gets or sets the colour of the messages. Defaults to red.</summary>
    public Colour Colour { get; set; } = Colour.Red;

    /// <summary>Gets the messages that will be shown, in order.</summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            if (Source is null)
                return [];

            if (string.IsNullOrEmpty(For))
                return Source.Values.SelectMany(list => list).ToArray();

            return Source.TryGetValue(For, out var messages) ? messages : [];
        }
    }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        var messages = Messages;
        if (messages.Count == 0)
            return 0;

        return Math.Min(messages.Max(m => TextWidth.Of(TextSanitiser.Sanitise(m))) + Bullet.Length, Math.Max(maxWidth, 0));
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var style = TextStyle.Plain.WithForeground(Colour);
        var lines = new List<RenderedLine>();
        var indent = Bullet.Length;

        foreach (var message in Messages)
        {
            var rows = TextWrapper.Wrap(TextSanitiser.Sanitise(message), Math.Max(width - indent, 1));
            for (var i = 0; i < rows.Count; i++)
            {
                var prefix = i == 0 ? new Span(Bullet, style) : Span.Spaces(indent);
                lines.Add(new RenderedLine([prefix, new Span(rows[i], style)]).Clip(Math.Max(width, 0)));
            }
        }

        return lines;
    }
}
