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
public abstract class ChoiceFieldElement : Element, IFormField, IPrefilledField
{
    /// <summary>The message shown for a typed answer that matches no choice; <c>{0}</c> is the answer.</summary>
    public const string UnknownChoiceMessage = "'{0}' is not one of the choices.";

    /// <summary>The marker in front of the highlighted choice in arrow-key mode.</summary>
    public const string HighlightMarker = ">";

    /// <summary>The separator between several answers in the form value.</summary>
    public const char ValueSeparator = ',';

    /// <summary>The message shown when a filter matches no choice; <c>{0}</c> is the filter.</summary>
    public const string NoMatchMessage = "No choice matches '{0}'.";

    /// <summary>The message shown for a typed part of a name that matches several choices; <c>{0}</c> is the text, <c>{1}</c> the matches.</summary>
    public const string AmbiguousMessage = "'{0}' matches {1}; type more of the name.";

    private readonly List<ChoiceElement> _choices = [];

    // The state shown by Layout: how the field is being answered, the highlight, the ticks and a message
    private FieldMode _mode = FieldMode.Static;
    private int _cursor;
    private bool[] _ticked = [];
    private string? _error;
    private string _filter = "";

    /// <summary>Gets or sets the form key the answer is stored under.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the text shown before the choices, e.g. <c>"Favourite colour:"</c>.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the value chosen when the user enters nothing, and the highlighted choice in arrow-key mode. For
    /// several answers, separate the values with commas. Each must be a choice's value (ignoring case), or
    /// <see cref="Read"/> throws; an empty answer stores the choice's own value.
    /// </summary>
    /// <example><code>var size = new RadioElement { Name = "Size", Default = "m" }; // an empty answer stores "M"</code></example>
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
    /// Gets whether typing narrows the choices (arrow-key mode) and a typed part of a name chooses the one choice it
    /// matches (typed mode). Only <see cref="SelectElement.Filter"/> allows it.
    /// </summary>
    protected virtual bool AllowsFilter => false;

    /// <summary>
    /// Gets how many choices are shown at once in arrow-key mode, scrolling with the highlight; 0 shows all of them.
    /// Only <see cref="SelectElement.Size"/> sets it.
    /// </summary>
    protected virtual int VisibleRows => 0;

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
    /// Thrown when the field has no choices, when several answers are allowed and a value contains a comma, or when
    /// <see cref="Default"/> names a value that no choice has.
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

        // An empty answer takes the default, which must therefore be a choice
        if (DefaultTokens().FirstOrDefault(t => FindValue(t) < 0) is { } unknown)
            throw new InvalidOperationException(
                $"{ElementName} '{Name}' has default '{unknown}', which is not the value of any of its choices ({string.Join(", ", _choices.Select(c => c.EffectiveValue))}).");

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
                // The default's choices, as a typed answer naming them would be stored
                var defaults = DefaultIndices();
                if (!Required || defaults.Count > 0)
                    return string.Join(ValueSeparator, defaults.Select(i => _choices[i].EffectiveValue));

                ShowError(renderer, FrameworkText.Current.Required);
                continue;
            }

            var tokens = AllowsSeveral ? answer.Split(ValueSeparator) : [answer];
            var chosen = new SortedSet<int>();
            string? unknown = null;

            string? problem = null;
            foreach (var token in tokens.Select(t => t.Trim()).Where(t => t.Length > 0))
            {
                var index = FindChoice(token);
                if (index < 0 && AllowsFilter)
                {
                    var matches = Matching(token);
                    if (matches.Count == 1)
                        index = matches[0];
                    else if (matches.Count > 1)
                        problem = FrameworkText.Format(FrameworkText.Current.Ambiguous, token,
                            string.Join(", ", matches.Take(5).Select(i => _choices[i].LabelText)) + (matches.Count > 5 ? ", …" : ""));
                }

                if (index < 0)
                {
                    unknown = token;
                    break;
                }

                chosen.Add(index);
            }

            if (unknown is not null)
            {
                ShowError(renderer, problem ?? FrameworkText.Format(FrameworkText.Current.UnknownChoice, unknown));
                continue;
            }

            if (chosen.Count == 0 && Required)
            {
                ShowError(renderer, FrameworkText.Current.Required);
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

    /// <summary>The choices whose label or value contains <paramref name="text"/>, ignoring case.</summary>
    private List<int> Matching(string text) =>
        text.Length == 0
            ? Enumerable.Range(0, _choices.Count).ToList()
            : Enumerable.Range(0, _choices.Count).Where(i =>
                _choices[i].LabelText.Contains(text, StringComparison.OrdinalIgnoreCase)
                || _choices[i].EffectiveValue.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>"Choose 1-3 [Green]: " or "Choose one or more of 1-3, e.g. 1,3: ".</summary>
    private string TypedPrompt()
    {
        var range = _choices.Count == 1 ? "1" : "1-" + _choices.Count.ToString(CultureInfo.InvariantCulture);
        var prompt = AllowsSeveral
            ? FrameworkText.Format(FrameworkText.Current.ChooseSeveral, range, "1," + _choices.Count.ToString(CultureInfo.InvariantCulture))
            : FrameworkText.Format(FrameworkText.Current.ChooseOne, range);

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
        _filter = "";
        _mode = FieldMode.Keys;
        renderer.Render(this);
        renderer.MapClicks(_clickTargets);

        // Esc clears a filter being typed; without one, it is left to an application shortcut
        using var claim = FieldKeyClaim.Claim(key => key.Key == ConsoleKey.Escape && AllowsFilter && _filter.Length > 0);

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
            renderer.MapClicks(_clickTargets);
        }
    }

    /// <summary>
    /// The click targets of the last layout in keys mode: a choice's lines move the highlight there (Home, then Down) and
    /// press Enter, or Space to tick it in a <c>&lt;select multiple&gt;</c>. Horizontal fields have none.
    /// </summary>
    private IReadOnlyList<ClickTarget> _clickTargets = [];

    /// <summary>The choice each line of the last vertical layout shows, by line object.</summary>
    private readonly Dictionary<RenderedLine, int> _choiceOfLine = new(ReferenceEqualityComparer.Instance);

    /// <summary>Where each choice of the last side-by-side layout is: its line and columns, and its index.</summary>
    private readonly List<(int Line, int From, int To, int Choice)> _choicePlaces = [];

    private void MapChoiceLines(IReadOnlyList<RenderedLine> lines)
    {
        var visible = Matching(_filter);
        var targets = new List<ClickTarget>();

        // Side by side: a choice is part of a line; clicking it moves there (Home, then Right) and presses Enter
        foreach (var (line, from, to, choice) in _choicePlaces)
        {
            var position = visible.IndexOf(choice);
            if (position < 0 || line >= lines.Count)
                continue;

            var keys = new List<ConsoleKeyInfo> { ClickTarget.Key(ConsoleKey.Home) };
            keys.AddRange(Enumerable.Repeat(ClickTarget.Key(ConsoleKey.RightArrow), position));
            keys.Add(ClickTarget.Key(ConsoleKey.Enter));
            targets.Add(new ClickTarget(line, keys) { FromColumn = from, ToColumn = to });
        }

        for (var line = 0; line < lines.Count; line++)
        {
            if (!_choiceOfLine.TryGetValue(lines[line], out var choice))
                continue;

            var position = visible.IndexOf(choice);
            if (position < 0)
                continue;

            var keys = new List<ConsoleKeyInfo> { ClickTarget.Key(ConsoleKey.Home) };
            keys.AddRange(Enumerable.Repeat(ClickTarget.Key(ConsoleKey.DownArrow), position));
            keys.Add(ClickTarget.Key(AllowsSeveral ? ConsoleKey.Spacebar : ConsoleKey.Enter));
            targets.Add(new ClickTarget(line, keys));
        }

        _clickTargets = targets;
    }

    private KeyOutcome HandleKey(ConsoleKeyInfo key)
    {
        var visible = Matching(_filter);
        var position = visible.IndexOf(_cursor);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.LeftArrow when visible.Count > 0:
                return Move(visible[position <= 0 ? visible.Count - 1 : position - 1]);

            case ConsoleKey.DownArrow or ConsoleKey.RightArrow or ConsoleKey.Tab when visible.Count > 0:
                return Move(visible[position < 0 || position == visible.Count - 1 ? 0 : position + 1]);

            case ConsoleKey.Home when visible.Count > 0:
                return Move(visible[0]);

            case ConsoleKey.End when visible.Count > 0:
                return Move(visible[^1]);

            case ConsoleKey.PageUp when visible.Count > 0:
                return Move(visible[Math.Max(position - Page, 0)]);

            case ConsoleKey.PageDown when visible.Count > 0:
                return Move(visible[Math.Min(Math.Max(position, 0) + Page, visible.Count - 1)]);

            case ConsoleKey.Spacebar when AllowsSeveral:
                if (position < 0)
                    return KeyOutcome.Ignored;
                _ticked[_cursor] = !_ticked[_cursor];
                _error = null;
                return KeyOutcome.Changed;

            case ConsoleKey.Enter:
                if (!AllowsSeveral && position < 0)
                {
                    _error = FrameworkText.Format(FrameworkText.Current.NoMatch, _filter);
                    return KeyOutcome.Changed;
                }

                if (Required && SelectedIndices().Count == 0)
                {
                    _error = FrameworkText.Current.Required;
                    return KeyOutcome.Changed;
                }

                return KeyOutcome.Confirmed;

            case ConsoleKey.Backspace when AllowsFilter && _filter.Length > 0:
                return SetFilter(_filter[..^1]);

            case ConsoleKey.Escape when AllowsFilter && _filter.Length > 0:
                return SetFilter("");
        }

        if (AllowsFilter)
            return key.KeyChar >= ' ' && !char.IsControl(key.KeyChar) ? SetFilter(_filter + key.KeyChar) : KeyOutcome.Ignored;

        if (key.KeyChar is >= '1' and <= '9' && key.KeyChar - '1' < _choices.Count)
            return Move(key.KeyChar - '1');

        return KeyOutcome.Ignored;
    }

    /// <summary>The rows PageUp and PageDown move by.</summary>
    private int Page => VisibleRows > 0 ? VisibleRows : 10;

    /// <summary>Narrows the choices; the highlight moves to the first match when the current one no longer matches.</summary>
    private KeyOutcome SetFilter(string filter)
    {
        _filter = filter;
        _error = null;
        var visible = Matching(filter);
        if (visible.Count > 0 && !visible.Contains(_cursor))
            _cursor = visible[0];
        return KeyOutcome.Changed;
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
    private List<int> DefaultIndices() =>
        DefaultTokens().Select(FindValue).Where(i => i >= 0).Distinct().Order().ToList();

    /// <summary>The values <see cref="Default"/> names: one, or several separated by commas when several answers are allowed.</summary>
    private IEnumerable<string> DefaultTokens()
    {
        if (string.IsNullOrWhiteSpace(Default))
            return [];

        var tokens = AllowsSeveral ? Default.Split(ValueSeparator) : [Default];
        return tokens.Select(t => t.Trim()).Where(t => t.Length > 0);
    }

    /// <summary>The choice whose value is <paramref name="value"/>, ignoring case; -1 when none is.</summary>
    private int FindValue(string value) =>
        _choices.FindIndex(c => string.Equals(c.EffectiveValue, value, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------ layout

    private IReadOnlyList<RenderedLine> NumberedLines(int width)
    {
        var items = _choices.Select((c, i) => (Marker: (i + 1).ToString(CultureInfo.InvariantCulture) + ")", Choice: c, Bold: false)).ToList();
        return ChoiceLines(width, items, hint: null, markerStyle: TextStyle.Plain, indent: LabelIndent + 2);
    }

    private IReadOnlyList<RenderedLine> KeyLines(int width)
    {
        var markerStyle = TextStyle.Plain.WithForeground(HighlightColour).WithDecoration(TextDecoration.Bold);
        var visible = Matching(_filter);

        // A window of VisibleRows choices that keeps the highlight in view
        var first = 0;
        var count = visible.Count;
        if (VisibleRows > 0 && visible.Count > VisibleRows)
        {
            var position = Math.Max(visible.IndexOf(_cursor), 0);
            first = Math.Clamp(position - VisibleRows / 2, 0, visible.Count - VisibleRows);
            count = VisibleRows;
        }

        var items = visible.Skip(first).Take(count).Select(i =>
        {
            var marker = Horizontal
                ? (i == _cursor ? "(*)" : "( )")
                : (i == _cursor ? HighlightMarker : " ") + (AllowsSeveral ? (_ticked[i] ? " [x]" : " [ ]") : "");
            return (Marker: marker, Choice: _choices[i], Bold: i == _cursor, Index: i);
        }).ToList();

        var words = FrameworkText.Current;
        var hint = (Horizontal, AllowsFilter, AllowsSeveral) switch
        {
            (true, _, _) => words.RadioKeys,
            (false, true, true) => words.FilterManyKeys,
            (false, true, false) => words.FilterKeys,
            (false, false, true) => words.SelectManyKeys,
            _ => words.SelectKeys
        };
        _choiceOfLine.Clear();
        _choicePlaces.Clear();
        var lines = ChoiceLines(width, items.Select(i => (i.Marker, i.Choice, i.Bold)).ToList(), hint, markerStyle,
            indent: LabelIndent + 1, choiceIndices: items.Select(i => i.Index).ToList()).ToList();

        var dim = TextStyle.Plain.WithDecoration(TextDecoration.Dim);
        var heading = Horizontal ? 0 : HeadingLines(width, hint).Count;
        if (first > 0)
            lines.Insert(heading, new RenderedLine([Span.Spaces(LabelIndent + 3), new Span(FrameworkText.Format(words.MoreAbove, first), dim)]).Clip(width));
        if (first + count < visible.Count)
            lines.Add(new RenderedLine([Span.Spaces(LabelIndent + 3), new Span(FrameworkText.Format(words.MoreBelow, visible.Count - first - count), dim)]).Clip(width));

        if (AllowsFilter && _filter.Length > 0)
        {
            var filter = TextSanitiser.Sanitise(_filter);
            lines.Insert(Math.Min(heading, lines.Count), new RenderedLine(
                [Span.Spaces(LabelIndent + 1), new Span(words.FilterLabel, dim), new Span(filter, TextStyle.Plain.WithDecoration(TextDecoration.Bold))]).Clip(width));
            if (visible.Count == 0)
                lines.Add(new RenderedLine([Span.Spaces(LabelIndent + 3), new Span(words.NoMatches, dim)]).Clip(width));
        }

        if (_error is not null)
        {
            var error = TextStyle.Plain.WithForeground(Colour.Red);
            lines.AddRange(TextWrapper.Wrap(_error, Math.Max(width - LabelIndent, 1))
                .Select(row => new RenderedLine([Span.Spaces(LabelIndent), new Span(row, error)]).Clip(width)));
        }

        MapChoiceLines(lines);
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
        string? hint, TextStyle markerStyle, int indent, IReadOnlyList<int>? choiceIndices = null)
    {
        var hintStyle = TextStyle.Plain.WithDecoration(TextDecoration.Dim);

        if (Horizontal)
        {
            // One space after the label, two between choices
            var segments = new List<(Span[] Spans, int Gap, int Choice)>();
            if (SafeLabel.Length > 0)
                segments.Add(([new Span(SafeLabel, LabelStyle)], 0, -1));

            for (var item = 0; item < items.Count; item++)
            {
                var (marker, choice, bold) = items[item];
                var style = bold ? choice.Style.WithDecoration(TextDecoration.Bold) : choice.Style;
                var gap = segments.Count == 0 ? 0 : segments.Count == 1 && SafeLabel.Length > 0 ? 1 : 2;
                segments.Add(([new Span(marker, markerStyle), Span.Spaces(1), new Span(TextSanitiser.Sanitise(choice.LabelText), style)], gap,
                    choiceIndices?[item] ?? -1));
            }

            if (hint is not null)
                segments.Add(([new Span(hint, hintStyle)], 2, -1));

            return Flow(segments, width, _choicePlaces);
        }

        var lines = HeadingLines(width, hint);

        for (var item = 0; item < items.Count; item++)
        {
            var (marker, choice, bold) = items[item];
            var prefixWidth = indent + TextWidth.Of(marker) + 1;
            var labelLines = choice.Layout(Math.Max(width - prefixWidth, 1), bold ? TextDecoration.Bold : TextDecoration.None);

            for (var i = 0; i < labelLines.Count; i++)
            {
                IEnumerable<Span> prefix = i == 0
                    ? [Span.Spaces(indent), new Span(marker, markerStyle), Span.Spaces(1)]
                    : [Span.Spaces(prefixWidth)];

                var line = labelLines[i].Surround(prefix, []).Clip(width);
                lines.Add(line);
                if (choiceIndices is not null)
                    _choiceOfLine[line] = choiceIndices[item];
            }
        }

        return lines;
    }

    /// <summary>The label of a vertical field, followed by the keys to press when there is a hint.</summary>
    private List<RenderedLine> HeadingLines(int width, string? hint)
    {
        var heading = new List<(Span[] Spans, int Gap, int Choice)>();
        if (SafeLabel.Length > 0)
            heading.Add(([new Span(SafeLabel, LabelStyle)], 0, -1));
        if (hint is not null)
            heading.Add(([new Span(hint, TextStyle.Plain.WithDecoration(TextDecoration.Dim))], 1, -1));
        return heading.Count == 0 ? [] : Flow(heading, width, []);
    }

    /// <summary>
    /// Lays segments out side by side, starting a new line when the next one does not fit. A segment wider than a whole
    /// line (a long choice on a narrow terminal) wraps onto the lines below, under its own text, rather than being cut.
    /// </summary>
    /// <param name="segments">The label, the choices and the hint, each with the gap before it and its choice (or -1).</param>
    /// <param name="width">The width of the lines.</param>
    /// <param name="places">Receives where each choice was put: its lines and columns.</param>
    private static List<RenderedLine> Flow(List<(Span[] Spans, int Gap, int Choice)> segments, int width,
        List<(int Line, int From, int To, int Choice)> places)
    {
        var lines = new List<RenderedLine>();
        var current = new List<Span>();
        var used = 0;

        void NewLine()
        {
            lines.Add(new RenderedLine(current).Clip(width));
            current = [];
            used = 0;
        }

        foreach (var (segment, gap, choice) in segments)
        {
            var segmentWidth = segment.Sum(s => s.Width);
            if (current.Count > 0 && used + gap + segmentWidth > width)
                NewLine();

            if (current.Count > 0)
            {
                current.Add(Span.Spaces(gap));
                used += gap;
            }

            if (used + segmentWidth <= width)
            {
                if (choice >= 0)
                    places.Add((lines.Count, used, used + segmentWidth, choice));
                current.AddRange(segment);
                used += segmentWidth;
                continue;
            }

            // Too wide for any line: the marker and as many words as fit, then the rest under the text, line by line
            var prefix = segment.Take(segment.Length - 1).ToArray();
            var last = segment[^1];
            var indent = used + prefix.Sum(s => s.Width);
            var pieces = TextWrapper.Wrap(last.Text, Math.Max(width - indent, 1));
            for (var i = 0; i < pieces.Count; i++)
            {
                if (i > 0)
                {
                    NewLine();
                    current.Add(Span.Spaces(indent));
                    used = indent;
                }

                var start = i == 0 ? used : indent;
                if (i == 0)
                    current.AddRange(prefix);
                current.Add(last with { Text = pieces[i] });
                used = indent + TextWidth.Of(pieces[i]);
                if (choice >= 0)
                    places.Add((lines.Count, i == 0 ? start : indent, used, choice));
            }
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

    /// <inheritdoc />
    /// <remarks>Only values that are choices are taken, so a value posted from code that is not one cannot break the field.</remarks>
    void IPrefilledField.Prefill(string posted)
    {
        var values = (AllowsSeveral ? posted.Split(ValueSeparator) : [posted]).Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
        if (values.Count > 0 && values.All(v => _choices.Any(c => string.Equals(c.EffectiveValue, v, StringComparison.OrdinalIgnoreCase))))
            Default = string.Join(ValueSeparator, values);
    }
}
