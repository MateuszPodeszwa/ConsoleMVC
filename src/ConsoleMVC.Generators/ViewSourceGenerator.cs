using System;
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

        context.RegisterSourceOutput(viewFiles.Combine(rootNamespace), static (ctx, pair) =>
        {
            var (file, rootNs) = pair;
            var text = file.GetText(ctx.CancellationToken);
            if (text is not null)
                GenerateView(ctx, file.Path, text, rootNs);
        });
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

        if (!SyntaxFacts.IsValidIdentifier(info.ControllerName))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.InvalidIdentifier, Location.Create(path, default, default), info.ControllerName, "controller folder"));
            return;
        }

        if (!SyntaxFacts.IsValidIdentifier(info.ClassName))
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
}
