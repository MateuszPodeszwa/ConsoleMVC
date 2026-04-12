using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

#nullable enable

namespace ConsoleMVC.Generators;

/// <summary>
/// An incremental source generator that transforms <c>.cvw</c> (Console View) files into
/// strongly-typed <c>ConsoleView&lt;TModel&gt;</c> subclasses at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Each <c>.cvw</c> file is expected to reside at <c>Views/{Controller}/{Action}View.cvw</c>
/// and must begin with an <c>@model</c> directive declaring the fully-qualified view model type.
/// Optional <c>@using</c> directives may follow to add additional namespace imports.
/// All subsequent lines are treated as the body of the generated <c>Render</c> method.
/// </para>
/// <para>
/// The generator reads the <c>RootNamespace</c> build property from the consuming project
/// so that generated classes are placed under <c>{RootNamespace}.Views.{Controller}</c>,
/// ensuring correct namespace alignment regardless of the project name.
/// </para>
/// <para>
/// <b>Supported directives:</b>
/// <list type="bullet">
///   <item><description><c>@model FullTypeName</c> — declares the view model type (required).</description></item>
///   <item><description><c>@using Namespace</c> — adds a <c>using</c> statement to the generated class.</description></item>
/// </list>
/// </para>
/// </remarks>
[Generator]
public class ViewSourceGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Registers the source generation pipeline. Collects <c>.cvw</c> additional files
    /// and the consuming project's <c>RootNamespace</c>, then generates a
    /// <c>ConsoleView&lt;TModel&gt;</c> subclass for each view file.
    /// </summary>
    /// <param name="context">The initialisation context provided by the Roslyn compiler.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect .cvw view files from AdditionalFiles
        var viewFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".cvw", StringComparison.OrdinalIgnoreCase));

        // Read the RootNamespace build property from the consuming project
        var rootNamespace = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
                return ns ?? "ConsoleMVC";
            });

        // Pair each view file with the root namespace for code generation
        var viewsWithNamespace = viewFiles.Combine(rootNamespace);

        context.RegisterSourceOutput(viewsWithNamespace, static (ctx, pair) =>
        {
            var (file, rootNs) = pair;

            var text = file.GetText(ctx.CancellationToken);
            if (text == null) return;

            var content = text.ToString();
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            string? modelType = null;
            var usings = new List<string>();
            var codeStartIndex = 0;

            // Parse directives (@model, @using) at the top of the file
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    codeStartIndex = i + 1;
                    continue;
                }

                if (trimmed.StartsWith("@model "))
                {
                    modelType = trimmed.Substring("@model ".Length).Trim();
                    codeStartIndex = i + 1;
                    continue;
                }

                if (trimmed.StartsWith("@using "))
                {
                    usings.Add(trimmed.Substring("@using ".Length).Trim());
                    codeStartIndex = i + 1;
                    continue;
                }

                // First non-directive, non-empty line — view body code starts here
                codeStartIndex = i;
                break;
            }

            // Files without an @model directive cannot be processed
            if (modelType == null) return;

            // Extract the view and controller names from the file path
            // Expected pattern: .../Views/{Controller}/{Action}View.cvw
            var fileName = Path.GetFileNameWithoutExtension(file.Path);
            var pathParts = file.Path.Replace('\\', '/').Split('/');

            string? controllerName = null;
            for (var i = 0; i < pathParts.Length - 1; i++)
            {
                if (string.Equals(pathParts[i], "Views", StringComparison.OrdinalIgnoreCase)
                    && i + 1 < pathParts.Length - 1)
                {
                    controllerName = pathParts[i + 1];
                    break;
                }
            }

            if (controllerName == null) return;

            // Build the view body code with correct indentation
            var codeLines = lines.Skip(codeStartIndex).ToArray();
            var codeBody = string.Join("\n",
                codeLines.Select(l => "        " + l));

            // Build the using directives block
            var usingBuilder = new StringBuilder();
            usingBuilder.AppendLine("using ConsoleMVC.Mvc;");
            foreach (var u in usings)
            {
                usingBuilder.AppendLine($"using {u};");
            }

            // Emit the generated ConsoleView<TModel> subclass
            var source = $@"// <auto-generated>
// Generated by ConsoleMVC ViewEngine from {controllerName}/{fileName}.cvw
// Do not modify this file directly — edit the .cvw source instead.
// </auto-generated>
{usingBuilder}
namespace {rootNs}.Views.{controllerName}
{{
    public class {fileName} : ConsoleView<{modelType}>
    {{
        public override NavigationResult Render({modelType} Model)
        {{
{codeBody}
        }}
    }}
}}";

            ctx.AddSource($"{controllerName}_{fileName}.g.cs", source);
        });
    }
}
