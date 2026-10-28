using System.Globalization;
using ConsoleMVC.Mvc;
using Microsoft.Extensions.Hosting;

namespace ConsoleMVC.App.Areas.Admin.Controllers;

/// <summary>
/// A controller in the Admin area: its namespace (<c>*.Areas.Admin.Controllers</c>) puts it there, so its route is
/// <c>Admin/Settings/Index</c> and its views live in <c>Areas/Admin/Views/Settings</c>.
/// </summary>
/// <param name="environment">The environment the application runs in.</param>
public class SettingsController(IHostEnvironment environment) : Controller
{
    /// <summary>
    /// Shows how the application is running: its environment, culture and content folder.
    /// </summary>
    public ActionResult Index()
    {
        ViewData["Environment"] = environment.EnvironmentName;
        ViewData["Culture"] = CultureInfo.CurrentUICulture.Name.Length > 0 ? CultureInfo.CurrentUICulture.Name : "invariant";
        ViewData["Folder"] = environment.ContentRootPath;
        return View();
    }
}
