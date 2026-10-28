using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConsoleMVC.Generators.Analysis;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Emit;
using ConsoleMVC.Generators.Parsing;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;
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
            .Combine(context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider)
                .Select(static (pair, _) => ModuleRoutes(pair.Left, pair.Right.GlobalOptions)))
            .Select(static (pair, _) => new RouteCatalogue(pair.Left.OfType<ControllerEntry>().Concat(pair.Right.Controllers), pair.Right.IsModule));

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

    /// <summary>
    /// The controllers of the ConsoleMVC modules the compilation references (assemblies with a generated route table,
    /// which <c>builder.AddModule</c> adds), so <c>nav="Billing/Invoices/Index"</c> is known; and whether the compilation
    /// is itself a module (see <see cref="Analysis.RouteSymbols.IsModule"/>), which cannot see the controllers of the
    /// application that adds it.
    /// </summary>
    private static ModuleRouteSet ModuleRoutes(Compilation compilation, Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions options) =>
        new(Analysis.RouteSymbols.ModuleControllers(compilation).ToImmutableArray(), Analysis.RouteSymbols.IsModule(compilation, options));

    /// <summary>The controllers of referenced modules, value-equal so the generator's cache holds while they do not change.</summary>
    private sealed class ModuleRouteSet(ImmutableArray<ControllerEntry> controllers, bool isModule) : IEquatable<ModuleRouteSet>
    {
        public ImmutableArray<ControllerEntry> Controllers { get; } = controllers;

        public bool IsModule { get; } = isModule;

        public bool Equals(ModuleRouteSet? other) => other is not null && IsModule == other.IsModule && Controllers.SequenceEqual(other.Controllers);

        public override bool Equals(object? obj) => Equals(obj as ModuleRouteSet);

        public override int GetHashCode() => Controllers.Length;
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
    private static PartialEntry? PartialEntryFor(AdditionalText file, string rootNamespace, System.Threading.CancellationToken cancellationToken) =>
        ViewInfo.TryCreate(file.Path, rootNamespace, out var info) && info.IsPartial
            ? PartialEntryFor(file.Path, file.GetText(cancellationToken)?.ToString() ?? "", rootNamespace)
            : null;

    /// <summary>
    /// Returns the catalogue entry for the shared view at <paramref name="path"/> (a partial view or a layout), or
    /// <see langword="null"/> when the file is not in <c>Views/Shared</c> or is not generated. The <c>cvw-preview</c> tool
    /// builds its catalogue with this too, so it checks <c>&lt;partial&gt;</c> and <c>@layout</c> as the build does.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <param name="text">The file's text.</param>
    /// <param name="rootNamespace">The project's root namespace.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    /// <example><code>var header = ViewSourceGenerator.PartialEntryFor("/app/Views/Shared/Header.cvw", "&lt;text&gt;Shop&lt;/text&gt;", "MyApp");</code></example>
    internal static PartialEntry? PartialEntryFor(string path, string text, string rootNamespace)
    {
        // Only partials that GenerateView will emit, so <partial> never refers to a class that does not exist
        if (!ViewInfo.TryCreate(path, rootNamespace, out var info) || !info.IsPartial
            || !IsValidNamespace(rootNamespace) || !IsValidName(info.ClassName) || info.ClassName == ReservedPartialName)
            return null;

        var document = CvwDocumentParser.Parse(text, MarkupSchema.Default);
        return new PartialEntry(info.ClassName, info.FullClassName, document.Model?.Value, path, document.HasErrors,
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
        var problems = CheckFile(path, rootNamespace, out var info);
        foreach (var problem in problems)
            context.ReportDiagnostic(Diagnostic.Create(problem.Descriptor, Location.Create(path, default, default), problem.Arguments));

        if (info is null)
            return;

        var document = CvwDocumentParser.Parse(text.ToString(), MarkupSchema.Default);
        var diagnostics = CheckMarkup(document, info, partials, routes);

        foreach (var diagnostic in diagnostics)
        {
            var span = TextSpan.FromBounds(
                Math.Min(diagnostic.Span.Start, text.Length),
                Math.Min(diagnostic.Span.End, text.Length));
            var location = Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, location, diagnostic.Arguments));
        }

        if (diagnostics.Any(d => d.IsError))
            return;

        context.AddSource(info.HintName, ViewEmitter.Emit(document, info, partials));
    }

    /// <summary>
    /// Checks where a view file is and what it is called, before it is read: CVW001, CVW002, CVW003, CVW005 and CVW009,
    /// all reported at the start of the file. The <c>cvw-preview</c> tool calls this too, so it reports them as the build
    /// does.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <param name="rootNamespace">The project's root namespace.</param>
    /// <param name="view">The view, or <see langword="null"/> when it is not generated (and so its markup is not checked).</param>
    /// <returns>The diagnostics, at offset 0.</returns>
    /// <example>
    /// <code>
    /// var problems = ViewSourceGenerator.CheckFile("/app/Views/Home/Index.cvw", "MyApp", out var view);
    /// // CVW003: the name does not end with View; view is not null, so the markup is checked as well
    /// </code>
    /// </example>
    internal static IReadOnlyList<ParseDiagnostic> CheckFile(string path, string rootNamespace, out ViewInfo? view)
    {
        view = null;
        var start = new SourceSpan(0, 0);
        if (!ViewInfo.TryCreate(path, rootNamespace, out var info))
            return [new ParseDiagnostic(CvwDiagnostics.MisplacedView, start, path)];

        if (!IsValidNamespace(rootNamespace))
            return [new ParseDiagnostic(CvwDiagnostics.InvalidRootNamespace, start, rootNamespace)];

        if (info.Area is { } area && !IsValidName(area))
            return [new ParseDiagnostic(CvwDiagnostics.InvalidIdentifier, start, area, "area folder")];

        if (!IsValidName(info.ControllerFolder))
            return [new ParseDiagnostic(CvwDiagnostics.InvalidIdentifier, start, info.ControllerFolder, "controller folder")];

        if (!IsValidName(info.ClassName))
            return [new ParseDiagnostic(CvwDiagnostics.InvalidIdentifier, start, info.ClassName, "view")];

        if (info.IsPartial && info.ClassName == ReservedPartialName)
            return [new ParseDiagnostic(CvwDiagnostics.ReservedPartialName, start, info.ClassName)];

        view = info;
        return !info.IsPartial && !info.ClassName.EndsWith("View", StringComparison.Ordinal)
            ? [new ParseDiagnostic(CvwDiagnostics.ViewNameSuffix, start, info.ClassName)]
            : [];
    }

    /// <summary>
    /// Returns every diagnostic of a parsed view: the parser's, then, when it has no errors, the checks that need the
    /// project (partials and layouts, CVW010, CVW103 and CVW114 to CVW118, and literal <c>nav</c> targets, CVW119). The
    /// view is generated only when none is an error. The <c>cvw-preview</c> tool calls this too, without controllers.
    /// </summary>
    /// <param name="document">The parsed view.</param>
    /// <param name="view">Where the view lives.</param>
    /// <param name="partials">The project's partial views and layouts.</param>
    /// <param name="routes">The project's controllers; without any, <c>nav</c> targets are not checked.</param>
    /// <returns>The diagnostics, in the order they are reported.</returns>
    /// <example><code>var diagnostics = ViewSourceGenerator.CheckMarkup(document, view, partials, new RouteCatalogue([]));</code></example>
    internal static IReadOnlyList<ParseDiagnostic> CheckMarkup(CvwDocument document, ViewInfo view, PartialCatalogue partials, RouteCatalogue routes)
    {
        if (document.HasErrors)
            return document.Diagnostics;

        // Route warnings come last: they never stop the view from being generated
        return [.. document.Diagnostics, .. PartialChecker.Check(document, view, partials), .. ViewChecker.Check(document, view),
            .. RouteChecker.Check(document, view, routes)];
    }

    /// <summary>A name usable as a C# identifier: valid characters and not a reserved keyword such as <c>class</c>.</summary>
    private static bool IsValidName(string name) =>
        SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

    /// <summary>A dotted namespace whose every part is a valid name.</summary>
    private static bool IsValidNamespace(string ns) => ns.Split('.').All(IsValidName);
}
