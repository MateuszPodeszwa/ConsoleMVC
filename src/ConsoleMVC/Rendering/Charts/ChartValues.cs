namespace ConsoleMVC.Rendering;

/// <summary>
/// Turns sequences of any number type into the values of a chart, so <c>values="@Model.Visits"</c> works for a
/// <c>List&lt;int&gt;</c> as well as a <c>double[]</c>. Generated code wraps <c>&lt;sparkline values&gt;</c> expressions in
/// <see cref="From(IEnumerable{double}?)"/>.
/// </summary>
/// <example><code>var line = new SparklineElement { Values = ChartValues.From(visitsPerHour) };</code></example>
public static class ChartValues
{
    /// <summary>Copies <paramref name="values"/>; <see langword="null"/> gives no values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The values as <see cref="double"/>.</returns>
    /// <example><code>ChartValues.From(new[] { 1.5, 2.5 });</code></example>
    public static IReadOnlyList<double> From(IEnumerable<double>? values) => values?.ToArray() ?? [];

    /// <summary>Converts <paramref name="values"/>; <see langword="null"/> gives no values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The values as <see cref="double"/>.</returns>
    /// <example><code>ChartValues.From(new List&lt;int&gt; { 3, 5, 2 });</code></example>
    public static IReadOnlyList<double> From(IEnumerable<int>? values) => values?.Select(v => (double)v).ToArray() ?? [];

    /// <summary>Converts <paramref name="values"/>; <see langword="null"/> gives no values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The values as <see cref="double"/>.</returns>
    /// <example><code>ChartValues.From(new long[] { 3, 5 });</code></example>
    public static IReadOnlyList<double> From(IEnumerable<long>? values) => values?.Select(v => (double)v).ToArray() ?? [];

    /// <summary>Converts <paramref name="values"/>; <see langword="null"/> gives no values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The values as <see cref="double"/>.</returns>
    /// <example><code>ChartValues.From(new[] { 0.5f, 1f });</code></example>
    public static IReadOnlyList<double> From(IEnumerable<float>? values) => values?.Select(v => (double)v).ToArray() ?? [];

    /// <summary>Converts <paramref name="values"/>; <see langword="null"/> gives no values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The values as <see cref="double"/>.</returns>
    /// <example><code>ChartValues.From(prices); // decimal prices</code></example>
    public static IReadOnlyList<double> From(IEnumerable<decimal>? values) => values?.Select(v => (double)v).ToArray() ?? [];
}
