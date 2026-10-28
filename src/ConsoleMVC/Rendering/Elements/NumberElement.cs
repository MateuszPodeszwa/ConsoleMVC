using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// A number field: the <c>&lt;number&gt;</c> markup element. The answer is checked against <see cref="Min"/>,
/// <see cref="Max"/> and <see cref="Decimals"/> and stored in the invariant culture (<c>2.5</c>), so it binds to
/// <see cref="int"/>, <see cref="decimal"/>, <see cref="double"/> and the other number types.
/// </summary>
/// <remarks>
/// In a terminal, Up and Down change the value by <see cref="Step"/> (PageUp and PageDown by ten steps) and digits
/// replace it; Enter confirms. Otherwise the number is typed after the label. Numbers use a dot for decimals.
/// </remarks>
/// <example>
/// <code>
/// var age = new NumberElement { Name = "Age", Label = "Age: ", Min = 0, Max = 130, Decimals = 0, Default = "30" };
/// string? answer = age.Read(renderer, ConsoleTextInput.Instance); // e.g. "42"
/// // Arrow keys: Age: 30  (Up/Down, type a number, Enter)
/// </code>
/// </example>
public sealed class NumberElement : Element, IFormField
{
    /// <summary>The message for text that is not a number.</summary>
    public const string NotANumberMessage = "Enter a number, e.g. 42 or 2.5.";

    /// <summary>The message for a number with a fractional part where <see cref="Decimals"/> is 0.</summary>
    public const string WholeNumberMessage = "Enter a whole number.";

    private const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    // The state shown by Layout while keys are read
    private FieldMode _mode = FieldMode.Static;
    private string _buffer = "";
    private bool _fresh;
    private string? _error;

    /// <summary>Gets or sets the form key the answer is stored under.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the text written before the number, e.g. <c>"Age: "</c>.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the value used when the user enters nothing, and the starting value in arrow-key mode.</summary>
    public string? Default { get; set; }

    /// <summary>Gets or sets the smallest accepted value, or <see langword="null"/> for none.</summary>
    public decimal? Min { get; set; }

    /// <summary>Gets or sets the largest accepted value, or <see langword="null"/> for none.</summary>
    public decimal? Max { get; set; }

    /// <summary>Gets or sets how much Up and Down change the value. Must be positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not positive.</exception>
    public decimal Step
    {
        get => _step;
        set => _step = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Step must be positive.");
    }

    private decimal _step = 1;

    /// <summary>Gets or sets the most decimal places accepted (0 for whole numbers), or <see langword="null"/> for any.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative or above 28.</exception>
    public int? Decimals
    {
        get => _decimals;
        set => _decimals = value is null or (>= 0 and <= 28) ? value : throw new ArgumentOutOfRangeException(nameof(value), "Decimals must be from 0 to 28.");
    }

    private int? _decimals;

    /// <summary>Gets or sets whether an empty answer is rejected and the field asked again.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets the colour of the label.</summary>
    public Colour LabelColour { get; set; }

    /// <summary>Gets or sets the colour of the value while it is edited with the keys.</summary>
    public Colour HighlightColour { get; set; } = Colour.Cyan;

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        var limit = Math.Max(maxWidth, 0);
        return Math.Min(Layout(Math.Max(limit, 1)).Max(l => l.Width), limit);
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        width = Math.Max(width, 1);
        var label = FieldText.SafeLabel(Label);
        var spans = new List<Span> { new(label.Length > 0 ? label + (label.EndsWith(' ') ? "" : " ") : "", LabelStyle) };

        switch (_mode)
        {
            case FieldMode.Keys:
                spans.Add(new Span(_buffer.Length > 0 ? _buffer : " ", TextStyle.Plain.WithForeground(HighlightColour).WithDecoration(TextDecoration.Bold | TextDecoration.Underline)));
                spans.Add(Span.Spaces(2));
                spans.Add(new Span(FrameworkText.Current.NumberKeys, FieldText.Hint));
                break;
            case FieldMode.Answered:
                spans.Add(new Span(_buffer, TextStyle.Plain.WithDecoration(TextDecoration.Bold)));
                break;
        }

        var lines = new List<RenderedLine> { new RenderedLine(spans).Clip(width) };
        if (_mode == FieldMode.Keys && _error is not null)
            lines.AddRange(FieldText.ErrorLines(_error, FieldText.Indent(label), width));
        return lines;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Min"/> is greater than <see cref="Max"/>.</exception>
    public string? Read(IMarkupRenderer renderer, ITextInput input)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        if (Min > Max)
            throw new InvalidOperationException($"<number> '{Name}' has min {Format(Min!.Value)} greater than max {Format(Max!.Value)}.");

        _error = null;
        return input.CanReadKeys && renderer.CanRewrite ? ReadWithKeys(renderer, input) : ReadTyped(renderer, input);
    }

    /// <summary>
    /// Checks a typed number.
    /// </summary>
    /// <param name="text">The text, e.g. <c>"2.5"</c>.</param>
    /// <param name="value">The number when valid.</param>
    /// <returns>An error message, or <see langword="null"/> when the number is valid.</returns>
    /// <example><code>var error = field.Validate("200", out _); // "Enter a number from 0 to 130."</code></example>
    public string? Validate(string text, out decimal value)
    {
        // A whole-number field asks for a whole number, not for "42 or 2.5"
        if (!decimal.TryParse(text, Styles, CultureInfo.InvariantCulture, out value))
            return Decimals == 0 ? FrameworkText.Current.WholeNumber : FrameworkText.Current.NotANumber;

        if (Decimals == 0 && value != decimal.Truncate(value))
            return FrameworkText.Current.WholeNumber;

        if (Decimals is > 0 and var places && value != Math.Round(value, places))
            return places == 1 ? FrameworkText.Current.DecimalPlace : FrameworkText.Format(FrameworkText.Current.DecimalPlaces, places);

        if (value < Min || value > Max)
        {
            return (Min, Max) switch
            {
                ({ } min, { } max) => FrameworkText.Format(FrameworkText.Current.NumberRange, Format(min), Format(max)),
                ({ } min, null) => FrameworkText.Format(FrameworkText.Current.NumberAtLeast, Format(min)),
                (null, { } max) => FrameworkText.Format(FrameworkText.Current.NumberAtMost, Format(max)),
                _ => null
            };
        }

        return null;
    }

    private string? ReadTyped(IMarkupRenderer renderer, ITextInput input)
    {
        _mode = FieldMode.Static;
        var label = FieldText.SafeLabel(Label);
        var prompt = label.Length > 0 && !label.EndsWith(' ') ? label + " " : label;

        while (true)
        {
            if (prompt.Length > 0)
                renderer.WritePrompt(prompt, LabelStyle);

            var line = input.ReadLine();
            if (line is null)
                return null;

            renderer.RecordInput(line);

            var text = line.Trim();
            if (text.Length == 0)
                text = Default?.Trim() ?? "";

            if (text.Length == 0)
            {
                if (!Required)
                    return "";

                FieldText.ShowError(renderer, FrameworkText.Current.Required);
                continue;
            }

            var error = Validate(text, out var value);
            if (error is null)
                return Format(value);

            FieldText.ShowError(renderer, error);
        }
    }

    private string? ReadWithKeys(IMarkupRenderer renderer, ITextInput input)
    {
        _buffer = Default?.Trim() ?? "";
        _fresh = true;
        _mode = FieldMode.Keys;
        renderer.Render(this);

        // Esc restores a changed value; when the value is the default, it is left to an application shortcut
        var start = _buffer;
        using var claim = FieldKeyClaim.Claim(key => key.Key == ConsoleKey.Escape && _buffer != start);

        while (true)
        {
            var read = input.ReadKey();
            if (read is not { } key)
                return null;

            if (key.Key == ConsoleKey.Enter)
            {
                if (_buffer.Length == 0)
                {
                    if (!Required)
                        return Answer("");
                    _error = FrameworkText.Current.Required;
                }
                else
                {
                    _error = Validate(_buffer, out var value);
                    if (_error is null)
                        return Answer(Format(value));
                }

                renderer.Rewrite(this);
                continue;
            }

            if (!Edit(key))
                continue;

            _error = null;
            renderer.Rewrite(this);
        }

        string Answer(string value)
        {
            _buffer = value;
            _mode = FieldMode.Answered;
            renderer.Rewrite(this);
            return value;
        }
    }

    /// <summary>Applies a key to the value being edited; returns whether it changed.</summary>
    private bool Edit(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                return Change(Step);
            case ConsoleKey.DownArrow:
                return Change(-Step);
            case ConsoleKey.PageUp:
                return Change(Step * 10);
            case ConsoleKey.PageDown:
                return Change(-Step * 10);
            case ConsoleKey.Backspace:
                if (_buffer.Length == 0)
                    return false;
                _buffer = _buffer[..^1];
                _fresh = false;
                return true;
            case ConsoleKey.Escape:
                _buffer = Default?.Trim() ?? "";
                _fresh = true;
                return true;
        }

        var c = key.KeyChar;
        if (!char.IsAsciiDigit(c) && c != '.' && c != '-')
            return false;

        // The first character typed replaces the starting value, as in most number fields
        _buffer = (_fresh ? "" : _buffer) + c;
        _fresh = false;
        return true;
    }

    /// <summary>Steps the value, starting from the value shown (or the minimum, or 0), and keeps it in range.</summary>
    private bool Change(decimal by)
    {
        var start = decimal.TryParse(_buffer, Styles, CultureInfo.InvariantCulture, out var current) ? current : Min ?? 0;
        var next = _buffer.Length == 0 && Min is not null ? Min.Value : start + by;
        if (Min is { } min && next < min)
            next = min;
        if (Max is { } max && next > max)
            next = max;

        var text = Format(next);
        if (text == _buffer)
            return false;

        _buffer = text;
        _fresh = true;
        return true;
    }

    /// <summary>The number as stored in the form: invariant culture, without trailing zeros.</summary>
    private static string Format(decimal value) => (value / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    private TextStyle LabelStyle => TextStyle.Plain.WithForeground(LabelColour);

    private enum FieldMode
    {
        Static,
        Keys,
        Answered
    }
}
