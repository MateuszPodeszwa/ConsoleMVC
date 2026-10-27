namespace ConsoleMVC.Mvc;

/// <summary>
/// Marks a public controller method that returns an <see cref="ActionResult"/> as a helper that
/// must not be reachable through navigation, mirroring ASP.NET Core's <c>[NonAction]</c>.
/// </summary>
/// <example>
/// <code>
/// public class AccountController : Controller
/// {
///     public ActionResult Index() => Dashboard();
///
///     [NonAction]
///     public ActionResult Dashboard() => View(new DashboardViewModel());
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class NonActionAttribute : Attribute
{
}
