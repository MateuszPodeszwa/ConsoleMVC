using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace ConsoleMVC.CodeFixes;

/// <summary>
/// Offers "Create view Views/{Controller}/{Name}View.cvw" for CVW121 (<c>View("Name")</c> or <c>View()</c> without a
/// view). The new view declares the model's type when the call passes a model, shows a heading and goes back.
/// </summary>
/// <example>
/// <code>
/// public ActionResult Details(int id) => View(store.Find(id)); // CVW121 → creates Views/Orders/DetailsView.cvw
/// </code>
/// </example>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CreateViewCodeFix)), Shared]
public sealed class CreateViewCodeFix : CodeFixProvider
{
    /// <summary>The diagnostic this fix handles.</summary>
    public const string MissingViewId = "CVW121";

    // Property names set by ConsoleMVC.Generators.Analysis.NavigationAnalyzer
    private const string ControllerProperty = "Controller";
    private const string ViewProperty = "View";
    private const string ModelProperty = "Model";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [MissingViewId];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(ControllerProperty, out var controller) || controller is null
                || !diagnostic.Properties.TryGetValue(ViewProperty, out var view) || view is null
                || !IsName(controller) || !IsName(view))
                continue;

            diagnostic.Properties.TryGetValue(ModelProperty, out var model);
            var project = context.Document.Project;
            var title = $"Create view Views/{controller}/{view}View.cvw";

            context.RegisterCodeFix(
                CodeAction.Create(title, cancellationToken => CreateViewAsync(project, controller, view, model, cancellationToken), title),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds the view to the project as an additional file in <c>Views/{controller}</c>.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="controller">The controller name, which is the view's folder.</param>
    /// <param name="view">The view name, without the <c>View</c> suffix.</param>
    /// <param name="model">The model's type, or <see langword="null"/> for <c>object</c>.</param>
    /// <param name="cancellationToken">Cancels the fix.</param>
    /// <returns>The solution with the new view.</returns>
    public static Task<Solution> CreateViewAsync(Project project, string controller, string view, string? model,
        CancellationToken cancellationToken = default)
    {
        var fileName = view + "View.cvw";
        var folder = Path.GetDirectoryName(project.FilePath ?? "") ?? "";
        var path = Path.Combine(folder, "Views", controller, fileName);

        if (project.AdditionalDocuments.Any(d => string.Equals(d.FilePath, path, System.StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(project.Solution);

        var text = SourceText.From(Content(view, model));
        var document = project.AddAdditionalDocument(fileName, text, ["Views", controller], path);
        return Task.FromResult(document.Project.Solution);
    }

    /// <summary>The new view: its model, a heading and a way back.</summary>
    /// <param name="view">The view name.</param>
    /// <param name="model">The model's type, or <see langword="null"/>.</param>
    /// <returns>The <c>.cvw</c> text.</returns>
    public static string Content(string view, string? model) =>
        $"@model {(string.IsNullOrWhiteSpace(model) ? "object" : model)}\n" +
        "\n" +
        $"<box title=\"{view}\" border=\"rounded\">\n" +
        $"    <text>The {view} page.</text>\n" +
        "</box>\n" +
        "\n" +
        "<menu>\n" +
        "    <option key=\"b\" back>Back</option>\n" +
        "    <option key=\"q\" quit>Exit</option>\n" +
        "</menu>\n";

    /// <summary>Only plain identifiers become folder and file names, so a fix never writes outside <c>Views</c>.</summary>
    private static bool IsName(string text) =>
        text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.All(c => char.IsLetterOrDigit(c) || c == '_');
}
