using Microsoft.Extensions.Options;

namespace ConsoleMVC.App.Services;

/// <summary>
/// The default <see cref="IGreetingService"/>. Registered as a singleton, so <see cref="Count"/> is kept for the
/// lifetime of the application.
/// </summary>
/// <param name="options">The <c>Greeting</c> settings from <c>appsettings.json</c>.</param>
/// <example><code>builder.Services.AddSingleton&lt;IGreetingService, GreetingService&gt;();</code></example>
public sealed class GreetingService(IOptions<GreetingOptions> options) : IGreetingService
{
    /// <inheritdoc />
    public int Count => Volatile.Read(ref _count);

    /// <summary>The greetings so far, counted with <see cref="Interlocked"/>.</summary>
    private int _count;

    /// <inheritdoc />
    public string Greet(string name, string? colour)
    {
        // Thread-safe: with remote sessions, several users greet at once through this one singleton
        Interlocked.Increment(ref _count);

        var greeting = $"{options.Value.Salutation}, {name}!";
        if (!string.IsNullOrWhiteSpace(colour))
            greeting += $" Great choice — {colour} is a wonderful colour.";

        return greeting;
    }
}
