namespace ConsoleMVC.App.Models;

/// <summary>
/// Model bound from form data submitted by the Greet/Index view.
/// </summary>
public class GreetFormModel
{
    /// <summary>
    /// Gets or sets the name entered by the user.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the user's favourite colour.
    /// </summary>
    public string Color { get; set; } = "";
}
