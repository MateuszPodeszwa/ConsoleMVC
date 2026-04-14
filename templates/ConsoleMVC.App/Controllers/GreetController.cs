using ConsoleMVC.App.Models;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.App.Controllers;

/// <summary>
/// Demonstrates form data posting with complex model binding.
/// The Index action renders a form view that collects user input,
/// and the Result action receives the posted data as a <see cref="GreetFormModel"/>
/// automatically bound by the framework.
/// </summary>
public class GreetController : Controller
{
    /// <summary>
    /// Displays the greeting form where the user enters their name and favourite colour.
    /// </summary>
    public ActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Receives form data posted from the Index view and displays a personalised greeting.
    /// If the name is empty, redirects back to the form.
    /// </summary>
    /// <param name="model">The form data automatically bound from the view's form submission.</param>
    public ActionResult Result(GreetFormModel model)
    {
        // Validate — redirect back to the form if name is missing.
        if (string.IsNullOrWhiteSpace(model.Name))
            return RedirectToAction("Index");

        var greeting = $"Hello, {model.Name}!";

        if (!string.IsNullOrWhiteSpace(model.Color))
            greeting += $" Great choice — {model.Color} is a wonderful colour.";

        return View(new GreetResultModel { Greeting = greeting });
    }
}
