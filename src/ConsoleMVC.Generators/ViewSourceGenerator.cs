using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Emit;
using ConsoleMVC.Generators.Parsing;
using ConsoleMVC.Generators.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace ConsoleMVC.Generators;

/// <summary>
/// An incremental source generator that transforms <c>.cvw</c> (Console View) files into
/// strongly-typed <c>ConsoleView&lt;TModel&gt;</c> subclasses at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Each <c>.cvw</c> file must reside at <c>Views/{Controller}/{Action}View.cvw</c>. It may begin with an
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

        context.RegisterSourceOutput(viewFiles.Combine(rootNamespace).Combine(duplicates), static (ctx, pair) =>
        {
            var ((file, rootNs), duplicateOf) = pair;

            if (duplicateOf.TryGetValue(file.Path, out var original))
            {
                ViewInfo.TryCreate(file.Path, rootNs, out var info);
                ctx.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.DuplicateView,
                    Location.Create(file.Path, default, default), file.Path, info.FullClassName, original));
                return;
            }

            var text = file.GetText(ctx.CancellationToken);
            if (text is not null)
                GenerateView(ctx, file.Path, text, rootNs);
        });
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
    private static void GenerateView(SourceProductionContext context, string path, SourceText text, string rootNamespace)
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

        if (!IsValidName(info.ControllerName))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.InvalidIdentifier, Location.Create(path, default, default), info.ControllerName, "controller folder"));
            return;
        }

        if (!IsValidName(info.ClassName))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.InvalidIdentifier, Location.Create(path, default, default), info.ClassName, "view"));
            return;
        }

        if (!info.ClassName.EndsWith("View", StringComparison.Ordinal))
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.ViewNameSuffix, Location.Create(path, default, default), info.ClassName));

        var document = CvwDocumentParser.Parse(text.ToString(), MarkupSchema.Default);

        foreach (var diagnostic in document.Diagnostics)
        {
            var span = TextSpan.FromBounds(
                Math.Min(diagnostic.Span.Start, text.Length),
                Math.Min(diagnostic.Span.End, text.Length));
            var location = Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, location, diagnostic.Arguments));
        }

        if (document.HasErrors)
            return;

        context.AddSource(info.HintName, ViewEmitter.Emit(document, info));
    }

    /// <summary>A name usable as a C# identifier: valid characters and not a reserved keyword such as <c>class</c>.</summary>
    private static bool IsValidName(string name) =>
        SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

    /// <summary>A dotted namespace whose every part is a valid name.</summary>
    private static bool IsValidNamespace(string ns) => ns.Split('.').All(IsValidName);
}
