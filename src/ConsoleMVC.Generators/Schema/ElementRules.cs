#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Emit;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// Checks literal attribute values against each other and against the element's children, with the same rules the
/// runtime element applies, so a value that would make the element throw (or never accept an empty answer) is a build
/// error at the attribute instead: a <c>&lt;number&gt;</c> step that is not positive, a <c>min</c> above <c>max</c>, a
/// default the field would not accept, a duplicate menu key or choice value, and a box narrower than a box can be.
/// </summary>
/// <remarks>
/// Only literal values are checked. A value written as an expression, or children produced by C# code such as
/// <c>@foreach</c>, are left to the runtime, which applies the same rules when the view runs.
/// </remarks>
/// <example><code>ElementRules.Check(element, diagnostics); // CVW101 for &lt;number step="0" /&gt;</code></example>
internal static class ElementRules
{
    /// <summary>The separator of several answers, as in <c>ChoiceFieldElement.ValueSeparator</c>.</summary>
    private const char ValueSeparator = ',';

    private const NumberStyles NumberStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    /// <summary>
    /// Reports the broken rules of <paramref name="element"/>, whose children have been parsed.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="diagnostics">Receives the errors.</param>
    /// <example><code>ElementRules.Check(menu, diagnostics); // CVW101 at the second of two options with key "q"</code></example>
    public static void Check(ElementNode element, List<ParseDiagnostic> diagnostics)
    {
        switch (element.Schema.Tag)
        {
            case "number":
                CheckNumber(element, diagnostics);
                break;
            case "date":
                CheckDate(element, diagnostics);
                break;
            case "select":
            case "radio":
                CheckChoices(element, diagnostics);
                break;
            case "menu":
                CheckMenu(element, diagnostics);
                break;
            case "option":
                CheckOption(element, diagnostics);
                break;
            case "box":
                CheckBox(element, diagnostics);
                break;
        }
    }

    // ------------------------------------------------------------------ <number>

    /// <summary>As <c>NumberElement</c>: a positive step, min not above max, and a default that <c>Validate</c> accepts.</summary>
    private static void CheckNumber(ElementNode element, List<ParseDiagnostic> diagnostics)
    {
        var step = Find(element, "Step");
        if (Number(step) is { } stepValue && stepValue <= 0)
            Report(diagnostics, step!, "the step must be more than 0, as the arrow keys change the value by it");

        var minAttribute = Find(element, "Min");
        var maxAttribute = Find(element, "Max");
        var min = Number(minAttribute);
        var max = Number(maxAttribute);
        if (min > max)
            Report(diagnostics, minAttribute!, $"'min' is greater than 'max' ({Text(maxAttribute!)}), so no number could be accepted");

        var defaultAttribute = Find(element, "Default");
        var fallback = Literal(defaultAttribute)?.Trim();
        if (string.IsNullOrEmpty(fallback))
            return;

        if (!decimal.TryParse(fallback, NumberStyle, CultureInfo.InvariantCulture, out var value))
        {
            Report(diagnostics, defaultAttribute!, "the default must be a number such as 42 or 2.5, because an empty answer takes it");
            return;
        }

        var decimalsAttribute = Find(element, "Decimals");
        if (Literal(decimalsAttribute) is { } decimalsText
            && int.TryParse(decimalsText.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var places))
        {
            if (places == 0 && value != decimal.Truncate(value))
                Report(diagnostics, defaultAttribute!, "the default must be a whole number, because 'decimals' is 0");
            else if (places > 0 && value != Math.Round(value, places))
                Report(diagnostics, defaultAttribute!, $"the default has more than {places} decimal place{(places == 1 ? "" : "s")}, which 'decimals' allows");
        }

        if (value < min || value > max)
        {
            var range = (min, max) switch
            {
                ({ }, { }) => $"from {Text(minAttribute!)} to {Text(maxAttribute!)}",
                ({ }, null) => $"at least {Text(minAttribute!)}",
                _ => $"at most {Text(maxAttribute!)}"
            };
            Report(diagnostics, defaultAttribute!, $"the default must be {range}, the range the field accepts");
        }
    }

    // ------------------------------------------------------------------ <date>

    /// <summary>As <c>DateElement</c>: min not after max, when both are fixed dates or both are relative to today.</summary>
    private static void CheckDate(ElementNode element, List<ParseDiagnostic> diagnostics)
    {
        var minAttribute = Find(element, "Min");
        var maxAttribute = Find(element, "Max");
        if (Literal(minAttribute) is not { } minText || Literal(maxAttribute) is not { } maxText
            || !DateAttributeType.TryParse(minText, out var minDays, out var minDate)
            || !DateAttributeType.TryParse(maxText, out var maxDays, out var maxDate))
            return;

        // A fixed date and a relative one compare differently from day to day, so only like with like
        if (minDays > maxDays || minDate > maxDate)
            Report(diagnostics, minAttribute!, $"'min' is after 'max' ({maxText.Trim()}), so no date could be accepted");
    }

    // ------------------------------------------------------------------ <select>, <radio>

    /// <summary>
    /// As <c>ChoiceFieldElement</c>: at least one choice, no two with the same value (ignoring case), no comma in a value
    /// when several answers are allowed, and a default that names choices.
    /// </summary>
    private static void CheckChoices(ElementNode element, List<ParseDiagnostic> diagnostics)
    {
        var tag = element.Schema.Tag;
        if (element.Children.Count == 0)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidElement, TagSpan(element),
                $"<{tag}> needs at least one <choice>, e.g. <choice value=\"red\">Red</choice>"));
            return;
        }

        // Choices from C# code (@foreach, @if) may be conditional or repeated: the runtime checks them
        if (element.Children.Any(c => c is not ElementNode))
            return;

        var multipleAttribute = Find(element, "Multiple");
        bool? multiple = multipleAttribute is null ? false : multipleAttribute.LiteralCode is { } flag ? flag == "true" : null;

        var values = new List<string>();
        var allKnown = true;
        foreach (var choice in element.Children.OfType<ElementNode>())
        {
            var valueAttribute = Find(choice, "Value");
            var value = valueAttribute is null ? LabelText(choice) : Literal(valueAttribute);
            if (value is null)
            {
                allKnown = false;
                continue;
            }

            if (values.Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)))
            {
                if (valueAttribute is not null)
                    Report(diagnostics, valueAttribute, $"an earlier choice already has the value '{value}' (values are compared ignoring case)");
                else
                    diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidElement, TagSpan(choice),
                        $"<choice> '{value}' has the same text as an earlier choice, and a choice without a value posts its text; give one of them a value=\"...\""));
            }
            else if (multiple == true && value.IndexOf(ValueSeparator) >= 0)
            {
                if (valueAttribute is not null)
                    Report(diagnostics, valueAttribute, $"<{tag} multiple> separates its answers with commas, so a value cannot contain one");
                else
                    diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidElement, TagSpan(choice),
                        $"<choice> '{value}' posts its text, which contains a comma, but <{tag} multiple> separates its answers with commas; give it a value=\"...\""));
            }

            values.Add(value);
        }

        var defaultAttribute = Find(element, "Default");
        if (!allKnown || multiple is null || Literal(defaultAttribute) is not { } fallback || fallback.Trim().Length == 0)
            return;

        var tokens = multiple.Value ? fallback.Split(ValueSeparator) : [fallback];
        foreach (var token in tokens.Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            if (values.Any(v => string.Equals(v, token, StringComparison.OrdinalIgnoreCase)))
                continue;

            Report(diagnostics, defaultAttribute!,
                $"'{token}' is not the value of any choice, so an empty answer could not take it (values: {string.Join(", ", values)})"
                + (!multiple.Value && fallback.IndexOf(ValueSeparator) >= 0 ? $"; only <select multiple> takes several values" : ""));
            return;
        }
    }

    /// <summary>
    /// The text a choice without a value posts (<c>ChoiceElement.LabelText</c>), or <see langword="null"/> when it is
    /// not known at build time (it contains an expression) or the runtime would change it (control characters).
    /// </summary>
    private static string? LabelText(ElementNode choice)
    {
        var text = new StringBuilder();
        foreach (var item in ViewEmitter.NormaliseInline(choice.Children))
        {
            switch (item)
            {
                case TextNode node:
                    text.Append(node.Text);
                    break;
                case ElementNode lineBreak:
                    var count = lineBreak.Attributes.FirstOrDefault();
                    if (count is null)
                        text.Append('\n');
                    else if (Literal(count) is { } repeat && int.TryParse(repeat.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var times))
                        text.Append('\n', Math.Max(times, 0));
                    else
                        return null;
                    break;
                default:
                    return null;
            }
        }

        return Sanitised(text.ToString())?.Replace('\n', ' ').Trim();
    }

    // ------------------------------------------------------------------ <menu>, <option>

    /// <summary>As <c>MenuElement.Add</c>: no two options with the same key, ignoring case and surrounding spaces.</summary>
    private static void CheckMenu(ElementNode element, List<ParseDiagnostic> diagnostics)
    {
        // Options from C# code may be conditional or repeated: the runtime checks them
        if (element.Children.Any(c => c is not ElementNode))
            return;

        var keys = new List<string>();
        foreach (var option in element.Children.OfType<ElementNode>())
        {
            var keyAttribute = Find(option, "Key");
            if (MenuKey(keyAttribute) is not { Length: > 0 } key)
                continue;

            if (keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                Report(diagnostics, keyAttribute!, $"an earlier option already has the key '{key}' (keys are compared ignoring case)");

            keys.Add(key);
        }
    }

    /// <summary>As <c>MenuElement.Add</c>: an option's key cannot be empty.</summary>
    private static void CheckOption(ElementNode element, List<ParseDiagnostic> diagnostics)
    {
        var keyAttribute = Find(element, "Key");
        if (MenuKey(keyAttribute) is { Length: 0 })
            Report(diagnostics, keyAttribute!, "the key is what the user types to choose the option, so it cannot be empty");
    }

    /// <summary>The key as <c>MenuElement</c> compares it, or <see langword="null"/> when it is not a literal.</summary>
    private static string? MenuKey(AttributeNode? key) =>
        Literal(key) is { } text ? Sanitised(text)?.Replace('\n', ' ').Trim() : null;

    // ------------------------------------------------------------------ <box>

    /// <summary>
    /// As <c>BoxElement.Layout</c>: a box is never narrower than its border and one column of content (three columns,
    /// or one without a border), so a literal width or max-width below that is not the width it draws.
    /// </summary>
    private static void CheckBox(ElementNode element, List<ParseDiagnostic> diagnostics)
    {
        var border = Find(element, "Border");
        var themed = Find(element, ElementSchema.ThemeClassProperty) is not null;
        var smallest = border is null
            ? themed ? 1 : 3 // a theme class may remove the border (or widen the box, see below)
            : border.LiteralCode is { } style ? style.EndsWith(".None", StringComparison.Ordinal) ? 1 : 3 : 1;
        var reason = smallest == 3 ? "two columns of border and one of content" : "one column of content";

        int? widest = null;
        AttributeNode? narrowing = null;

        var width = Find(element, "Width");
        if (Literal(width)?.Trim() is { } widthText)
        {
            // 0% is no columns at any terminal width; other percentages depend on it
            var fixedText = widthText.EndsWith("%", StringComparison.Ordinal) ? widthText.TrimEnd('%') : widthText;
            if (int.TryParse(fixedText, NumberStyles.None, CultureInfo.InvariantCulture, out var columns) && (fixedText == widthText || columns == 0))
            {
                widest = columns;
                narrowing = width;
            }
        }

        var maxWidth = Find(element, "MaxWidth");
        if (Literal(maxWidth) is { } maxText && int.TryParse(maxText.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var maxColumns)
            && (widest is null || maxColumns < widest))
        {
            widest = maxColumns;
            narrowing = maxWidth;
        }

        if (widest is null)
            return;

        // min-width is applied last, so it widens the box again; a theme class may set one
        var minWidth = Find(element, "MinWidth");
        if (minWidth is null && themed)
            return;

        if (minWidth is not null)
        {
            if (Literal(minWidth) is not { } minText || !int.TryParse(minText.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var minColumns))
                return;

            widest = Math.Max(widest.Value, minColumns);
        }

        if (widest < smallest)
            Report(diagnostics, narrowing!, $"a box is at least {smallest} column{(smallest == 1 ? "" : "s")} wide ({reason}), so it would not be drawn this narrow");
    }

    // ------------------------------------------------------------------ helpers

    private static AttributeNode? Find(ElementNode element, string property) =>
        element.Attributes.FirstOrDefault(a => a.Schema.Property == property);

    /// <summary>The literal text of an attribute, or <see langword="null"/> when it is missing or contains an expression.</summary>
    private static string? Literal(AttributeNode? attribute) =>
        attribute?.LiteralCode is null ? null : string.Concat(attribute.Parts.Select(p => p.Text));

    private static decimal? Number(AttributeNode? attribute) =>
        Literal(attribute) is { } text && decimal.TryParse(text.Trim(), NumberStyle, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string Text(AttributeNode attribute) => Literal(attribute)!.Trim();

    /// <summary>
    /// The text unchanged when <c>TextSanitiser</c> would leave it so; otherwise <see langword="null"/>, as the checks
    /// cannot be sure what the runtime compares.
    /// </summary>
    private static string? Sanitised(string text) =>
        text.Any(c => c is '\r' or '\t' || (c != '\n' && (c < ' ' || c is >= '\u007f' and <= '\u009f' || c is >= '‪' and <= '‮' || c is >= '⁦' and <= '⁩')))
            ? null
            : text;

    private static SourceSpan TagSpan(ElementNode element) => new(element.Span.Start, element.Schema.Tag.Length + 1);

    private static void Report(List<ParseDiagnostic> diagnostics, AttributeNode attribute, string problem)
    {
        var parts = attribute.Parts;
        var span = parts.Count > 0 ? SourceSpan.FromBounds(parts[0].Span.Start, parts[parts.Count - 1].Span.End) : attribute.Span;
        diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidAttributeValue, span, string.Concat(parts.Select(p => p.Text)),
            attribute.Schema.Name, problem));
    }
}
