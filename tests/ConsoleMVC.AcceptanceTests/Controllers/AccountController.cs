using Acceptance.Models;
using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>The sign-up form of the validation guide.</summary>
public class AccountController : Controller
{
    /// <summary>Shows the empty form. Example: <c>nav="Account/Signup"</c>.</summary>
    public ActionResult Signup() => View();

    /// <summary>Validates the posted form; shows it again with its errors, or the welcome page.</summary>
    public ActionResult Save(SignupModel model) =>
        ModelState.IsValid ? View("Welcome", model) : View("Signup", model);
}
