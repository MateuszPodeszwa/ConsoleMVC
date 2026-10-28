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
        Assert.Contains("Index", diagnostic.GetMessage()); // the views that exist
        Assert.Contains("List", diagnostic.GetMessage());
    }

    [Fact]
    public void AMisspeltName_IsSuggested_AndListedFirst()
    {
        var diagnostic = Assert.Single(Analyze("public ActionResult A() => View(\"Lst\");"));

        Assert.Contains("Did you mean 'List'?", diagnostic.GetMessage());
        Assert.Contains("Views of 'Orders': List, ", diagnostic.GetMessage());
    }

    [Fact]
    public void ANameCloseToNothing_HasNoSuggestion()
    {
        var diagnostic = Assert.Single(Analyze("public ActionResult A() => RedirectToAction(\"Zzzzzzzz\");"));

        Assert.DoesNotContain("Did you mean", diagnostic.GetMessage());
    }

    [Fact]
    public void TheMessage_QuotesTheCallAsWritten()
    {
        var diagnostic = Assert.Single(Analyze("public ActionResult A() => RedirectToAction(\"Detials\", \"Orders\");"));

        Assert.StartsWith("RedirectToAction(\"Orders\", \"Detials\")", diagnostic.GetMessage());
        Assert.Contains("Did you mean 'Details'?", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("\"Orders/Lsit\"", "MapKey(\"Orders/Lsit\"): the controller 'Orders' has no action 'Lsit'. Did you mean 'List'?")]
    [InlineData("\"Ordres/List\"", "MapKey(\"Ordres/List\"): there is no controller 'Ordres'. Did you mean 'Orders'?")]
    public void MapKeyTargets_AreChecked(string target, string message)
    {
        var diagnostic = Assert.Single(Analyze($"private void Keys(MvcApplication app) => app.MapKey(System.ConsoleKey.F1, {target}, \"Help\");"));

        Assert.Equal("CVW120", diagnostic.Id);
        Assert.StartsWith(message, diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("public ActionResult Q() => View(\"List\"); public ActionResult Q(int id) => View(\"List\");")]
    [InlineData("public ActionResult Report() => View(\"List\"); public ActionResult report() => View(\"List\");")]
    [InlineData("public ActionResult Index(int page) => View(\"List\");")]   // a second Index, beside the controller's own
    public void TwoActionsWithTheSameName_AreCVW123(string actions)
    {
        var diagnostic = Assert.Single(Analyze(actions), d => d.Id == "CVW123");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("actions named", diagnostic.GetMessage());
    }

    [Fact]
    public void AnActionAndANonActionHelper_WithTheSameName_AreFine()
    {
        Assert.DoesNotContain(Analyze("[NonAction] public ActionResult Report() => View(\"List\"); public ActionResult Report(int id) => View(\"List\");"),
            d => d.Id == "CVW123");
    }

    [Fact]
    public void ACorrectMapKeyTarget_HasNoWarning()
    {
        Assert.Empty(Analyze("private void Keys(MvcApplication app) => app.MapKey(System.ConsoleKey.F1, \"Orders/List?page=2\", \"Help\");"));
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
