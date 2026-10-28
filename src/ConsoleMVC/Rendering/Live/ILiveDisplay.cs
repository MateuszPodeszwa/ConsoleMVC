namespace ConsoleMVC.Rendering;

/// <summary>
/// Draws elements that change while an action works, such as a <see cref="ProgressElement"/> or a
/// <see cref="SpinnerElement"/>, in place on the screen. Registered by the framework as a singleton (<see cref="LiveDisplay"/>),
/// so controllers and services take it in their constructor. Replace the registration to draw live elements elsewhere.
/// </summary>
/// <example>
/// <code>
/// public sealed class ImportController(ILiveDisplay live, IImporter importer) : Controller
/// {
///     public async Task&lt;ActionResult&gt; Run()
///     {
///         var bar = new ProgressElement { Label = "Import ", Max = importer.Count };
///         await using (var region = live.Start(bar))
///         {
///             await foreach (var _ in importer.ImportAsync())
///                 region.Update(() => bar.Value++);
///         }
///         return View();
///     }
/// }
/// </code>
/// </example>
public interface ILiveDisplay
{
    /// <summary>
    /// Draws <paramref name="element"/> and keeps it up to date until the returned region is disposed.
    /// </summary>
    /// <param name="element">The element; it should be the last thing drawn while the region is live.</param>
    /// <param name="interval">How often spinners turn and the element is redrawn; <see langword="null"/> for <see cref="LiveDisplay.DefaultInterval"/>.</param>
    /// <returns>The region: change the element through <see cref="LiveRegion.Update"/>, and dispose it when the work is done.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is outside <see cref="LiveDisplay.MinimumInterval"/> to <see cref="LiveDisplay.MaximumInterval"/>.</exception>
    /// <example><code>using var region = live.Start(new SpinnerElement { Label = "Connecting" });</code></example>
    LiveRegion Start(Element element, TimeSpan? interval = null);
}
