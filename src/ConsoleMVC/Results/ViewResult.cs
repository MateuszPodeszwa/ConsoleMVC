namespace ConsoleMVC.Mvc;

/// <summary>
/// An <see cref="ActionResult"/> that instructs the framework to render the console view
/// matching the current controller and action, passing the specified model and view data.
/// </summary>
/// <remarks>
/// This is analogous to <c>ViewResult</c> in ASP.NET Core MVC.
/// Instances are created by calling <see cref="Controller.View(object?)"/> from within
/// a controller action method.
/// </remarks>
public class ViewResult : ActionResult
{
    /// <summary>
    /// Gets the name of the controller whose view should be rendered
    /// (without the <c>Controller</c> suffix).
    /// </summary>
    public string ControllerName { get; init; } = "";

    /// <summary>
    /// Gets the name of the view to render, which corresponds to the action method name.
    /// The framework resolves this to a class named <c>{ViewName}View</c> in the
    /// <c>*.Views.{ControllerName}</c> namespace.
    /// </summary>
    public string ViewName { get; init; } = "";

    /// <summary>
    /// Gets the view model to pass to the view, or <see langword="null"/> if no model is provided.
    /// </summary>
    public object? Model { get; init; }

    /// <summary>
    /// Gets the <see cref="ViewDataDictionary"/> containing supplementary data
    /// to make available to the view.
    /// </summary>
    public ViewDataDictionary ViewData { get; init; } = new();

    /// <summary>
    /// Gets the validation errors of the request that produced this result, passed on to the view.
    /// </summary>
    public ModelStateDictionary ModelState { get; init; } = new();

    /// <summary>The shortest refresh interval accepted by <see cref="RefreshEvery"/>: 100 ms.</summary>
    public static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets how often the screen is drawn again while the user has not started answering, as chosen by
    /// <see cref="RefreshEvery"/>; <see cref="Timeout.InfiniteTimeSpan"/> after <see cref="NoRefresh"/>; or
    /// <see langword="null"/>, which leaves the choice to the view's <c>@refresh</c> directive.
    /// </summary>
    /// <example><code>var interval = result.RefreshInterval;</code></example>
    public TimeSpan? RefreshInterval { get; private set; }

    /// <summary>
    /// Draws the screen again every <paramref name="interval"/> until the user starts answering: the action runs again
    /// for fresh data. This wins over the view's <c>@refresh</c> directive.
    /// </summary>
    /// <param name="interval">How often, at least <see cref="MinimumRefreshInterval"/>.</param>
    /// <returns>This result, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is shorter than <see cref="MinimumRefreshInterval"/>.</exception>
    /// <example><code>return View(status).RefreshEvery(TimeSpan.FromSeconds(5));</code></example>
    public ViewResult RefreshEvery(TimeSpan interval)
    {
        if (interval < MinimumRefreshInterval)
            throw new ArgumentOutOfRangeException(nameof(interval), interval,
                $"A screen refreshes at most every {MinimumRefreshInterval.TotalMilliseconds} ms.");

        RefreshInterval = interval;
        return this;
    }

    /// <summary>Turns off the view's <c>@refresh</c> directive for this result: the screen waits for the user as usual.</summary>
    /// <returns>This result, for chaining.</returns>
    /// <example><code>return paused ? View(status).NoRefresh() : View(status);</code></example>
    public ViewResult NoRefresh()
    {
        RefreshInterval = Timeout.InfiniteTimeSpan;
        return this;
    }

    /// <summary>Returns the interval to refresh at, given the view's own (<c>@refresh</c>); <see langword="null"/> for none.</summary>
    internal TimeSpan? ResolveRefresh(TimeSpan? viewInterval) =>
        RefreshInterval == Timeout.InfiniteTimeSpan ? null : RefreshInterval ?? viewInterval;
}
