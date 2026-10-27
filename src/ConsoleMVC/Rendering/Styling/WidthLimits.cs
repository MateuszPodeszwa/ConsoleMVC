namespace ConsoleMVC.Rendering;

/// <summary>
/// Applies <c>min-width</c> and <c>max-width</c> to a resolved width.
/// </summary>
/// <example><code>var width = WidthLimits.Apply(resolved, MinWidth, MaxWidth, available);</code></example>
internal static class WidthLimits
{
    /// <summary>
    /// Clamps <paramref name="width"/> to the limits, then to the available width (which always wins, so nothing draws
    /// past its container).
    /// </summary>
    /// <param name="width">The width from the element's own sizing.</param>
    /// <param name="min">The minimum width, or <see langword="null"/>.</param>
    /// <param name="max">The maximum width, or <see langword="null"/>.</param>
    /// <param name="available">The width available.</param>
    /// <returns>The final width.</returns>
    public static int Apply(int width, int? min, int? max, int available)
    {
        if (max is { } upper)
            width = Math.Min(width, Math.Max(upper, 0));

        if (min is { } lower)
            width = Math.Max(width, lower);

        return Math.Clamp(width, 0, Math.Max(available, 0));
    }
}
