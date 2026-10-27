#nullable enable

using System.Collections.Generic;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// Checks the rules that need the whole project: every <c>&lt;partial&gt;</c> names an existing partial view (written
/// literally) and passes a model when the partial declares one, a partial does not render itself, and partial views
/// contain no <c>&lt;menu&gt;</c> (a menu ends a view, which a partial cannot do).
/// </summary>
/// <example><code>var errors = PartialChecker.Check(document, info, catalogue);</code></example>
internal static class PartialChecker
{
    /// <summary>
    /// Returns the problems found in <paramref name="document"/>.
    /// </summary>
    /// <param name="document">The parsed view, free of parse errors.</param>
    /// <param name="info">Where the view lives (whether it is a partial).</param>
    /// <param name="partials">The project's partial views.</param>
    /// <returns>The diagnostics, empty when everything is valid.</returns>
    public static IReadOnlyList<ParseDiagnostic> Check(CvwDocument document, ViewInfo info, PartialCatalogue partials)
    {
        var diagnostics = new List<ParseDiagnostic>();
        foreach (var element in Elements(document.Body))
        {
            if (element.Schema.Tag == MarkupSchema.MenuTag && info.IsPartial)
            {
                diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.ElementNotAllowed, element.Span,
                    "<menu> is not allowed in a partial view: a menu ends the view, so put it in the view that renders the partial"));
            }

            if (element.Schema.Tag == MarkupSchema.PartialTag)
                CheckPartial(element, info, partials, diagnostics);
        }

        return diagnostics;
    }

    /// <summary>
    /// Returns the literal name of a <c>&lt;partial&gt;</c> element, or <see langword="null"/> when it contains an expression.
    /// </summary>
    /// <param name="element">The partial element.</param>
    public static string? LiteralName(ElementNode element)
    {
        var name = element.Attributes.First(a => a.Schema.Property == "Name");
        return name.Parts.Any(p => p.IsExpression) ? null : string.Concat(name.Parts.Select(p => p.Text));
    }

    private static void CheckPartial(ElementNode element, ViewInfo info, PartialCatalogue partials, List<ParseDiagnostic> diagnostics)
    {
        var nameAttribute = element.Attributes.First(a => a.Schema.Property == "Name");
        var name = LiteralName(element);

        if (name is null)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.UnknownPartial, nameAttribute.Span,
                "The partial name must be written literally, e.g. <partial name=\"Header\" />, so it can be checked at build time"));
            return;
        }

        if (!partials.TryGet(name, out var partial))
        {
            var available = partials.Names.ToList();
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.UnknownPartial, nameAttribute.Span,
                $"No partial view named '{name}': partials are .cvw files in Views/Shared (available: " +
                (available.Count == 0 ? "none" : string.Join(", ", available)) + ")"));
            return;
        }

        if (info.IsPartial && partial.FullClassName == info.FullClassName)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.RecursivePartial, element.Span, partial.Name));
            return;
        }

        if (partial.ModelType is not null && element.Attributes.All(a => a.Schema.Property != "Model"))
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.PartialModelMissing, element.Span, partial.Name, partial.ModelType));
    }

    /// <summary>Returns every element in <paramref name="nodes"/>, including nested ones.</summary>
    private static IEnumerable<ElementNode> Elements(IEnumerable<Node> nodes)
    {
        foreach (var element in nodes.OfType<ElementNode>())
        {
            yield return element;
            foreach (var child in Elements(element.Children))
                yield return child;
        }
    }
}
