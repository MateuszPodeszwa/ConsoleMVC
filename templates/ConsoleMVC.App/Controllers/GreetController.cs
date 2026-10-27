using ConsoleMVC.App.Models;
using ConsoleMVC.App.Services;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.App.Controllers;

/// <summary>
/// Demonstrates form data posting with complex model binding.
/// The Index action renders a form view that collects user input,
/// and the Result action receives the posted data as a <see cref="GreetFormModel"/>
/// automatically bound by the framework. The <see cref="IGreetingService"/> is injected through the constructor.
/// </summary>
/// <param name="greetings">Builds the greeting text; registered in <c>Program.cs</c>.</param>
public class GreetController(IGreetingService greetings) : Controller
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
        // The posted form was validated against GreetFormModel's attributes before this action ran.
        // Show the form again, with its errors, when anything is wrong.
        if (!ModelState.IsValid)
            return View("Index", null);

        return View(new GreetResultModel { Greeting = greetings.Greet(model.Name, model.Color) });
    }
}
