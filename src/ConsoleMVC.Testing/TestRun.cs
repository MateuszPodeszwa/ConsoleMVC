namespace ConsoleMVC.Testing;

/// <summary>
/// What a run of <see cref="MvcTestHost"/> produced: the screens, everything drawn, the routes visited and how much of
/// the script was not read.
/// </summary>
/// <example>
/// <code>
/// var run = host.Run("1", "Ada", "q");
/// Assert.Equal(["Home/Index", "Greet/Index"], run.Routes);
/// Snapshot.Match(run.LastScreen, "greeting");
/// </code>
/// </example>
public sealed class TestRun
{
    /// <summary>
    /// Initialises a run result.
    /// </summary>
    /// <param name="screens">The screens drawn, oldest first.</param>
    /// <param name="routes">The routes of the navigation cycles, as <c>Controller/Action</c>.</param>
    /// <param name="remainingInput">How many script steps were not read.</param>
    /// <example><code>var run = new TestRun(["Hello\n"], ["Home/Index"], 0);</code></example>
    public TestRun(IReadOnlyList<string> screens, IReadOnlyList<string> routes, int remainingInput)
    {
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentNullException.ThrowIfNull(routes);
        Screens = screens;
        Routes = routes;
        RemainingInput = remainingInput;
    }

    /// <summary>Gets the screens drawn, oldest first, as plain text with <c>\n</c> line breaks.</summary>
    public IReadOnlyList<string> Screens { get; }

    /// <summary>Gets the last screen, or an empty string when nothing was drawn.</summary>
    public string LastScreen => Screens.Count > 0 ? Screens[^1] : "";

    /// <summary>Gets everything drawn, all screens one after another.</summary>
    public string Output => string.Concat(Screens);

    /// <summary>
    /// Gets the route of every navigation cycle, as <c>Controller/Action</c>, including redirects.
    /// </summary>
    public IReadOnlyList<string> Routes { get; }

    /// <summary>Gets how many steps of the script were not read; usually 0 for a test that drives the whole run.</summary>
    public int RemainingInput { get; }
}
