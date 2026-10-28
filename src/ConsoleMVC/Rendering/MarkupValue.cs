using System.Globalization;
using System.Numerics;

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

    /// <summary>
    /// Converts a number of any type to a <see cref="decimal"/>, for a <c>decimal</c> attribute written as an expression,
    /// such as <c>&lt;number min="@(0.5)"&gt;</c>.
    /// </summary>
    /// <typeparam name="T">The number type: <see cref="int"/>, <see cref="double"/>, <see cref="decimal"/> and so on.</typeparam>
    /// <param name="value">The number.</param>
    /// <returns>The same number as a <see cref="decimal"/>.</returns>
    /// <exception cref="OverflowException">Thrown when the number cannot be a <see cref="decimal"/> (too large, or not a number).</exception>
    /// <example><code>decimal min = MarkupValue.ToDecimal(0.5); // 0.5m</code></example>
    public static decimal ToDecimal<T>(T value) where T : struct, INumberBase<T> => decimal.CreateChecked(value);

    /// <summary>Converts a nullable number of any type to a nullable <see cref="decimal"/>; <see langword="null"/> stays <see langword="null"/>.</summary>
    /// <typeparam name="T">The number type.</typeparam>
    /// <param name="value">The number, or <see langword="null"/>.</param>
    /// <returns>The same number as a <see cref="decimal"/>, or <see langword="null"/>.</returns>
    /// <exception cref="OverflowException">Thrown when the number cannot be a <see cref="decimal"/>.</exception>
    /// <example><code>double? limit = null; decimal? max = MarkupValue.ToDecimal(limit); // null</code></example>
    public static decimal? ToDecimal<T>(T? value) where T : struct, INumberBase<T> =>
        value is { } number ? decimal.CreateChecked(number) : null;

    /// <summary>
    /// Converts a value with its own conversion to <see cref="decimal"/> (a type that is not one of the number types).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <example><code>decimal min = MarkupValue.ToDecimal(price); // price converts itself to decimal</code></example>
    public static decimal ToDecimal(decimal value) => value;

    /// <summary>Converts a value with its own conversion to a nullable <see cref="decimal"/>.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <example><code>decimal? max = MarkupValue.ToDecimal((decimal?)null); // null</code></example>
    public static decimal? ToDecimal(decimal? value) => value;

    /// <summary>
    /// Converts a number of any type to a <see cref="double"/>, for a <c>double</c> attribute written as an expression,
    /// such as <c>&lt;progress value="@Model.Done"&gt;</c> with a <see cref="decimal"/> <c>Done</c>.
    /// </summary>
    /// <typeparam name="T">The number type.</typeparam>
    /// <param name="value">The number.</param>
    /// <returns>The same number as a <see cref="double"/>.</returns>
    /// <example><code>double done = MarkupValue.ToDouble(3.5m); // 3.5</code></example>
    public static double ToDouble<T>(T value) where T : struct, INumberBase<T> => double.CreateChecked(value);

    /// <summary>Converts a nullable number of any type to a nullable <see cref="double"/>; <see langword="null"/> stays <see langword="null"/>.</summary>
    /// <typeparam name="T">The number type.</typeparam>
    /// <param name="value">The number, or <see langword="null"/>.</param>
    /// <returns>The same number as a <see cref="double"/>, or <see langword="null"/>.</returns>
    /// <example><code>decimal? top = 10m; double? max = MarkupValue.ToDouble(top); // 10</code></example>
    public static double? ToDouble<T>(T? value) where T : struct, INumberBase<T> =>
        value is { } number ? double.CreateChecked(number) : null;

    /// <summary>Converts a value with its own conversion to <see cref="double"/> (a type that is not one of the number types).</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <example><code>double value = MarkupValue.ToDouble(reading); // reading converts itself to double</code></example>
    public static double ToDouble(double value) => value;

    /// <summary>Converts a value with its own conversion to a nullable <see cref="double"/>.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns><paramref name="value"/>.</returns>
    /// <example><code>double? max = MarkupValue.ToDouble((double?)null); // null</code></example>
    public static double? ToDouble(double? value) => value;
}
