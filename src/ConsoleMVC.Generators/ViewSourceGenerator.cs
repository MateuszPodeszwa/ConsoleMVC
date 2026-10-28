using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConsoleMVC.Generators.Analysis;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Emit;
using ConsoleMVC.Generators.Parsing;
using ConsoleMVC.Generators.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace ConsoleMVC.Generators;

/// <summary>
/// An incremental source generator that transforms <c>.cvw</c> (Console View) files into
/// strongly-typed <c>ConsoleView&lt;TModel&gt;</c> subclasses at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Each <c>.cvw</c> file must reside at <c>Views/{Controller}/{Action}View.cvw</c>, or at
/// <c>Views/Shared/{Name}.cvw</c> for a partial view (a <c>ConsolePartialView&lt;TModel&gt;</c>). It may begin with an
/// <c>@model</c> directive (defaulting to <c>object</c>) and <c>@using</c> directives; the rest of the file
/// is the body of the generated <c>Render</c> method — C# code that may embed C-View Markup elements.
/// </para>
/// <para>
/// The pipeline is: <see cref="CvwDocumentParser"/> (directives and body) → diagnostics →
/// <see cref="ViewEmitter"/> (C# with <c>#line</c> mapping). See <c>docs/design/c-view-markup.md</c>.
/// </para>
/// <para>
/// The generator reads the <c>RootNamespace</c> build property from the consuming project
/// so that generated classes are placed under <c>{RootNamespace}.Views.{Controller}</c>.
/// </para>
/// </remarks>
[Generator]
public class ViewSourceGenerator : IIncrementalGenerator
{
    /// <summary>The namespace used when the consuming project does not define <c>RootNamespace</c>.</summary>
    private const string FallbackRootNamespace = "ConsoleMVC";

    /// <summary>A partial's class cannot be named after its own <c>Render</c> method (CVW009).</summary>
    private const string ReservedPartialName = "Render";

    /// <summary>
    /// Registers the source generation pipeline. Collects <c>.cvw</c> additional files
    /// and the consuming project's <c>RootNamespace</c>, then generates a
    /// <c>ConsoleView&lt;TModel&gt;</c> subclass for each view file.
    /// </summary>
    /// <param name="context">The initialisation context provided by the Roslyn compiler.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var viewFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".cvw", StringComparison.OrdinalIgnoreCase));

        var rootNamespace = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
                return string.IsNullOrWhiteSpace(ns) ? FallbackRootNamespace : ns!.Trim();
            });

        // Views that would generate the same class as an earlier view (by path order) are reported and skipped.
        // Only the list of paths feeds this step, so editing a view's contents keeps the per-file caching.
        var duplicates = viewFiles
            .Select(static (file, _) => file.Path)
            .Collect()
            .Combine(rootNamespace)
            .Select(static (pair, _) => FindDuplicates(pair.Left, pair.Right));

        // The partial views (Views/Shared) by name, so <partial> elements can be checked and compiled to direct calls.
        // Only a partial's name and @model feed this step, so editing its body does not regenerate other views.
        var partials = viewFiles
            .Combine(rootNamespace)
            .Select(static (pair, cancellationToken) => PartialEntryFor(pair.Left, pair.Right, cancellationToken))
            .Collect()
            .Select(static (entries, _) => new PartialCatalogue(entries.Where(e => e is not null)!));

        // The controllers and their actions, so literal nav targets can be checked (CVW119). Only names feed this step,
        // so editing the body of an action does not regenerate the views.
        var routes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax c && c.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal),
                static (syntax, cancellationToken) => ControllerEntryFor((ClassDeclarationSyntax)syntax.Node, syntax.SemanticModel, cancellationToken))
            .Where(static entry => entry is not null)
            .Collect()
            .Select(static (entries, _) => new RouteCatalogue(entries!));

        // The route table: controllers and hand-written views from the compilation, views from the .cvw files.
        // Each entry is a string of C#, so the table is only written again when a signature or a view changes.
        var tableControllers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax c && c.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal),
                static (syntax, cancellationToken) => syntax.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntax.Node, cancellationToken) is INamedTypeSymbol type
                    ? RouteTableEmitter.ForController(type)
                    : null)
            .Where(static entry => entry is not null)
            .Collect();

        var tableViews = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax c && c.Identifier.ValueText.EndsWith("View", StringComparison.Ordinal),
                static (syntax, cancellationToken) => syntax.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntax.Node, cancellationToken) is INamedTypeSymbol type
                    ? RouteTableEmitter.ForView(type)
                    : null)
            .Where(static entry => entry is not null)
            .Collect();

        // Views compiled from .cvw files register themselves (a GeneratedView attribute in their own file), so only
        // views that compiled are listed; the table is still written for a project that has nothing but .cvw files
        var anyViewFiles = viewFiles.Collect().Select(static (files, _) => files.Length > 0);

        var table = tableControllers.Combine(tableViews).Combine(anyViewFiles);
        context.RegisterSourceOutput(table, static (ctx, pair) =>
        {
            var ((controllers, views), hasViewFiles) = pair;
            if (RouteTableEmitter.Emit(controllers!, views!, hasViewFiles) is { } source)
                ctx.AddSource(RouteTableEmitter.HintName, SourceText.From(source, System.Text.Encoding.UTF8));
        });

        var inputs = viewFiles.Combine(rootNamespace).Combine(duplicates).Combine(partials).Combine(routes);
        context.RegisterSourceOutput(inputs, static (ctx, pair) =>
        {
            var ((((file, rootNs), duplicateOf), catalogue), routeCatalogue) = pair;

            if (duplicateOf.TryGetValue(file.Path, out var original))
            {
                ViewInfo.TryCreate(file.Path, rootNs, out var info);
                ctx.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.DuplicateView,
                    Location.Create(file.Path, default, default), file.Path, info.FullClassName, original));
                return;
            }

            var text = file.GetText(ctx.CancellationToken);
            if (text is not null)
                GenerateView(ctx, file.Path, text, rootNs, catalogue, routeCatalogue);
        });
    }

    /// <summary>Describes a controller the way the runtime router discovers it (see <see cref="RouteSymbols"/>).</summary>
    /// <returns>The entry, or <see langword="null"/> when the class is not a controller.</returns>
    private static ControllerEntry? ControllerEntryFor(ClassDeclarationSyntax syntax, SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(syntax, cancellationToken) is not INamedTypeSymbol type
            || RouteSymbols.ControllerName(type) is not { } name)
            return null;

        return new ControllerEntry(name, RouteSymbols.ActionNames(type));
    }

    /// <summary>
    /// Returns the catalogue entry for a partial view file, or <see langword="null"/> when the file is not in
    /// <c>Views/Shared</c>.
    /// </summary>
    private static PartialEntry? PartialEntryFor(AdditionalText file, string rootNamespace, System.Threading.CancellationToken cancellationToken)
    {
        // Only partials that GenerateView will emit, so <partial> never refers to a class that does not exist
        if (!ViewInfo.TryCreate(file.Path, rootNamespace, out var info) || !info.IsPartial
            || !IsValidNamespace(rootNamespace) || !IsValidName(info.ClassName) || info.ClassName == ReservedPartialName)
            return null;

        var text = file.GetText(cancellationToken)?.ToString() ?? "";
        var document = CvwDocumentParser.Parse(text, MarkupSchema.Default);
        return new PartialEntry(info.ClassName, info.FullClassName, document.Model?.Value, file.Path, document.HasErrors,
            document.HasBodyPlaceholder);
    }

    /// <summary>
    /// Maps each view path that collides with an earlier one (same class name, ignoring case, because the router
    /// matches names case-insensitively) to the path of the earlier view.
    /// </summary>
    private static EquatableMap FindDuplicates(ImmutableArray<string> paths, string rootNamespace)
    {
        var first = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in paths.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!ViewInfo.TryCreate(path, rootNamespace, out var info))
                continue;

            if (first.TryGetValue(info.FullClassName, out var original))
                duplicates[path] = original;
            else
                first[info.FullClassName] = path;
        }

        return new EquatableMap(duplicates);
    }

    /// <summary>
    /// A read-only path map with value equality, so the incremental pipeline can tell when it has not changed.
    /// </summary>
    private sealed class EquatableMap : IEquatable<EquatableMap>
    {
        private readonly Dictionary<string, string> _map;

        public EquatableMap(Dictionary<string, string> map) => _map = map;

        public bool TryGetValue(string key, out string value) => _map.TryGetValue(key, out value!);

        public bool Equals(EquatableMap? other) =>
            other is not null && _map.Count == other._map.Count
            && _map.All(pair => other._map.TryGetValue(pair.Key, out var value) && value == pair.Value);

        public override bool Equals(object? obj) => Equals(obj as EquatableMap);

        public override int GetHashCode() => _map.Count;
    }

    /// <summary>
    /// Validates, parses and emits a single view, reporting diagnostics against the <c>.cvw</c> file.
    /// </summary>
    private static void GenerateView(SourceProductionContext context, string path, SourceText text, string rootNamespace,
        PartialCatalogue partials, RouteCatalogue routes)
    {
        if (!ViewInfo.TryCreate(path, rootNamespace, out var info))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.MisplacedView, Location.Create(path, default, default), path));
            return;
        }

        if (!IsValidNamespace(rootNamespace))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.InvalidRootNamespace, Location.Create(path, default, default), rootNamespace));
            return;
        }

        if (info.Area is { } area && !IsValidName(area))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.InvalidIdentifier, Location.Create(path, default, default), area, "area folder"));
            return;
        }

        if (!IsValidName(info.ControllerFolder))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.InvalidIdentifier, Location.Create(path, default, default), info.ControllerFolder, "controller folder"));
            return;
        }

        if (!IsValidName(info.ClassName))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.InvalidIdentifier, Location.Create(path, default, default), info.ClassName, "view"));
            return;
        }

        if (info.IsPartial && info.ClassName == ReservedPartialName)
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.ReservedPartialName, Location.Create(path, default, default), info.ClassName));
            return;
        }

        if (!info.IsPartial && !info.ClassName.EndsWith("View", StringComparison.Ordinal))
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.ViewNameSuffix, Location.Create(path, default, default), info.ClassName));

        var document = CvwDocumentParser.Parse(text.ToString(), MarkupSchema.Default);

        IReadOnlyList<ParseDiagnostic> compositionErrors = document.HasErrors
            ? []
            : [..PartialChecker.Check(document, info, partials), ..ViewChecker.Check(document, info)];

        // Warnings only: the view is still generated
        IReadOnlyList<ParseDiagnostic> routeWarnings = document.HasErrors ? [] : RouteChecker.Check(document, info, routes);

        foreach (var diagnostic in document.Diagnostics.Concat(compositionErrors).Concat(routeWarnings))
        {
            var span = TextSpan.FromBounds(
                Math.Min(diagnostic.Span.Start, text.Length),
                Math.Min(diagnostic.Span.End, text.Length));
            var location = Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, location, diagnostic.Arguments));
        }

        if (document.HasErrors || compositionErrors.Count > 0)
            return;

        context.AddSource(info.HintName, ViewEmitter.Emit(document, info, partials));
    }

    /// <summary>A name usable as a C# identifier: valid characters and not a reserved keyword such as <c>class</c>.</summary>
    private static bool IsValidName(string name) =>
        SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

    /// <summary>A dotted namespace whose every part is a valid name.</summary>
    private static bool IsValidNamespace(string ns) => ns.Split('.').All(IsValidName);
}
