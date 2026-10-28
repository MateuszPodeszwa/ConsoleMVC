#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ConsoleMVC.Generators.Emit;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// The value type of a markup attribute. Knows how to validate a literal value at compile time and
/// translate it into C# that constructs the runtime value.
/// </summary>
/// <remarks>
/// Add a subclass to support a new kind of attribute value; register attributes using it in
/// <see cref="MarkupSchema"/>.
/// </remarks>
internal abstract class AttributeType
{
    /// <summary>Gets a short human-readable description, used in error messages (e.g. "a colour").</summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the kind of value, for tools that read the exported schema (<c>schema/cvw-schema.json</c>): <c>text</c>,
    /// <c>integer</c>, <c>number</c>, <c>number-list</c>, <c>date</c>, <c>boolean</c>, <c>colour</c>, <c>length</c>,
    /// <c>choice</c>, <c>route</c> or <c>model</c>.
    /// </summary>
    public abstract string Kind { get; }

    /// <summary>Gets the keywords the value may be, for completion in editors; empty when it is not a fixed set.</summary>
    public virtual IReadOnlyList<string> Values => Array.Empty<string>();

    /// <summary>Gets the smallest value accepted, for whole numbers; otherwise <see langword="null"/>.</summary>
    public virtual int? Minimum => null;

    /// <summary>Gets the largest value accepted, for whole numbers; otherwise <see langword="null"/>.</summary>
    public virtual int? Maximum => null;

    /// <summary>Gets a value indicating whether the attribute may be written without a value (<c>&lt;text bold&gt;</c>).</summary>
    public virtual bool AllowsFlag => false;

    /// <summary>Gets a value indicating whether the value may mix literal text and expressions (<c>title="Hi @Model.Name"</c>).</summary>
    public virtual bool AllowsInterpolation => false;

    /// <summary>
    /// Validates a literal value and translates it to C#.
    /// </summary>
    /// <param name="literal">The literal text (entities decoded). <see langword="null"/> for a valueless flag.</param>
    /// <param name="code">The C# expression constructing the value.</param>
    /// <param name="error">Why the value is invalid, when translation fails.</param>
    /// <returns><see langword="true"/> when the literal is valid.</returns>
    public abstract bool TryTranslateLiteral(string? literal, out string code, out string error);

    /// <summary>
    /// Wraps a user expression so it can be assigned to the attribute's property. By default the
    /// expression is assigned as-is and type-checked by the C# compiler.
    /// </summary>
    /// <param name="expressionCode">The C# expression (already wrapped in <c>#line</c> mapping).</param>
    /// <returns>The C# code to assign.</returns>
    public virtual string WrapExpression(string expressionCode) => "(" + expressionCode + ")";
}

/// <summary>Free text, e.g. a box title. Expressions are formatted with <c>MarkupValue.Format</c>.</summary>
internal sealed class StringAttributeType : AttributeType
{
    /// <inheritdoc />
    public override string Description => "text";

    /// <inheritdoc />
    public override string Kind => "text";

    /// <inheritdoc />
    public override bool AllowsInterpolation => true;

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        error = "";
        code = CSharpLiteral.String(literal ?? "");
        return literal is not null;
    }

    /// <inheritdoc />
    public override string WrapExpression(string expressionCode) =>
        "global::ConsoleMVC.Rendering.MarkupValue.Format(" + expressionCode + ")";
}

/// <summary>A whole number within a range, e.g. padding.</summary>
internal sealed class IntegerAttributeType : AttributeType
{
    private readonly int _minimum;
    private readonly int _maximum;

    /// <summary>
    /// Creates an integer attribute type.
    /// </summary>
    /// <param name="minimum">The smallest accepted value.</param>
    /// <param name="maximum">The largest accepted value.</param>
    public IntegerAttributeType(int minimum, int maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    /// <inheritdoc />
    public override string Description => $"a whole number from {_minimum} to {_maximum}";

    /// <inheritdoc />
    public override string Kind => "integer";

    /// <inheritdoc />
    public override int? Minimum => _minimum;

    /// <inheritdoc />
    public override int? Maximum => _maximum;

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        code = "";
        error = "expected " + Description;

        if (!int.TryParse(literal?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < _minimum || value > _maximum)
            return false;

        code = value.ToString(CultureInfo.InvariantCulture);
        return true;
    }
}

/// <summary>
/// A number in the invariant culture, e.g. <c>0</c>, <c>-5</c> or <c>2.5</c>; emitted as a <c>decimal</c> (for
/// <c>&lt;number&gt;</c>) or a <c>double</c> (for <c>&lt;progress&gt;</c>).
/// </summary>
internal sealed class DecimalAttributeType : AttributeType
{
    private readonly string _suffix;

    /// <summary>
    /// Creates a number attribute type.
    /// </summary>
    /// <param name="asDouble"><see langword="true"/> to emit a <c>double</c> literal instead of a <c>decimal</c>.</param>
    public DecimalAttributeType(bool asDouble = false)
    {
        _suffix = asDouble ? "d" : "m";
    }

    /// <inheritdoc />
    public override string Description => "a number such as 0, -5 or 2.5";

    /// <inheritdoc />
    public override string Kind => "number";

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        code = "";
        error = "expected " + Description;

        if (!decimal.TryParse(literal?.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
            return false;

        code = value.ToString(CultureInfo.InvariantCulture) + _suffix;
        return true;
    }
}

/// <summary>
/// The values of a chart: a literal list of numbers separated by commas or spaces (<c>3, 5, 2</c>), or an expression
/// for a sequence of any number type, which is passed through <c>ChartValues.From</c>.
/// </summary>
internal sealed class NumberListAttributeType : AttributeType
{
    /// <inheritdoc />
    public override string Description => "numbers separated by commas or spaces, e.g. '3, 5, 2', or an expression such as @Model.Values";

    /// <inheritdoc />
    public override string Kind => "number-list";

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        code = "";
        error = "expected " + Description;
        var parts = (literal ?? "").Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var numbers = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                    out var value))
                return false;

            numbers.Add(value.ToString("R", CultureInfo.InvariantCulture) + "d");
        }

        code = "new double[] { " + string.Join(", ", numbers) + " }";
        return true;
    }

    /// <inheritdoc />
    public override string WrapExpression(string expressionCode) =>
        "global::ConsoleMVC.Rendering.ChartValues.From(" + expressionCode + ")";
}

/// <summary>
/// A date for <c>&lt;date&gt;</c>: <c>yyyy-MM-dd</c>, <c>today</c>, <c>tomorrow</c>, <c>yesterday</c>, <c>today+N</c>,
/// <c>today-N</c>, <c>+N</c> or <c>-N</c> (up to 100000 days), the forms <c>DateElement.TryParse</c> accepts; checked at
/// build time and emitted as a string, which the element resolves when it is shown.
/// </summary>
internal sealed class DateAttributeType : AttributeType
{
    /// <summary>The most days a relative date may be from today, as in <c>DateElement.TryParse</c>.</summary>
    public const int MaximumDays = 100_000;

    /// <inheritdoc />
    public override string Description => "a date such as 2026-10-27, today, tomorrow, yesterday, today+7 or today-30";

    /// <inheritdoc />
    public override string Kind => "date";

    /// <inheritdoc />
    public override IReadOnlyList<string> Values { get; } = ["today", "tomorrow", "yesterday"];

    /// <inheritdoc />
    public override bool AllowsInterpolation => true;

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        code = "";
        error = "expected " + Description + ", at most " + MaximumDays.ToString(CultureInfo.InvariantCulture) + " days from today";
        var text = (literal ?? "").Trim().ToLowerInvariant();

        // An empty value is no value, as it is for the element's property
        if (text.Length > 0 && !TryParse(text, out _, out _))
            return false;

        code = CSharpLiteral.String(text);
        return true;
    }

    /// <summary>
    /// Reads a literal date exactly as <c>DateElement.TryParse</c> does, without knowing what today is.
    /// </summary>
    /// <param name="text">The literal, e.g. <c>today+7</c> or <c>2026-10-27</c>.</param>
    /// <param name="daysFromToday">The days from today, for a date relative to today; otherwise <see langword="null"/>.</param>
    /// <param name="date">The date, for a fixed date; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the runtime accepts the text as a date.</returns>
    /// <example><code>DateAttributeType.TryParse("today-30", out var days, out _); // days == -30</code></example>
    public static bool TryParse(string? text, out int? daysFromToday, out DateTime? date)
    {
        daysFromToday = null;
        date = null;
        var value = (text ?? "").Trim().ToLowerInvariant();

        switch (value)
        {
            case "today":
                daysFromToday = 0;
                return true;
            case "tomorrow":
                daysFromToday = 1;
                return true;
            case "yesterday":
                daysFromToday = -1;
                return true;
        }

        if (value.StartsWith("today", StringComparison.Ordinal))
            value = value.Substring("today".Length);

        if (value.Length > 1 && value[0] is '+' or '-'
            && int.TryParse(value.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days <= MaximumDays)
        {
            daysFromToday = value[0] == '+' ? days : -days;
            return true;
        }

        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fixedDate))
            return false;

        date = fixedDate;
        return true;
    }

    /// <inheritdoc />
    public override string WrapExpression(string expressionCode) =>
        "global::ConsoleMVC.Rendering.MarkupValue.Format(" + expressionCode + ")";
}

/// <summary><c>true</c>/<c>false</c>, or a valueless flag meaning <c>true</c>.</summary>
internal sealed class BooleanAttributeType : AttributeType
{
    /// <inheritdoc />
    public override string Description => "true or false";

    /// <inheritdoc />
    public override string Kind => "boolean";

    /// <inheritdoc />
    public override IReadOnlyList<string> Values { get; } = ["true", "false"];

    /// <inheritdoc />
    public override bool AllowsFlag => true;

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        error = "expected " + Description;
        var trimmed = literal?.Trim();

        if (trimmed is null || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            code = "true";
            return true;
        }

        code = "false";
        return string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A colour name (<c>cyan</c>, <c>darkgrey</c>), <c>default</c>, or a hex value (<c>#f80</c>, <c>#ff8800</c>).</summary>
internal sealed class ColourAttributeType : AttributeType
{
    /// <summary>
    /// Normalised colour names mapped to <see cref="ConsoleColor"/> member names. Must stay in sync with
    /// <c>ConsoleMVC.Rendering.Colour</c>; the generator tests compile the output to guarantee it.
    /// </summary>
    private static readonly Dictionary<string, string> Names = BuildNames();

    /// <inheritdoc />
    public override string Description => "a colour name such as 'cyan' or 'darkgrey', or a hex value such as '#ff8800'";

    /// <inheritdoc />
    public override string Kind => "colour";

    /// <inheritdoc />
    /// <remarks>The named colours and <c>default</c>; hex values such as <c>#ff8800</c> are accepted too.</remarks>
    public override IReadOnlyList<string> Values { get; } = ["default", .. Names.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        code = "";
        error = "expected " + Description;
        var trimmed = (literal ?? "").Trim();

        if (trimmed.Length == 0 || string.Equals(trimmed, "default", StringComparison.OrdinalIgnoreCase))
        {
            code = "global::ConsoleMVC.Rendering.Colour.Default";
            return true;
        }

        if (trimmed[0] == '#')
            return TryTranslateHex(trimmed.Substring(1), out code);

        var key = trimmed.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
        if (!Names.TryGetValue(key, out var member))
            return false;

        code = "global::ConsoleMVC.Rendering.Colour.FromConsoleColor(global::System.ConsoleColor." + member + ")";
        return true;
    }

    private static bool TryTranslateHex(string digits, out string code)
    {
        code = "";
        if (digits.Length == 3)
            digits = new string(new[] { digits[0], digits[0], digits[1], digits[1], digits[2], digits[2] });

        if (digits.Length != 6
            || !int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            return false;

        code = string.Format(CultureInfo.InvariantCulture,
            "global::ConsoleMVC.Rendering.Colour.FromRgb({0}, {1}, {2})", (rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        return true;
    }

    private static Dictionary<string, string> BuildNames()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in Enum.GetNames(typeof(ConsoleColor)))
        {
            var key = member.ToLowerInvariant();
            names[key] = member;
            if (key.Contains("gray"))
                names[key.Replace("gray", "grey")] = member;
        }

        return names;
    }
}

/// <summary>A width: <c>auto</c>, a column count (<c>40</c>) or a percentage (<c>50%</c>).</summary>
internal sealed class LengthAttributeType : AttributeType
{
    /// <inheritdoc />
    public override string Description => "'auto', a column count such as '40', a percentage such as '50%', or '*' for the remaining width";

    /// <inheritdoc />
    public override string Kind => "length";

    /// <inheritdoc />
    public override IReadOnlyList<string> Values { get; } = ["auto", "*"];

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        code = "";
        error = "expected " + Description;
        var trimmed = (literal ?? "").Trim();

        if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase))
        {
            code = "global::ConsoleMVC.Rendering.Length.Auto";
            return true;
        }

        if (trimmed == "*")
        {
            code = "global::ConsoleMVC.Rendering.Length.Fill";
            return true;
        }

        var isPercent = trimmed.EndsWith("%", StringComparison.Ordinal);
        var number = isPercent ? trimmed.Substring(0, trimmed.Length - 1) : trimmed;

        if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || (isPercent && value > 100))
            return false;

        code = isPercent
            ? "global::ConsoleMVC.Rendering.Length.Percent(" + value.ToString(CultureInfo.InvariantCulture) + ")"
            : "global::ConsoleMVC.Rendering.Length.Fixed(" + value.ToString(CultureInfo.InvariantCulture) + ")";
        return true;
    }
}

/// <summary>One of a fixed set of keywords, each mapped to a C# expression (e.g. <c>double</c> → <c>BorderStyle.Double</c>).</summary>
internal sealed class ChoiceAttributeType : AttributeType
{
    private readonly Dictionary<string, string> _choices;
    private readonly string _displayChoices;

    /// <summary>
    /// Creates a choice attribute type.
    /// </summary>
    /// <param name="choices">Keyword (case-insensitive) to C# expression pairs. Aliases may map to the same expression.</param>
    public ChoiceAttributeType(IEnumerable<KeyValuePair<string, string>> choices)
    {
        _choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in choices)
            _choices[pair.Key] = pair.Value;

        _displayChoices = string.Join(", ", _choices.Keys.Select(k => "'" + k + "'"));
    }

    /// <inheritdoc />
    public override string Description => "one of " + _displayChoices;

    /// <inheritdoc />
    public override string Kind => "choice";

    /// <inheritdoc />
    public override IReadOnlyList<string> Values => _choices.Keys.ToArray();

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        error = "expected " + Description;

        if (_choices.TryGetValue((literal ?? "").Trim(), out var translated))
        {
            code = translated;
            return true;
        }

        code = "";
        return false;
    }
}

/// <summary>
/// A navigation target: <c>Controller/Action</c>, or <c>Action</c> on the current controller. Literals are
/// validated at compile time; expressions are formatted as text.
/// </summary>
internal sealed class RouteAttributeType : AttributeType
{
    /// <inheritdoc />
    public override string Description => "a route such as 'Home/Index', 'Index', 'Admin/Users/Index' or 'Orders/Details?id=42'";

    /// <inheritdoc />
    public override string Kind => "route";

    /// <inheritdoc />
    public override bool AllowsInterpolation => true;

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        code = "";
        error = "expected " + Description;
        var text = (literal ?? "").Trim();
        var query = text.IndexOf('?');
        var parts = (query < 0 ? text : text.Substring(0, query)).Split('/');

        if (parts.Length > 3 || parts.Any(p => !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(p)))
            return false;

        // Route values: key=value pairs separated by '&'; every key must have a name
        if (query >= 0 && text.Substring(query + 1).Split('&').Any(pair => pair.Length > 0 && (pair[0] == '=' || pair.Trim().Length == 0)))
        {
            error = "expected route values such as '?id=42&tab=details'";
            return false;
        }

        code = CSharpLiteral.String(string.Join("/", parts) + (query < 0 ? "" : text.Substring(query)));
        return true;
    }

    /// <inheritdoc />
    public override string WrapExpression(string expressionCode) =>
        "global::ConsoleMVC.Rendering.MarkupValue.Format(" + expressionCode + ")";

    /// <summary>
    /// Wraps an expression that appears after the <c>?</c> of a route, so its value is URL-escaped and cannot add or
    /// split route values (<c>nav="Item?name=@name"</c> with a name containing <c>&amp;</c>).
    /// </summary>
    /// <param name="expressionCode">The C# expression.</param>
    /// <returns>The escaped value.</returns>
    public static string WrapQueryExpression(string expressionCode) =>
        "global::System.Uri.EscapeDataString(global::ConsoleMVC.Rendering.MarkupValue.Format(" + expressionCode + "))";
}

/// <summary>
/// The model passed to a partial view: an expression (<c>model="@Model.User"</c>), type-checked by the compiler against
/// the partial's <c>@model</c>, or literal text, which is passed as a <see cref="string"/>.
/// </summary>
internal sealed class ModelAttributeType : AttributeType
{
    /// <inheritdoc />
    public override string Description => "a model expression";

    /// <inheritdoc />
    public override string Kind => "model";

    /// <inheritdoc />
    public override bool TryTranslateLiteral(string? literal, out string code, out string error)
    {
        error = "expected a value, e.g. model=\"@Model.User\"";
        code = CSharpLiteral.String(literal ?? "");
        return literal is not null;
    }
}
