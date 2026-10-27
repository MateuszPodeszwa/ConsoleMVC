namespace ConsoleMVC.App.Models;

/// <summary>
/// View model for the home and about pages, carrying display text and menu options.
/// </summary>
public class HomeViewModel
{
    /// <summary>
    /// Gets or sets the page title displayed at the top of the view.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the descriptive message shown to the user.
    /// </summary>
    public string Message { get; set; } = "";
}
