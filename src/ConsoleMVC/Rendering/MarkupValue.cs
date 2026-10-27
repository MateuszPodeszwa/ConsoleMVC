using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Converts values of markup expressions (<c>@Model.Count</c>, <c>title=@Model.Name</c>) to text.
/// </summary>
/// <example>
/// <code>
/// MarkupValue.Format(42);            // "42"
/// MarkupValue.Format(null);          // ""
/// MarkupValue.Format(DateTime.Now);  // formatted with the current culture
/// </code>
/// </example>
public static class MarkupValue
{
    /// <summary>
    /// Formats <paramref name="value"/> using the current culture. <see langword="null"/> becomes an empty string.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The text representation (not yet sanitised).</returns>
    public static string Format(object? value) => value switch
    {
        null => "",
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? ""
    };
}
