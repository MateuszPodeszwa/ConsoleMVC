using System.Collections.Immutable;
using ConsoleMVC.CodeFixes;
using ConsoleMVC.Generators.Analysis;
using ConsoleMVC.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for the code fix that creates a missing view (CVW121).</summary>
public class CreateViewCodeFixTests
{
    private const string Source = """
        using ConsoleMVC.Mvc;

        namespace Shop.Controllers
        {
            public class OrdersController : Controller
            {
                public ActionResult Index() => View("Details", 5);
                public ActionResult List() => View();
            }
        }

        namespace Shop.Views.Orders
        {
            public sealed class ListView : ConsoleView<object>
            {
                public override NavigationResult Render(object Model) => NavigationResult.Quit();
            }
        }
        """;

    private static async Task<(Document Document, Diagnostic Diagnostic)> Setup()
    {
        var workspace = new AdhocWorkspace();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => p.Length > 0).Append(typeof(Controller).Assembly.Location)
            .GroupBy(Path.GetFileName).Select(g => MetadataReference.CreateFromFile(g.First()));

        var project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Shop", "Shop",
                LanguageNames.CSharp, filePath: "/src/Shop/Shop.csproj")
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(references));
        var document = project.AddDocument("Orders.cs", Source, filePath: "/src/Shop/Controllers/Orders.cs");

        var compilation = (await document.Project.GetCompilationAsync())!;
        var diagnostics = await compilation.WithAnalyzers([new NavigationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        return (document, Assert.Single(diagnostics, d => d.Id == "CVW121"));
    }

    [Fact]
    public async Task Fix_AddsTheViewWithTheModelType()
    {
        var (document, diagnostic) = await Setup();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);

        await new CreateViewCodeFix().RegisterCodeFixesAsync(context);

        var action = Assert.Single(actions);
        Assert.Equal("Create view Views/Orders/DetailsView.cvw", action.Title);

        var operation = Assert.Single(await action.GetOperationsAsync(CancellationToken.None)).Should<ApplyChangesOperation>();
        var view = Assert.Single(operation.ChangedSolution.GetProject(document.Project.Id)!.AdditionalDocuments);
        // The project folder as the fix sees it: on Windows "/src/Shop" becomes "\src\Shop"
        var projectFolder = Path.GetDirectoryName("/src/Shop/Shop.csproj")!;
        Assert.Equal(Path.Combine(projectFolder, "Views", "Orders", "DetailsView.cvw"), view.FilePath);
        Assert.Equal(["Views", "Orders"], view.Folders);
        Assert.StartsWith("@model int\n", (await view.GetTextAsync()).ToString());
    }

    [Fact]
    public async Task Fix_IgnoresNamesThatAreNotIdentifiers()
    {
        var (document, diagnostic) = await Setup();
        var unsafeDiagnostic = Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location,
            ImmutableDictionary<string, string?>.Empty.Add("Controller", "../..").Add("View", "x"), "x");
        var actions = new List<CodeAction>();

        await new CreateViewCodeFix().RegisterCodeFixesAsync(
            new CodeFixContext(document, unsafeDiagnostic, (action, _) => actions.Add(action), CancellationToken.None));

        Assert.Empty(actions);
    }

    [Fact]
    public void Content_IsAValidView()
    {
        var result = Infrastructure.ViewHarness.Generate(CreateViewCodeFix.Content("Details", null));

        Assert.Empty(result.Errors);
    }
}

internal static class OperationExtensions
{
    public static T Should<T>(this CodeActionOperation operation) where T : CodeActionOperation =>
        Assert.IsType<T>(operation);
}
