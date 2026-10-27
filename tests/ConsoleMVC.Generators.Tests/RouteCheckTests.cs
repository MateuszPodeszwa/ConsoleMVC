using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Tests for compile-time checks of literal <c>nav</c> targets against the application's controllers and actions
/// (docs/design/route-checks.md).
/// </summary>
public class RouteCheckTests
{
    private const string Controllers = """
        using System.Threading.Tasks;
        using ConsoleMVC.Mvc;

        namespace TestApp.Controllers
        {
            public class HomeController : Controller
            {
                public ActionResult Index() => View();
                public ActionResult About() => View();
                public Task<ActionResult> LoadAsync() => Task.FromResult<ActionResult>(View());
                [NonAction] public ActionResult Helper() => View();
                public string NotAnAction() => "";
                public static ActionResult Static() => null!;
                private ActionResult Hidden() => View();
            }

            public abstract class BaseController : Controller
            {
                public ActionResult Shared() => View();
            }

            public partial class OrdersController : BaseController
            {
                public ActionResult Details(int id) => View();
            }

            public partial class OrdersController
            {
                public ViewResult List() => View();
            }
        }
        """;

    private static IEnumerable<Diagnostic> Cvw119(string cvw, string path = "/app/Views/Home/IndexView.cvw",
        string controllers = Controllers) =>
        ViewHarness.Generate(cvw, path, controllers).GeneratorDiagnostics.Where(d => d.Id == "CVW119");

    private static string Menu(string nav) => $"<menu><option key=\"1\" nav=\"{nav}\">Go</option></menu>";

    [Theory]
    [InlineData("Home/Index")]
    [InlineData("home/about")]                 // case-insensitive, like the router
    [InlineData("About")]                      // an action of this view's controller
    [InlineData("Home/LoadAsync")]             // Task<ActionResult>
    [InlineData("Orders/Details?id=42")]       // route values are ignored by the check
    [InlineData("Orders/Shared")]              // inherited from a base controller
    [InlineData("Orders/List")]                // another part of a partial class
    public void KnownTargets_AreFine(string nav)
    {
        Assert.Empty(Cvw119(Menu(nav)));
    }

    [Theory]
    [InlineData("Orders/Detials", "'Orders' has no action 'Detials'", "Details")]
    [InlineData("Ordrs/Details", "no controller 'Ordrs'", "Orders")]
    [InlineData("Abuot", "'Home' has no action 'Abuot'", "About")]
    [InlineData("Home/Helper", "no action 'Helper'", "Index")]          // [NonAction]
    [InlineData("Home/NotAnAction", "no action 'NotAnAction'", "Index")] // not an ActionResult
    [InlineData("Home/Static", "no action 'Static'", "Index")]
    [InlineData("Home/Hidden", "no action 'Hidden'", "Index")]
    [InlineData("Base/Shared", "no controller 'Base'", "Home")]         // abstract
    public void UnknownTargets_ReportCVW119_WithTheChoices(string nav, string message, string suggestion)
    {
        var diagnostic = Assert.Single(Cvw119(Menu(nav)));

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains(message, diagnostic.GetMessage());
        Assert.Contains(suggestion, diagnostic.GetMessage());
        Assert.Equal(0, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Equal(Menu(nav).IndexOf(nav, StringComparison.Ordinal), diagnostic.Location.GetLineSpan().StartLinePosition.Character);
    }

    [Fact]
    public void ExpressionTargets_AreNotChecked()
    {
        Assert.Empty(Cvw119("var target = \"Nowhere\";\n" + Menu("Home/@target")));
    }

    [Fact]
    public void NoControllersInTheCompilation_MeansNoChecks()
    {
        // e.g. views compiled in a library whose controllers live elsewhere
        Assert.Empty(Cvw119(Menu("Anything/AtAll"), controllers: ""));
    }

    [Fact]
    public void ActionOnlyTargets_InAFolderWithoutAController_AreNotChecked()
    {
        Assert.Empty(Cvw119(Menu("Anything"), "/app/Views/Reports/IndexView.cvw"));
    }

    [Fact]
    public void TheViewStillCompiles()
    {
        var result = ViewHarness.Generate(Menu("Orders/Detials"), extraSource: Controllers);

        Assert.NotNull(result.Assembly);
        Assert.Empty(result.Errors);
    }
}
