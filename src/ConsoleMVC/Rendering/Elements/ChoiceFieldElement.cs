using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Base class of the fields that offer a list of <see cref="ChoiceElement"/>s: <see cref="SelectElement"/> (one under
/// another, optionally several answers) and <see cref="RadioElement"/> (side by side, one answer).
/// </summary>
/// <remarks>
/// <para>
/// In a terminal (<see cref="ITextInput.CanReadKeys"/> and <see cref="IMarkupRenderer.CanRewrite"/>) the user moves a
/// highlight with the arrow keys, ticks with Space and confirms with Enter; digits 1 to 9 jump to a choice. The field
/// is redrawn in place after every key and collapses to <c>Label answer</c> once answered.
/// </para>
/// <para>
/// Otherwise (piped input, tests, consoles without cursor control) the choices are numbered and the user types a
/// number, a value or a label; several answers are separated by commas. Both ways give the same form value. See
/// docs/design/choice-inputs.md.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var size = new RadioElement { Name = "Size", Label = "Size:", Default = "M" };
/// foreach (var label in new[] { "S", "M", "L" })
/// {
///     var choice = new ChoiceElement();
///     choice.AppendText(label);
///     size.Add(choice);
/// }
///
/// string? answer = size.Read(renderer, ConsoleTextInput.Instance); // "M" when the user just presses Enter
/// </code>
/// </example>
public abstract class ChoiceFieldElement : Element, IFormField
{
    /// <summary>The message shown for a typed answer that matches no choice; <c>{0}</c> is the answer.</summary>
    public const string UnknownChoiceMessage = "'{0}' is not one of the choices.";

    /// <summary>The marker in front of the highlighted choice in arrow-key mode.</summary>
    public const string HighlightMarker = ">";

    /// <summary>The separator between several answers in the form value.</summary>
    public const char ValueSeparator = ',';

    private readonly List<ChoiceElement> _choices = [];

    // The state shown by Layout: how the field is being answered, the highlight, the ticks and a message
    private FieldMode _mode = FieldMode.Static;
    private int _cursor;
    private bool[] _ticked = [];
    private string? _error;

    /// <summary>Gets or sets the form key the answer is stored under.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the text shown before the choices, e.g. <c>"Favourite colour:"</c>.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the value chosen when the user enters nothing, and the highlighted choice in arrow-key mode. For
    /// several answers, separate the values with commas.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>Gets or sets whether an empty answer is rejected and the field asked again.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets the colour of the label.</summary>
    public Colour LabelColour { get; set; }

    /// <summary>Gets or sets the colour of the highlight marker in arrow-key mode.</summary>
    public Colour HighlightColour { get; set; } = Colour.Cyan;

    /// <summary>Gets the choices in display order.</summary>
    public IReadOnlyList<ChoiceElement> Choices => _choices;

    /// <summary>Gets whether several choices may be ticked. Only <see cref="SelectElement.Multiple"/> allows it.</summary>
    protected virtual bool AllowsSeveral => false;

    /// <summary>Gets whether the choices are laid out side by side (<see langword="true"/>) or one under another.</summary>
    protected abstract bool Horizontal { get; }

    /// <summary>
    /// Appends a choice.
    /// </summary>
    /// <param name="choice">The choice to append.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="choice"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when another choice already has the same value (ignoring case).</exception>
    /// <example><code>select.Add(new ChoiceElement { Value = "red" });</code></example>
    public void Add(ChoiceElement choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        if (_choices.Any(c => string.Equals(c.EffectiveValue, choice.EffectiveValue, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"{ElementName} '{Name}' already has a choice with the value '{choice.EffectiveValue}'.", nameof(choice));

        _choices.Add(choice);
    }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        var limit = Math.Max(maxWidth, 0);
        var lines = Layout(Math.Max(limit, 1));
        return lines.Count == 0 ? 0 : Math.Min(lines.Max(l => l.Width), limit);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Before <see cref="Read"/> and for typed answers the choices are numbered; while keys are read the highlight and
    /// ticks are shown; once answered the field is a single line with the answer.
    /// </remarks>
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        width = Math.Max(width, 1);

        return _mode switch
        {
            FieldMode.Answered => AnswerLines(width),
            FieldMode.Keys => KeyLines(width),
            _ => NumberedLines(width)
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the field has no choices, or when several answers are allowed and a value contains a comma.
    /// </exception>
    public string? Read(IMarkupRenderer renderer, ITextInput input)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        if (_choices.Count == 0)
            throw new InvalidOperationException($"{ElementName} '{Name}' has no choices. Add <choice> elements, or check the list it is built from.");

        if (AllowsSeveral && _choices.Any(c => c.EffectiveValue.Contains(ValueSeparator)))
            throw new InvalidOperationException(
                $"{ElementName} '{Name}' allows several answers, so its choice values cannot contain '{ValueSeparator}'.");

        return input.CanReadKeys && renderer.CanRewrite ? ReadWithKeys(renderer, input) : ReadTyped(renderer, input);
    }

    /// <summary>Gets the element as written in markup, for messages, e.g. <c>&lt;select&gt;</c>.</summary>
    protected virtual string ElementName => GetType().Name;

    // ------------------------------------------------------------------ typed answers

    private string? ReadTyped(IMarkupRenderer renderer, ITextInput input)
    {
        _mode = FieldMode.Static;
        renderer.Render(this);

        while (true)
        {
            renderer.WritePrompt(TypedPrompt(), TextStyle.Plain);

            var line = input.ReadLine();
            if (line is null)
                return null;

            renderer.RecordInput(line);

            var answer = line.Trim();
            if (answer.Length == 0)
            {
                var fallback = Default?.Trim() ?? "";
                if (!Required || fallback.Length > 0)
                    return fallback;

                ShowError(renderer, InputElement.RequiredMessage);
                continue;
            }

            var tokens = AllowsSeveral ? answer.Split(ValueSeparator) : [answer];
            var chosen = new SortedSet<int>();
            string? unknown = null;

            foreach (var token in tokens.Select(t => t.Trim()).Where(t => t.Length > 0))
            {
                var index = FindChoice(token);
                if (index < 0)
                {
                    unknown = token;
                    break;
                }

                chosen.Add(index);
            }

            if (unknown is not null)
            {
                ShowError(renderer, string.Format(CultureInfo.InvariantCulture, UnknownChoiceMessage, unknown));
                continue;
            }

            if (chosen.Count == 0 && Required)
            {
                ShowError(renderer, InputElement.RequiredMessage);
                continue;
            }

            // In the order of the choices, without duplicates
            return string.Join(ValueSeparator, chosen.Select(i => _choices[i].EffectiveValue));
        }
    }

    /// <summary>Finds a choice by number (1-based), value or label, ignoring case; -1 when none matches.</summary>
    private int FindChoice(string token)
    {
        if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= _choices.Count)
            return number - 1;

        var byValue = _choices.FindIndex(c => string.Equals(c.EffectiveValue, token, StringComparison.OrdinalIgnoreCase));
        return byValue >= 0 ? byValue : _choices.FindIndex(c => string.Equals(c.LabelText, token, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>"Choose 1-3 [Green]: " or "Choose one or more of 1-3, e.g. 1,3: ".</summary>
    private string TypedPrompt()
    {
        var range = _choices.Count == 1 ? "1" : "1-" + _choices.Count.ToString(CultureInfo.InvariantCulture);
        var prompt = AllowsSeveral ? $"Choose one or more of {range}, e.g. 1,{_choices.Count}" : "Choose " + range;

        var defaults = DefaultIndices();
        if (defaults.Count > 0)
            prompt += " [" + string.Join(", ", defaults.Select(i => _choices[i].LabelText)) + "]";
        else if (!string.IsNullOrWhiteSpace(Default))
            prompt += " [" + Default.Trim() + "]";

        return prompt + ": ";
    }

    // ------------------------------------------------------------------ arrow keys

    private string? ReadWithKeys(IMarkupRenderer renderer, ITextInput input)
    {
        var defaults = DefaultIndices();
        _cursor = defaults.Count > 0 ? defaults[0] : 0;
        _ticked = new bool[_choices.Count];
        if (AllowsSeveral)
        {
            foreach (var index in defaults)
                _ticked[index] = true;
        }

        _error = null;
        _mode = FieldMode.Keys;
        renderer.Render(this);

        while (true)
        {
            var read = input.ReadKey();
            if (read is not { } key)
                return null;

            var outcome = HandleKey(key);
            if (outcome == KeyOutcome.Ignored)
                continue;

            if (outcome == KeyOutcome.Confirmed)
            {
                _mode = FieldMode.Answered;
                renderer.Rewrite(this);
                return string.Join(ValueSeparator, SelectedIndices().Select(i => _choices[i].EffectiveValue));
            }

            renderer.Rewrite(this);
        }
    }

    private KeyOutcome HandleKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.LeftArrow:
                return Move(_cursor == 0 ? _choices.Count - 1 : _cursor - 1);

            case ConsoleKey.DownArrow or ConsoleKey.RightArrow or ConsoleKey.Tab:
                return Move(_cursor == _choices.Count - 1 ? 0 : _cursor + 1);

            case ConsoleKey.Home:
                return Move(0);

            case ConsoleKey.End:
                return Move(_choices.Count - 1);

            case ConsoleKey.Spacebar when AllowsSeveral:
                _ticked[_cursor] = !_ticked[_cursor];
                _error = null;
                return KeyOutcome.Changed;

            case ConsoleKey.Enter:
                if (Required && SelectedIndices().Count == 0)
                {
                    _error = InputElement.RequiredMessage;
                    return KeyOutcome.Changed;
                }

                return KeyOutcome.Confirmed;
        }

        if (key.KeyChar is >= '1' and <= '9' && key.KeyChar - '1' < _choices.Count)
            return Move(key.KeyChar - '1');

        return KeyOutcome.Ignored;
    }

    private KeyOutcome Move(int index)
    {
        if (index == _cursor)
            return KeyOutcome.Ignored;

        _cursor = index;
        return KeyOutcome.Changed;
    }

    /// <summary>The ticked choices, or the highlighted one when only one answer is allowed.</summary>
    private List<int> SelectedIndices() =>
        AllowsSeveral ? Enumerable.Range(0, _choices.Count).Where(i => _ticked[i]).ToList() : [_cursor];

    /// <summary>The choices named by <see cref="Default"/>, matched like typed answers.</summary>
    private List<int> DefaultIndices()
    {
        if (string.IsNullOrWhiteSpace(Default))
            return [];

        var tokens = AllowsSeveral ? Default.Split(ValueSeparator) : [Default];
        return tokens.Select(t => t.Trim()).Where(t => t.Length > 0)
            .Select(t => _choices.FindIndex(c => string.Equals(c.EffectiveValue, t, StringComparison.OrdinalIgnoreCase)))
            .Where(i => i >= 0).Distinct().Order().ToList();
    }

    // ------------------------------------------------------------------ layout

    private IReadOnlyList<RenderedLine> NumberedLines(int width)
    {
        var items = _choices.Select((c, i) => (Marker: (i + 1).ToString(CultureInfo.InvariantCulture) + ")", Choice: c, Bold: false)).ToList();
        return ChoiceLines(width, items, hint: null, markerStyle: TextStyle.Plain, indent: LabelIndent + 2);
    }

    private IReadOnlyList<RenderedLine> KeyLines(int width)
    {
        var markerStyle = TextStyle.Plain.WithForeground(HighlightColour).WithDecoration(TextDecoration.Bold);
        var items = _choices.Select((c, i) =>
        {
            var marker = Horizontal
                ? (i == _cursor ? "(*)" : "( )")
                : (i == _cursor ? HighlightMarker : " ") + (AllowsSeveral ? (_ticked[i] ? " [x]" : " [ ]") : "");
            return (Marker: marker, Choice: c, Bold: i == _cursor);
        }).ToList();

        var keys = Horizontal ? "Left/Right" : "Up/Down";
        var hint = AllowsSeveral ? $"({keys}, Space, Enter)" : $"({keys}, Enter)";
        var lines = ChoiceLines(width, items, hint, markerStyle, indent: LabelIndent + 1).ToList();

        if (_error is not null)
        {
            var error = TextStyle.Plain.WithForeground(Colour.Red);
            lines.AddRange(TextWrapper.Wrap(_error, Math.Max(width - LabelIndent, 1))
                .Select(row => new RenderedLine([Span.Spaces(LabelIndent), new Span(row, error)]).Clip(width)));
        }

        return lines;
    }

    private IReadOnlyList<RenderedLine> AnswerLines(int width)
    {
        var answer = string.Join(", ", SelectedIndices().Select(i => _choices[i].LabelText));
        var spans = new List<Span>();
        if (SafeLabel.Length > 0)
            spans.Add(new Span(SafeLabel + " ", LabelStyle));
        spans.Add(new Span(answer, TextStyle.Plain.WithDecoration(TextDecoration.Bold)));

        return [new RenderedLine(spans).Clip(width)];
    }

    /// <summary>
    /// The label, then each choice with its marker: one per line (labels wrapping under themselves), or flowed side by
    /// side after the label for a horizontal field.
    /// </summary>
    private IReadOnlyList<RenderedLine> ChoiceLines(int width, List<(string Marker, ChoiceElement Choice, bool Bold)> items,
        string? hint, TextStyle markerStyle, int indent)
    {
        var hintStyle = TextStyle.Plain.WithDecoration(TextDecoration.Dim);

        if (Horizontal)
        {
            // One space after the label, two between choices
            var segments = new List<(Span[] Spans, int Gap)>();
            if (SafeLabel.Length > 0)
                segments.Add(([new Span(SafeLabel, LabelStyle)], 0));

            foreach (var (marker, choice, bold) in items)
            {
                var style = bold ? choice.Style.WithDecoration(TextDecoration.Bold) : choice.Style;
                var gap = segments.Count == 0 ? 0 : segments.Count == 1 && SafeLabel.Length > 0 ? 1 : 2;
                segments.Add(([new Span(marker, markerStyle), Span.Spaces(1), new Span(TextSanitiser.Sanitise(choice.LabelText), style)], gap));
            }

            if (hint is not null)
                segments.Add(([new Span(hint, hintStyle)], 2));

            return Flow(segments, width);
        }

        var lines = new List<RenderedLine>();
        if (SafeLabel.Length > 0 || hint is not null)
        {
            var heading = new List<(Span[] Spans, int Gap)>();
            if (SafeLabel.Length > 0)
                heading.Add(([new Span(SafeLabel, LabelStyle)], 0));
            if (hint is not null)
                heading.Add(([new Span(hint, hintStyle)], 1));
            lines.AddRange(Flow(heading, width));
        }

        foreach (var (marker, choice, bold) in items)
        {
            var prefixWidth = indent + TextWidth.Of(marker) + 1;
            var labelLines = choice.Layout(Math.Max(width - prefixWidth, 1), bold ? TextDecoration.Bold : TextDecoration.None);

            for (var i = 0; i < labelLines.Count; i++)
            {
                IEnumerable<Span> prefix = i == 0
                    ? [Span.Spaces(indent), new Span(marker, markerStyle), Span.Spaces(1)]
                    : [Span.Spaces(prefixWidth)];

                lines.Add(labelLines[i].Surround(prefix, []).Clip(width));
            }
        }

        return lines;
    }

    /// <summary>
    /// Places segments side by side, each after its gap of spaces, starting a new line when one does not fit.
    /// </summary>
    private static List<RenderedLine> Flow(List<(Span[] Spans, int Gap)> segments, int width)
    {
        var lines = new List<RenderedLine>();
        var current = new List<Span>();
        var used = 0;

        foreach (var (segment, gap) in segments)
        {
            var segmentWidth = segment.Sum(s => s.Width);
            if (current.Count > 0 && used + gap + segmentWidth > width)
            {
                lines.Add(new RenderedLine(current).Clip(width));
                current = [];
                used = 0;
            }

            if (current.Count > 0)
            {
                current.Add(Span.Spaces(gap));
                used += gap;
            }

            current.AddRange(segment);
            used += segmentWidth;
        }

        if (current.Count > 0)
            lines.Add(new RenderedLine(current).Clip(width));

        return lines;
    }

    private static void ShowError(IMarkupRenderer renderer, string message)
    {
        var error = new TextElement { Foreground = Colour.Red };
        error.AppendText(message);
        renderer.Render(error);
    }

    /// <summary>The label as one line; leading spaces are kept, as an indent for the whole field.</summary>
    private string SafeLabel => TextSanitiser.Sanitise(Label).Replace('\n', ' ').TrimEnd();

    /// <summary>The number of spaces the label starts with, which the choices are indented by too.</summary>
    private int LabelIndent => SafeLabel.Length - SafeLabel.TrimStart(' ').Length;

    private TextStyle LabelStyle => TextStyle.Plain.WithForeground(LabelColour);

    /// <summary>How the field is shown.</summary>
    private enum FieldMode
    {
        /// <summary>Numbered choices: before reading, and for typed answers.</summary>
        Static,

        /// <summary>The highlight and ticks, while keys are read.</summary>
        Keys,

        /// <summary>One line with the answer.</summary>
        Answered
    }

    /// <summary>What a key press did.</summary>
    private enum KeyOutcome
    {
        /// <summary>Nothing changed; the field is not redrawn.</summary>
        Ignored,

        /// <summary>The highlight, a tick or the message changed.</summary>
        Changed,

        /// <summary>The answer was confirmed.</summary>
        Confirmed
    }
}
