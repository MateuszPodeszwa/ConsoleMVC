using ConsoleMVC.App.Models;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.App.Controllers;

public class HomeController : Controller
{
    public ActionResult Index()
    {
        var model = new HomeViewModel
        {
            Title = "Welcome to ConsoleMVC",
            Message = "A structured MVC framework for console applications.",
            MenuOptions = ["About", "Exit"]
        };

        return View(model);
    }

    public ActionResult About()
    {
        var model = new HomeViewModel
        {
            Title = "About ConsoleMVC",
            Message = "ConsoleMVC brings ASP.NET Core MVC patterns to console applications.\n" +
                      "Each page has its own Controller, View, and ViewModel — just like web MVC.",
            MenuOptions = ["Back to Home", "Exit"]
        };

        return View(model);
    }
}
