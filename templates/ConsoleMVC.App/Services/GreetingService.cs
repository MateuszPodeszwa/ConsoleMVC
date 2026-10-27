namespace ConsoleMVC.App.Services;

/// <summary>
/// The default <see cref="IGreetingService"/>. Registered as a singleton, so <see cref="Count"/> is kept for the
/// lifetime of the application.
/// </summary>
/// <example><code>builder.Services.AddSingleton&lt;IGreetingService, GreetingService&gt;();</code></example>
public sealed class GreetingService : IGreetingService
{
    /// <inheritdoc />
    public int Count { get; private set; }

    /// <inheritdoc />
    public string Greet(string name, string? colour)
    {
        Count++;

        var greeting = $"Hello, {name}!";
        if (!string.IsNullOrWhiteSpace(colour))
            greeting += $" Great choice — {colour} is a wonderful colour.";

        return greeting;
    }
}
