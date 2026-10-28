using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for CVW120 and CVW121: navigation and views named in C# (docs/design/route-checks.md).</summary>
public class NavigationAnalyzerTests
{
    private const string Models = """
        using ConsoleMVC.Mvc;

        namespace TestApp.Controllers
        {
            public class OrdersController : Controller
            {
                public const string ListAction = "List";

                public ActionResult Index() => View();
                public ActionResult List() => View();
                public ActionResult Details(int id) => View("Details", id);
                public ActionResult Save() => RedirectToAction("Details", new { id = 1 });
                public ActionResult Back() => RedirectToAction(nameof(Index));
                public ActionResult Home() => RedirectToAction("Index", "Home");
                public ActionResult Constant() => RedirectToAction(ListAction);
                public ActionResult Computed(string where) => RedirectToAction(where);
                [NonAction] public ActionResult Helper() => View();
                %ACTIONS%
            }

            public class HomeController : Controller
            {
                public ActionResult Index() => View();
            }
        }

        namespace TestApp.Views.Orders
        {
            public sealed class DetailsView : ConsoleView<int>
            {
                public override NavigationResult Render(int Model) => NavigationResult.ToAction("List");
            }
        }
        """;

    private const string OrdersViews = "return NavigationResult.Quit();";

    private static readonly (string, string)[] Views =
    [
        ("/app/Views/Orders/IndexView.cvw", OrdersViews),
        ("/app/Views/Orders/ListView.cvw", "return NavigationResult.To(\"Home\", \"Index\");"),
        ("/app/Views/Orders/HelperView.cvw", OrdersViews),
        ("/app/Views/Home/IndexView.cvw", OrdersViews)
    ];

    private static IReadOnlyList<Diagnostic> Analyze(string actions = "", params (string, string)[] extraViews) =>
        ViewHarness.Analyze([..Views, ..extraViews], Models.Replace("%ACTIONS%", actions));

    [Fact]
    public void CorrectNavigation_HasNoWarnings()
    {
        Assert.Empty(Analyze());
    }

    [Theory]
    [InlineData("public ActionResult A() => RedirectToAction(\"Detials\");", "'Orders' has no action 'Detials'", "Details")]
    [InlineData("public ActionResult A() => RedirectToAction(\"Index\", \"Hme\");", "no controller 'Hme'", "Home, Orders")]
    [InlineData("public ActionResult A() => RedirectToAction(\"Abuot\", \"Home\");", "'Home' has no action 'Abuot'", "Index")]
    [InlineData("public ActionResult A() => RedirectToAction(\"Helper\");", "no action 'Helper'", "Index")]
    [InlineData("public ActionResult A() => RedirectToAction(\"Detail\", new { id = 2 });", "no action 'Detail'", "Details")]
    public void UnknownRedirects_AreCVW120(string action, string message, string suggestion)
    {
        var diagnostic = Assert.Single(Analyze(action));

        Assert.Equal("CVW120", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains(message, diagnostic.GetMessage());
        Assert.Contains(suggestion, diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("public ActionResult Missing() => View();", "'Orders/Missing'")]
    [InlineData("public ActionResult A() => View(\"Lst\");", "'Orders/Lst'")]
    [InlineData("public ActionResult A() => View(\"Lst\", 5);", "'Orders/Lst'")]
    public void MissingViews_AreCVW121(string action, string message)
    {
        var diagnostic = Assert.Single(Analyze(action));

        Assert.Equal("CVW121", diagnostic.Id);
        Assert.Contains(message, diagnostic.GetMessage());
        Assert.Contains("Index, List", diagnostic.GetMessage()); // the views that exist
    }

    [Fact]
    public void ViewInAHelperMethod_IsNotChecked()
    {
        Assert.Empty(Analyze("private ActionResult Render() => View();"));
    }

    [Fact]
    public void NavigationInCvwViews_IsCheckedAtTheViewsLine()
    {
        var diagnostics = Analyze("", ("/app/Views/Home/AboutView.cvw", "<text>x</text>\nreturn NavigationResult.To(\"Orders\", \"Lst\");"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("CVW120", diagnostic.Id);
        var span = diagnostic.Location.GetMappedLineSpan();
        Assert.EndsWith("AboutView.cvw", span.Path);
        Assert.Equal(1, span.StartLinePosition.Line);
    }

    [Theory]
    [InlineData("return NavigationResult.ToAction(\"Lst\");", "'Orders' has no action 'Lst'")]
    [InlineData("return NavigationResult.To(\"Orders/Lst?x=1\");", "'Orders' has no action 'Lst'")]
    [InlineData("return NavigationResult.To(\"Lst\");", "'Orders' has no action 'Lst'")]
    [InlineData("return NavigationResult.To(\"Ordrs\", \"List\");", "no controller 'Ordrs'")]
    public void NavigationResults_InViews(string code, string message)
    {
        var diagnostic = Assert.Single(Analyze("", ("/app/Views/Orders/OtherView.cvw", code)));

        Assert.Contains(message, diagnostic.GetMessage());
    }

    [Fact]
    public void NoControllers_MeansNoChecks()
    {
        var diagnostics = ViewHarness.Analyze([("/app/Views/Home/IndexView.cvw", "return NavigationResult.To(\"No\", \"Where\");")], "");

        Assert.Empty(diagnostics);
    }
}
