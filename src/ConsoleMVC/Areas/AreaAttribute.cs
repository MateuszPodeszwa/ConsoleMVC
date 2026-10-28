namespace ConsoleMVC.Mvc;

/// <summary>
/// Puts a controller in an area, a group of related screens reached as <c>{Area}/{Controller}/{Action}</c>, whose views
/// live in <c>Areas/{Area}/Views/{Controller}</c>. A controller in a namespace containing <c>.Areas.{Area}.</c> is in that
/// area without the attribute.
/// </summary>
/// <param name="name">The area's name, a valid C# identifier such as <c>Admin</c>.</param>
/// <example>
/// <code>
/// [Area("Admin")]
/// public class UsersController : Controller { public ActionResult Index() => View(); }
/// // nav="Admin/Users/Index", view: Areas/Admin/Views/Users/IndexView.cvw
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AreaAttribute(string name) : Attribute
{
    /// <summary>Gets the area's name.</summary>
    public string Name { get; } = name;
}
