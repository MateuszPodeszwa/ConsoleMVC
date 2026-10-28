using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The <see cref="ILiveDisplay"/> registered in services: a live element started in a remote session is drawn on that
/// session's terminal, and anywhere else on the application's own display (the console).
/// </summary>
/// <param name="local">The display of the local console.</param>
/// <example><code>services.AddSingleton&lt;ILiveDisplay&gt;(new SessionLiveDisplay(new LiveDisplay(renderer)));</code></example>
internal sealed class SessionLiveDisplay(ILiveDisplay local) : ILiveDisplay
{
    /// <inheritdoc />
    public LiveRegion Start(Element element, TimeSpan? interval = null) =>
        (TerminalSession.Current?.LiveDisplay ?? local).Start(element, interval);
}
