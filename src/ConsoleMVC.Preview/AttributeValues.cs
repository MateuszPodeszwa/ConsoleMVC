using System.Globalization;
using System.Reflection;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Preview;

/// <summary>
/// Turns a parsed attribute into the value of the element's property, as the generated code would assign it: a literal
/// is converted to the property's type; an attribute with <c>@expressions</c> becomes placeholder text for a text
/// property (<c>title="Hi @Model.Name"</c> → <c>Hi {Model.Name}</c>), sample numbers for a chart, and is left out
/// otherwise, since the preview runs no C#.
/// </summary>
/// <example>
/// <code>
/// if (AttributeValues.TryConvert(attribute, typeof(BorderStyle), out var value))
///     property.SetValue(element, value); // BorderStyle.Double for border="double"
/// </code>
/// </example>
internal static class AttributeValues
{
    /// <summary>The values a chart shows when its values come from an expression.</summary>
    public static readonly IReadOnlyList<double> SampleSeries = [30, 50, 20, 60, 40, 70, 50, 80];

    /// <summary>Converts <paramref name="attribute"/> to a value of <paramref name="propertyType"/>.</summary>
    /// <param name="attribute">The parsed attribute.</param>
    /// <param name="propertyType">The type of the property it sets.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns><see langword="false"/> when the attribute is left out: its value is an expression the preview cannot show, or invalid.</returns>
    public static bool TryConvert(AttributeNode attribute, Type propertyType, out object? value)
    {
        value = null;
        var target = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (attribute.Parts.Any(p => p.IsExpression))
            return TryPlaceholder(attribute, target, out value);

        // A flag (bold) or a literal, already checked by the parser; check again, as the parser keeps going after errors
        string? literal = attribute.Parts.Count == 0 ? null : string.Concat(attribute.Parts.Select(p => p.Text));
        if (!attribute.Schema.Type.TryTranslateLiteral(literal, out var code, out _))
            return false;

        return TryLiteral(literal, code, attribute.Schema.Type, target, out value);
    }

    /// <summary>The placeholder for an attribute with expressions: text with <c>{expression}</c>, or sample numbers.</summary>
    private static bool TryPlaceholder(AttributeNode attribute, Type target, out object? value)
    {
        value = null;
        if (target == typeof(string))
        {
            value = string.Concat(attribute.Parts.Select(p => p.IsExpression ? Placeholder(p.Text) : p.Text));
            return true;
        }

        if (typeof(IEnumerable<double>).IsAssignableFrom(target) || target.IsAssignableFrom(typeof(double[])))
        {
            value = SampleSeries.ToArray();
            return true;
        }

        return false;
    }

    private static bool TryLiteral(string? literal, string code, AttributeType type, Type target, out object? value)
    {
        value = null;
        var text = (literal ?? "").Trim();
        try
        {
            if (target == typeof(string))
                value = literal ?? "";
            else if (target == typeof(bool))
                value = code == "true";
            else if (target == typeof(int))
                value = int.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            else if (target == typeof(decimal))
                value = decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
            else if (target == typeof(double))
                value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            else if (target == typeof(Colour))
                value = Colour.TryParse(text, out var colour) ? colour : Colour.Default;
            else if (target == typeof(Length))
                value = Length.Parse(text);
            else if (StaticMember(target, code) is { } member)
                value = member;
            else if (type is NumberListAttributeType)
                value = text.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => double.Parse(n, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            else
                return false;
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
        {
            return false;
        }

        return true;
    }

    /// <summary>The placeholder text shown for an expression: <c>{Model.Name}</c>.</summary>
    /// <param name="expression">The expression's code.</param>
    /// <returns>The placeholder.</returns>
    /// <example><code>AttributeValues.Placeholder("Model.Title"); // "{Model.Title}"</code></example>
    public static string Placeholder(string expression) => "{" + expression.Trim() + "}";

    /// <summary>
    /// The value of a generated expression that names a static member of <paramref name="target"/>, such as the enum
    /// member <c>global::ConsoleMVC.Rendering.Alignment.Right</c> or the predefined
    /// <c>global::ConsoleMVC.Rendering.BorderStyle.Double</c>; <see langword="null"/> for any other code.
    /// </summary>
    private static object? StaticMember(Type target, string code)
    {
        const string global = "global::";
        var name = code.StartsWith(global, StringComparison.Ordinal) ? code[global.Length..] : code;
        var dot = name.LastIndexOf('.');
        if (dot < 0 || name[..dot] != target.FullName)
            return null;

        var memberName = name[(dot + 1)..];
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        var value = target.GetField(memberName, flags)?.GetValue(null) ?? target.GetProperty(memberName, flags)?.GetValue(null);
        return target.IsInstanceOfType(value) ? value : null;
    }
}
