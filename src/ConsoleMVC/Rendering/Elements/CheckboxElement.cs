namespace ConsoleMVC.Rendering;

/// <summary>
/// A yes/no question: the <c>&lt;checkbox&gt;</c> markup element. The form value is <c>"true"</c> or <c>"false"</c>,
/// which binds to a <see cref="bool"/>.
/// </summary>
/// <remarks>
/// In a terminal the box is ticked with Space and confirmed with Enter; <c>y</c> and <c>n</c> answer at once. Otherwise
/// the user types y/yes/true/1 or n/no/false/0, and an empty answer keeps <see cref="Checked"/>.
/// </remarks>
/// <example>
/// <code>
/// var news = new CheckboxElement { Name = "Newsletter", Label = "Send me the newsletter" };
///
/// // Arrow keys: [ ] Send me the newsletter  (Space, Enter)
/// // Typed:      Send me the newsletter? [y/N]: _
/// string? answer = news.Read(renderer, ConsoleTextInput.Instance); // "true" or "false"
/// </code>
/// </example>
public sealed class CheckboxElement : Element, IFormField
{
    /// <summary>The message shown for a typed answer that is neither yes nor no.</summary>
    public const string AnswerMessage = "Please answer y or n.";

    /// <summary>The message shown when a required box is left unticked.</summary>
    public const string RequiredMessage = "This box must be ticked.";

    private static readonly string[] Yes = ["y", "yes", "true", "1"];
    private static readonly string[] No = ["n", "no", "false", "0"];

    /// <summary>Whether a typed answer is the translated letter or word for yes or no.</summary>
    private static bool IsAnswer(string answer, string letter, string word) =>
        string.Equals(answer, letter, StringComparison.CurrentCultureIgnoreCase) || string.Equals(answer, word, StringComparison.CurrentCultureIgnoreCase);

    // The state shown by Layout while keys are read
    private bool _usingKeys;
    private bool _ticked;
    private string? _error;

    /// <summary>Gets or sets the form key the answer is stored under.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the text shown beside the box, e.g. <c>"Send me the newsletter"</c>.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets whether the box starts ticked (the answer when the user enters nothing).</summary>
    public bool Checked { get; set; }

    /// <summary>Gets or sets whether the box must be ticked to continue, e.g. to accept terms.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets the colour of the label.</summary>
    public Colour LabelColour { get; set; }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        var limit = Math.Max(maxWidth, 0);
        return Math.Min(Layout(Math.Max(limit, 1)).Max(l => l.Width), limit);
    }

    /// <inheritdoc />
    /// <remarks>Shows the box and label; while keys are read it also shows the keys to press and any message.</remarks>
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        width = Math.Max(width, 1);
        // A label's leading spaces indent the box, so "  Shout" lines up with other indented fields
        var label = SafeLabel.TrimStart(' ');
        var indent = SafeLabel.Length - label.Length;
        var spans = new List<Span> { Span.Spaces(indent), new(_ticked ? "[x]" : "[ ]", TextStyle.Plain.WithDecoration(TextDecoration.Bold)) };
        if (label.Length > 0)
            spans.AddRange([Span.Spaces(1), new Span(label, LabelStyle)]);
        if (_usingKeys)
            spans.AddRange([Span.Spaces(2), new Span("(Space, Enter)", TextStyle.Plain.WithDecoration(TextDecoration.Dim))]);

        var lines = new List<RenderedLine> { new RenderedLine(spans).Clip(width) };
        if (_usingKeys && _error is not null)
        {
            var error = TextStyle.Plain.WithForeground(Colour.Red);
            lines.AddRange(TextWrapper.Wrap(_error, Math.Max(width - indent, 1))
                .Select(row => new RenderedLine([Span.Spaces(indent), new Span(row, error)]).Clip(width)));
        }

        return lines;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public string? Read(IMarkupRenderer renderer, ITextInput input)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        _ticked = Checked;
        _error = null;
        var answer = input.CanReadKeys && renderer.CanRewrite ? ReadWithKeys(renderer, input) : ReadTyped(renderer, input);
        return answer is null ? null : answer.Value ? "true" : "false";
    }

    private bool? ReadTyped(IMarkupRenderer renderer, ITextInput input)
    {
        _usingKeys = false;
        var words = FrameworkText.Current;
        var prompt = Question() + (Checked
            ? $" [{words.YesLetter.ToUpperInvariant()}/{words.NoLetter}]: "
            : $" [{words.YesLetter}/{words.NoLetter.ToUpperInvariant()}]: ");

        while (true)
        {
            renderer.WritePrompt(prompt, LabelStyle);

            var line = input.ReadLine();
            if (line is null)
                return null;

            renderer.RecordInput(line);

            var text = line.Trim();
            bool answer;
            if (text.Length == 0)
                answer = Checked;
            else if (Yes.Contains(text, StringComparer.OrdinalIgnoreCase) || IsAnswer(text, FrameworkText.Current.YesLetter, FrameworkText.Current.YesWord))
                answer = true;
            else if (No.Contains(text, StringComparer.OrdinalIgnoreCase) || IsAnswer(text, FrameworkText.Current.NoLetter, FrameworkText.Current.NoWord))
                answer = false;
            else
            {
                ShowError(renderer, FrameworkText.Current.CheckboxAnswer);
                continue;
            }

            if (Required && !answer)
            {
                ShowError(renderer, FrameworkText.Current.CheckboxRequired);
                continue;
            }

            _ticked = answer;
            return answer;
        }
    }

    private bool? ReadWithKeys(IMarkupRenderer renderer, ITextInput input)
    {
        _usingKeys = true;
        renderer.Render(this);
        MapClicks(renderer);

        while (true)
        {
            var read = input.ReadKey();
            if (read is not { } key)
                return null;

            bool confirm;
            switch (char.ToLowerInvariant(key.KeyChar))
            {
                case 'y':
                    _ticked = true;
                    confirm = true;
                    break;
                case 'n':
                    _ticked = false;
                    confirm = true;
                    break;
                case ' ':
                    _ticked = !_ticked;
                    _error = null;
                    confirm = false;
                    break;
                case '\r' or '\n':
                    confirm = true;
                    break;
                default:
                    continue;
            }

            if (confirm && Required && !_ticked)
            {
                _error = FrameworkText.Current.CheckboxRequired;
                confirm = false;
            }

            if (confirm)
                _usingKeys = false;

            renderer.Rewrite(this);
            if (confirm)
                return _ticked;
            MapClicks(renderer);
        }
    }

    /// <summary>Clicking the box (its first line) ticks or clears it, as Space does.</summary>
    private static void MapClicks(IMarkupRenderer renderer) =>
        renderer.MapClicks([new ClickTarget(0, [ClickTarget.Key(ConsoleKey.Spacebar)])]);

    /// <summary>The label as a question: "Send news?"; a label already ending in punctuation is kept as it is.</summary>
    private string Question() =>
        SafeLabel.Trim().Length == 0 ? "" : SafeLabel[^1] is '?' or ':' or '.' or '!' ? SafeLabel : SafeLabel + "?";

    private static void ShowError(IMarkupRenderer renderer, string message)
    {
        var error = new TextElement { Foreground = Colour.Red };
        error.AppendText(message);
        renderer.Render(error);
    }

    /// <summary>The label as one line; leading spaces are kept, as an indent.</summary>
    private string SafeLabel => TextSanitiser.Sanitise(Label).Replace('\n', ' ').TrimEnd();

    private TextStyle LabelStyle => TextStyle.Plain.WithForeground(LabelColour);
}
