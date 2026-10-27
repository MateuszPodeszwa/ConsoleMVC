namespace ConsoleMVC.App.Models;

/// <summary>
/// View model for the status page, which refreshes every second.
/// </summary>
public class StatusViewModel
{
    /// <summary>
    /// Gets or sets when the page was drawn.
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Gets or sets how many of the services are up.
    /// </summary>
    public int Healthy { get; set; }

    /// <summary>
    /// Gets or sets how many services there are.
    /// </summary>
    public int Total { get; set; }
}
