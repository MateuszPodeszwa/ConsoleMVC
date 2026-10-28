using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for views and navigation checks in areas (docs/design/areas-and-modules.md).</summary>
public class AreaMarkupTests
{
    private const string Controllers = """
        using ConsoleMVC.Mvc;

        namespace TestApp.Controllers
        {
            public class PortalController : Controller
            {
                public ActionResult Index() => View();
            }

            [Area("Reports")]
            public class SalesController : Controller
            {
                public ActionResult Index() => View();
            }
        }

        namespace TestApp.Areas.Admin.Controllers
        {
            public class UsersController : Controller
            {
                public ActionResult Index() => View();
                public ActionResult Leave() => RedirectToAction("Index", "Portal");
                %ACTIONS%
            }

            public class PortalController : Controller
            {
                public ActionResult Index() => View();
            }
        }
        """;

    private const string Quit = "return NavigationResult.Quit();";

    private static readonly (string, string)[] Views =
    [
        ("/app/Views/Portal/IndexView.cvw", Quit),
        ("/app/Areas/Admin/Views/Users/IndexView.cvw", Quit),
        ("/app/Areas/Admin/Views/Users/LeaveView.cvw", Quit),
        ("/app/Areas/Admin/Views/Portal/IndexView.cvw", Quit),
        ("/app/Areas/Reports/Views/Sales/IndexView.cvw", Quit)
    ];

    private static string Menu(string target) => $"<menu><option key=\"1\" nav=\"{target}\">Go</option></menu>";

    [Fact]
    public void AreaViews_GetTheAreasNamespace_AndDoNotClashWithRootViews()
    {
        var result = ViewHarness.GenerateFiles(Views, Controllers.Replace("%ACTIONS%", ""));

        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CVW004");
        Assert.NotNull(result.Assembly!.GetType("TestApp.Areas.Admin.Views.Portal.IndexView"));
        Assert.NotNull(result.Assembly.GetType("TestApp.Views.Portal.IndexView"));
    }

    [Theory]
    [InlineData("Index")]                    // the view's own controller, Admin/Users
    [InlineData("Portal/Index")]             // the Admin area's Portal
    [InlineData("Admin/Portal/Index")]
    [InlineData("Reports/Sales/Index")]      // an [Area] controller
    public void Nav_KnowsAreas(string target)
    {
        var result = ViewHarness.GenerateFiles([..Views, ("/app/Areas/Admin/Views/Users/MenuView.cvw", Menu(target))],
            Controllers.Replace("%ACTIONS%", "public ActionResult Menu() => View();"));

        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CVW119");
    }

    [Theory]
    [InlineData("Admin/Users/Nope", "the controller 'Admin/Users' has no action 'Nope'")]
    [InlineData("Sales/Index", "there is no controller 'Sales'")]
    [InlineData("Nope", "the controller 'Admin/Users' has no action 'Nope'")]
    public void Nav_ReportsWrongAreaRoutes(string target, string message)
    {
        var result = ViewHarness.GenerateFiles([..Views, ("/app/Areas/Admin/Views/Users/MenuView.cvw", Menu(target))],
            Controllers.Replace("%ACTIONS%", "public ActionResult Menu() => View();"));

        var warning = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW119");
        Assert.Contains(message, warning.GetMessage());
    }

    [Fact]
    public void AnInvalidAreaFolder_IsReported()
    {
        var result = ViewHarness.Generate(Quit, "/app/Areas/My-Area/Views/Users/IndexView.cvw");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW002");
    }

    // ------------------------------------------------------------------ C# navigation

    private static IReadOnlyList<Diagnostic> Analyze(string actions) =>
        ViewHarness.Analyze(Views, Controllers.Replace("%ACTIONS%", actions));

    [Fact]
    public void Redirects_InAnArea_PreferTheArea_AndFindAreaViews()
    {
        Assert.Empty(Analyze("public ActionResult Other() => RedirectToAction(\"Index\", \"Admin/Portal\");"));
    }

    [Fact]
    public void WrongRedirects_AndMissingViews_InAnArea_AreReported()
    {
        var diagnostics = Analyze("public ActionResult Bad() => RedirectToAction(\"Nope\"); public ActionResult Missing() => View();");

        Assert.Contains(diagnostics, d => d.Id == "CVW120" && d.GetMessage().Contains("'Admin/Users' has no action 'Nope'"));
        var missing = Assert.Single(diagnostics, d => d.Id == "CVW121");
        Assert.Contains("Areas/Admin/Views/Users/MissingView.cvw", missing.GetMessage());
        Assert.Equal("Admin/Users", missing.Properties["Controller"]);
    }
}
