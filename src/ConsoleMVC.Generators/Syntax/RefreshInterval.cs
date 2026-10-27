#nullable enable

using System;
using System.Globalization;

namespace ConsoleMVC.Generators.Syntax;

/// <summary>
/// Reads the interval of an <c>@refresh</c> directive: a number with <c>ms</c>, <c>s</c> or <c>m</c>, from 100 ms to 24 hours.
/// </summary>
/// <example><code>RefreshInterval.TryParse("1.5s", out var ms, out var error); // ms == 1500</code></example>
internal static class RefreshInterval
{
    /// <summary>The shortest interval accepted, matching <c>ViewResult.MinimumRefreshInterval</c>.</summary>
    public const double MinimumMilliseconds = 100;

    /// <summary>The longest interval accepted: 24 hours.</summary>
    public const double MaximumMilliseconds = 24 * 60 * 60 * 1000;

    /// <summary>
    /// Parses <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The directive's argument, e.g. <c>5s</c>.</param>
    /// <param name="milliseconds">The interval, when valid.</param>
    /// <param name="error">Why it is invalid, otherwise empty.</param>
    /// <returns><see langword="true"/> when the interval can be used.</returns>
    public static bool TryParse(string text, out double milliseconds, out string error)
    {
        milliseconds = 0;
        error = "";
        var value = text.Trim();

        var (unit, scale) = value.EndsWith("ms", StringComparison.Ordinal) ? ("ms", 1d)
            : value.EndsWith("s", StringComparison.Ordinal) ? ("s", 1000d)
            : value.EndsWith("m", StringComparison.Ordinal) ? ("m", 60_000d)
            : ("", 0d);

        if (unit.Length == 0
            || !double.TryParse(value.Substring(0, value.Length - unit.Length), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount))
        {
            error = $"'{value}' is not an interval: write a number with ms, s or m, e.g. '@refresh 5s' or '@refresh 500ms'";
            return false;
        }

        milliseconds = Math.Round(amount * scale);
        if (milliseconds < MinimumMilliseconds || milliseconds > MaximumMilliseconds)
        {
            error = $"'{value}' is outside the intervals a screen can refresh at: from 100ms to 24 hours (1440m)";
            return false;
        }

        return true;
    }
}
