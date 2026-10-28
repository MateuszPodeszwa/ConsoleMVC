using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// A date field: the <c>&lt;date&gt;</c> markup element. The answer is stored as <c>yyyy-MM-dd</c>, which binds to
/// <see cref="DateOnly"/> and <see cref="DateTime"/>.
/// </summary>
/// <remarks>
/// <para>
/// In a terminal, Left and Right pick the year, month or day, Up and Down change it (PageUp and PageDown by a larger
/// amount), <c>t</c> goes to today and Enter confirms; the date always stays within <see cref="Min"/> and
/// <see cref="Max"/>. Otherwise the user types <c>2026-10-27</c>, <c>today</c>, <c>tomorrow</c>, <c>yesterday</c> or
/// a number of days such as <c>+7</c> or <c>-1</c>.
/// </para>
/// <para>
/// <see cref="Default"/>, <see cref="Min"/> and <see cref="Max"/> take the same forms: a date, <c>today</c>, or
/// <c>today+N</c> / <c>today-N</c> days.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var due = new DateElement { Name = "Due", Label = "Due: ", Default = "today+7", Min = "today" };
/// string? answer = due.Read(renderer, ConsoleTextInput.Instance); // e.g. "2026-11-03"
/// // Arrow keys: Due: 2026-11-03  (Left/Right, Up/Down, t today, Enter)
/// </code>
/// </example>
public sealed class DateElement : Element, IFormField, IPrefilledField
{
    /// <summary>The form of the stored answer.</summary>
    public const string ValueFormat = "yyyy-MM-dd";

    /// <summary>The message for text that is not a date.</summary>
    public const string NotADateMessage = "Enter a date as yyyy-mm-dd (e.g. 2026-10-27), today, tomorrow or +7.";

    // The state shown by Layout while keys are read
    private FieldMode _mode = FieldMode.Static;
    private DateOnly _date;
    private DatePart _part = DatePart.Day;

    /// <summary>Gets or sets the form key the answer is stored under.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the text written before the date, e.g. <c>"Due: "</c>.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the date used when the user enters nothing, and the starting date in arrow-key mode:
    /// <c>2026-10-27</c>, <c>today</c> or <c>today+N</c>. Arrow-key mode starts today when it is not set.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>Gets or sets the earliest accepted date (<c>2026-01-01</c>, <c>today</c>, <c>today-30</c>), or <see langword="null"/>.</summary>
    public string? Min { get; set; }

    /// <summary>Gets or sets the latest accepted date, or <see langword="null"/>.</summary>
    public string? Max { get; set; }

    /// <summary>Gets or sets whether an empty answer is rejected and the field asked again.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets the colour of the label.</summary>
    public Colour LabelColour { get; set; }

    /// <summary>Gets or sets the colour of the part being changed in arrow-key mode.</summary>
    public Colour HighlightColour { get; set; } = Colour.Cyan;

    /// <summary>Gets or sets the clock that decides what <c>today</c> is (replace it in tests).</summary>
    /// <example><code>field.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero));</code></example>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>
    /// Reads a date written as <c>2026-10-27</c>, <c>today</c>, <c>tomorrow</c>, <c>yesterday</c>, <c>today+N</c>,
    /// <c>today-N</c>, <c>+N</c> or <c>-N</c> (days from today).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="today">Today's date.</param>
    /// <param name="date">The date when the text is valid.</param>
    /// <returns><see langword="true"/> when the text is a date.</returns>
    /// <example><code>DateElement.TryParse("today+7", new DateOnly(2026, 10, 27), out var d); // 2026-11-03</code></example>
    public static bool TryParse(string? text, DateOnly today, out DateOnly date)
    {
        date = default;
        var value = (text ?? "").Trim().ToLowerInvariant();

        switch (value)
        {
            case "today":
                date = today;
                return true;
            case "tomorrow":
                date = today.AddDays(1);
                return true;
            case "yesterday":
                date = today.AddDays(-1);
                return true;
        }

        if (value.StartsWith("today", StringComparison.Ordinal))
            value = value["today".Length..];

        if (value.Length > 1 && value[0] is '+' or '-'
            && int.TryParse(value[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days <= 100_000)
        {
            try
            {
                date = today.AddDays(value[0] == '+' ? days : -days);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        return DateOnly.TryParseExact(value, ValueFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

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
                var normal = TextStyle.Plain;
                var current = TextStyle.Plain.WithForeground(HighlightColour).WithDecoration(TextDecoration.Bold | TextDecoration.Underline);
                spans.Add(new Span(_date.Year.ToString("0000", CultureInfo.InvariantCulture), _part == DatePart.Year ? current : normal));
                spans.Add(new Span("-", normal));
                spans.Add(new Span(_date.Month.ToString("00", CultureInfo.InvariantCulture), _part == DatePart.Month ? current : normal));
                spans.Add(new Span("-", normal));
                spans.Add(new Span(_date.Day.ToString("00", CultureInfo.InvariantCulture), _part == DatePart.Day ? current : normal));
                spans.Add(new Span(" " + _date.DayOfWeek.ToString()[..3], FieldText.Hint));
                spans.Add(Span.Spaces(2));
                spans.Add(new Span(FrameworkText.Current.DateKeys, FieldText.Hint));
                break;
            case FieldMode.Answered:
                spans.Add(new Span(_date.ToString(ValueFormat, CultureInfo.InvariantCulture), TextStyle.Plain.WithDecoration(TextDecoration.Bold)));
                break;
        }

        return [new RenderedLine(spans).Clip(width)];
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Default"/>, <see cref="Min"/> or <see cref="Max"/> is not a date, or <see cref="Min"/>
    /// is after <see cref="Max"/>.
    /// </exception>
    public string? Read(IMarkupRenderer renderer, ITextInput input)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        var today = DateOnly.FromDateTime(Clock.GetLocalNow().DateTime);
        var min = Setting(Min, nameof(Min), today);
        var max = Setting(Max, nameof(Max), today);
        var fallback = Setting(Default, nameof(Default), today);
        if (min > max)
            throw new InvalidOperationException($"<date> '{Name}' has min {Format(min!.Value)} after max {Format(max!.Value)}.");

        return input.CanReadKeys && renderer.CanRewrite
            ? ReadWithKeys(renderer, input, today, min, max, fallback)
            : ReadTyped(renderer, input, today, min, max, fallback);
    }

    private string? ReadTyped(IMarkupRenderer renderer, ITextInput input, DateOnly today, DateOnly? min, DateOnly? max, DateOnly? fallback)
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
            DateOnly date;
            if (text.Length == 0)
            {
                if (fallback is { } value)
                    date = value;
                else if (Required)
                {
                    FieldText.ShowError(renderer, FrameworkText.Current.Required);
                    continue;
                }
                else
                    return "";
            }
            else if (!TryParse(text, today, out date))
            {
                FieldText.ShowError(renderer, FrameworkText.Current.NotADate);
                continue;
            }

            var error = RangeError(date, min, max);
            if (error is null)
                return Format(date);

            FieldText.ShowError(renderer, error);
        }
    }

    private string? ReadWithKeys(IMarkupRenderer renderer, ITextInput input, DateOnly today, DateOnly? min, DateOnly? max, DateOnly? fallback)
    {
        _date = Clamp(fallback ?? today, min, max);
        _part = DatePart.Day;
        _mode = FieldMode.Keys;
        renderer.Render(this);

        while (true)
        {
            var read = input.ReadKey();
            if (read is not { } key)
                return null;

            var before = (_date, _part);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    _mode = FieldMode.Answered;
                    renderer.Rewrite(this);
                    return Format(_date);
                case ConsoleKey.LeftArrow:
                    _part = _part == DatePart.Year ? DatePart.Year : _part - 1;
                    break;
                case ConsoleKey.RightArrow or ConsoleKey.Tab:
                    _part = _part == DatePart.Day ? DatePart.Day : _part + 1;
                    break;
                case ConsoleKey.UpArrow:
                    _date = Clamp(Add(_date, _part, 1), min, max);
                    break;
                case ConsoleKey.DownArrow:
                    _date = Clamp(Add(_date, _part, -1), min, max);
                    break;
                case ConsoleKey.PageUp:
                    _date = Clamp(Add(_date, _part, _part == DatePart.Day ? 7 : _part == DatePart.Month ? 12 : 10), min, max);
                    break;
                case ConsoleKey.PageDown:
                    _date = Clamp(Add(_date, _part, _part == DatePart.Day ? -7 : _part == DatePart.Month ? -12 : -10), min, max);
                    break;
                default:
                    if (char.ToLowerInvariant(key.KeyChar) == 't')
                        _date = Clamp(today, min, max);
                    break;
            }

            if ((_date, _part) != before)
                renderer.Rewrite(this);
        }
    }

    /// <summary>Changes one part; a month or year change keeps the day within the new month.</summary>
    private static DateOnly Add(DateOnly date, DatePart part, int amount)
    {
        try
        {
            return part switch
            {
                DatePart.Year => date.AddYears(amount),
                DatePart.Month => date.AddMonths(amount),
                _ => date.AddDays(amount)
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return date; // beyond year 1 or 9999
        }
    }

    private static DateOnly Clamp(DateOnly date, DateOnly? min, DateOnly? max) =>
        min is { } low && date < low ? low : max is { } high && date > high ? high : date;

    private static string? RangeError(DateOnly date, DateOnly? min, DateOnly? max)
    {
        if (min is { } low && date < low)
            return max is { } high ? FrameworkText.Format(FrameworkText.Current.DateRange, Format(low), Format(high)) : FrameworkText.Format(FrameworkText.Current.DateOnOrAfter, Format(low));
        if (max is { } latest && date > latest)
            return min is { } earliest ? FrameworkText.Format(FrameworkText.Current.DateRange, Format(earliest), Format(latest)) : FrameworkText.Format(FrameworkText.Current.DateOnOrBefore, Format(latest));
        return null;
    }

    private DateOnly? Setting(string? value, string name, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!TryParse(value, today, out var date))
            throw new InvalidOperationException($"<date> '{Name}' has {name.ToLowerInvariant()} '{value}', which is not a date. Use yyyy-mm-dd, today or today+N.");

        return date;
    }

    private static string Format(DateOnly date) => date.ToString(ValueFormat, CultureInfo.InvariantCulture);

    private TextStyle LabelStyle => TextStyle.Plain.WithForeground(LabelColour);

    private enum FieldMode
    {
        Static,
        Keys,
        Answered
    }

    private enum DatePart
    {
        Year,
        Month,
        Day
    }

    /// <inheritdoc />
    void IPrefilledField.Prefill(string posted)
    {
        if (DateOnly.TryParseExact(posted.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
            Default = posted.Trim();
    }
}
