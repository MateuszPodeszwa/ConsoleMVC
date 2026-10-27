#nullable enable

using System.Collections.Generic;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Parsing;
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
        CheckLayout(document, info, partials, diagnostics);

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

        if (partial.IsLayout)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.UnknownPartial, nameAttribute.Span,
                $"'{partial.Name}' is a layout, not a partial; a view uses it with '@layout {partial.Name}'"));
            return;
        }

        if (partial.HasErrors)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.UnknownPartial, nameAttribute.Span,
                $"Partial view '{partial.Name}' has errors; fix them first"));
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

    /// <summary>The name a view's <c>@layout</c> directive uses for "no layout".</summary>
    public const string NoLayout = "none";

    /// <summary>The layout used by views without an <c>@layout</c> directive, when it exists.</summary>
    public const string DefaultLayout = "_Layout";

    /// <summary>
    /// Returns the layout a full view is drawn in: the one named by <c>@layout</c>, else <c>_Layout</c> when it exists;
    /// <see langword="null"/> for <c>@layout none</c>, partials, layouts, and unknown names (reported by <see cref="Check"/>).
    /// </summary>
    /// <param name="document">The parsed view.</param>
    /// <param name="info">Where the view lives.</param>
    /// <param name="partials">The project's partial views and layouts.</param>
    public static PartialEntry? ResolveLayout(CvwDocument document, ViewInfo info, PartialCatalogue partials)
    {
        if (info.IsPartial)
            return null;

        var name = document.Layout?.Value ?? DefaultLayout;
        if (name == NoLayout)
            return null;

        return partials.TryGet(name, out var layout) && layout.IsLayout && !layout.HasErrors ? layout : null;
    }

    /// <summary>
    /// Checks <c>&lt;body /&gt;</c> placement (only in <c>Views/Shared</c>, once, outside any C# block), that layouts have
    /// no <c>@model</c> or <c>@layout</c>, and that a view's <c>@layout</c> names an existing layout.
    /// </summary>
    private static void CheckLayout(CvwDocument document, ViewInfo info, PartialCatalogue partials, List<ParseDiagnostic> diagnostics)
    {
        var bodies = Elements(document.Body).Where(e => e.Schema.Tag == MarkupSchema.BodyTag).ToList();

        if (bodies.Count > 0 && !info.IsPartial)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidLayout, bodies[0].Span,
                "<body /> belongs in a layout: put this file in Views/Shared and use it with @layout"));
            return;
        }

        if (bodies.Count > 1)
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidLayout, bodies[1].Span, "A layout has exactly one <body />"));

        if (bodies.Count == 1)
        {
            var index = IndexOf(document.Body, bodies[0]);
            if (index >= 0 && BraceDepth(document.Body.Take(index)) != 0)
            {
                diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidLayout, bodies[0].Span,
                    "<body /> cannot be inside a C# block (if, for, { }), because the view is drawn between the parts before and after it"));
            }

            if (document.Model is not null)
                diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidLayout, document.Model.Span,
                    "A layout has no model; read shared values from ViewData instead"));

            if (document.Layout is not null)
                diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidLayout, document.Layout.Span, "A layout cannot use another layout"));

            return;
        }

        if (document.Layout is null || document.Layout.Value == NoLayout)
            return;

        if (info.IsPartial)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.UnknownLayout, document.Layout.Span,
                "Partial views are drawn inside a view and its layout, so they cannot have a layout"));
            return;
        }

        if (!partials.TryGet(document.Layout.Value, out var layout) || !layout.IsLayout)
        {
            var available = partials.LayoutNames.ToList();
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.UnknownLayout, document.Layout.Span,
                $"No layout named '{document.Layout.Value}': a layout is a .cvw file in Views/Shared with a <body /> (available: " +
                (available.Count == 0 ? "none" : string.Join(", ", available)) + "), or use '@layout none'"));
        }
    }

    private static int IndexOf(IReadOnlyList<Node> nodes, Node node)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], node))
                return i;
        }

        return -1;
    }

    /// <summary>The number of unclosed <c>{</c> in the C# code of <paramref name="nodes"/>, ignoring strings and comments.</summary>
    private static int BraceDepth(IEnumerable<Node> nodes)
    {
        var depth = 0;
        foreach (var code in nodes.OfType<CodeNode>().Select(c => c.Code))
        {
            var position = 0;
            while (position < code.Length)
            {
                var skip = CSharpLexer.Skip(code, position);
                if (skip.Kind != TriviaKind.None)
                {
                    position = skip.End;
                    continue;
                }

                if (code[position] == '{') depth++;
                else if (code[position] == '}') depth--;
                position++;
            }
        }

        return depth;
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
