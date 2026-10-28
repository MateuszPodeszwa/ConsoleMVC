namespace ConsoleMVC.Rendering;

/// <summary>
/// Lets the field being answered claim the keys it has a use for at that moment, so an application shortcut on the
/// same key does not take them: Esc restores a changed <c>&lt;number&gt;</c> to its default and clears a
/// <c>&lt;select filter&gt;</c>'s filter, and is the application's Back shortcut (if mapped) otherwise.
/// </summary>
/// <example>
/// <code>
/// using (FieldKeyClaim.Claim(key => key.Key == ConsoleKey.Escape &amp;&amp; _buffer != _default))
///     key = input.ReadKey();
/// </code>
/// </example>
internal static class FieldKeyClaim
{
    // Per asynchronous flow, so each remote session has its own field
    private static readonly AsyncLocal<Func<ConsoleKeyInfo, bool>?> Current = new();

    /// <summary>
    /// Claims the keys <paramref name="claims"/> accepts until the result is disposed. The test runs for every key, so
    /// it can depend on the field's state.
    /// </summary>
    /// <param name="claims">Whether the field uses a key now.</param>
    /// <returns>Disposing it gives up the claim, putting back any claim made before.</returns>
    /// <example><code>using var claim = FieldKeyClaim.Claim(key => key.Key == ConsoleKey.Escape);</code></example>
    public static IDisposable Claim(Func<ConsoleKeyInfo, bool> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var previous = Current.Value;
        Current.Value = claims;
        return new Release(previous);
    }

    /// <summary>
    /// Whether the field being answered uses <paramref name="key"/> now.
    /// </summary>
    /// <param name="key">The key pressed.</param>
    /// <returns><see langword="true"/> when the field claims it, so a shortcut must not take it.</returns>
    /// <example><code>if (FieldKeyClaim.IsClaimed(key)) return; // the field handles it</code></example>
    public static bool IsClaimed(ConsoleKeyInfo key) => Current.Value?.Invoke(key) == true;

    private sealed class Release(Func<ConsoleKeyInfo, bool>? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
