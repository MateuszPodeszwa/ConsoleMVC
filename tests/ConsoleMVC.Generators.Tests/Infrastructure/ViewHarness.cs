using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ConsoleMVC.Generators.Tests.Infrastructure;

/// <summary>
/// The outcome of running the generator and compiling its output.
/// </summary>
/// <param name="GeneratorDiagnostics">Diagnostics reported by the view generator (CVWxxx).</param>
/// <param name="CompilationDiagnostics">Diagnostics from compiling the generated code.</param>
/// <param name="GeneratedSource">The generated C# source, or <see langword="null"/> if none was produced.</param>
/// <param name="Assembly">The compiled assembly, or <see langword="null"/> when compilation failed.</param>
public sealed record GenerationResult(
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    string? GeneratedSource,
    Assembly? Assembly)
{
    /// <summary>Gets every error from the generator or the compiler.</summary>
    public IEnumerable<Diagnostic> Errors =>
        GeneratorDiagnostics.Concat(CompilationDiagnostics).Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>Formats all diagnostics plus the generated source, for assertion messages.</summary>
    public string Describe()
    {
        var builder = new StringBuilder();
        foreach (var diagnostic in GeneratorDiagnostics.Concat(CompilationDiagnostics))
            builder.AppendLine(diagnostic.ToString());

        builder.AppendLine("----- generated -----").AppendLine(GeneratedSource);
        return builder.ToString();
    }
}

/// <summary>
/// Runs <see cref="ViewSourceGenerator"/> over an in-memory <c>.cvw</c> file, compiles the result against
/// the real ConsoleMVC runtime and optionally executes the view with a plain-text renderer.
/// </summary>
/// <example>
/// <code>
/// var output = ViewHarness.Render("@model string\n&lt;text&gt;Hi @Model&lt;/text&gt;\nreturn NavigationResult.Quit();", "\"Ada\"");
/// Assert.Equal("Hi Ada\n", output);
/// </code>
/// </example>
public static class ViewHarness
{
    /// <summary>The default location of the view under test.</summary>
    public const string DefaultPath = "/app/Views/Home/IndexView.cvw";

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(LoadReferences);

    /// <summary>
    /// Runs the generator and compiles the output.
    /// </summary>
    /// <param name="cvw">The contents of the <c>.cvw</c> file.</param>
    /// <param name="path">The file path, which determines controller and class names.</param>
    /// <param name="extraSource">Additional C# compiled alongside (e.g. model classes).</param>
    /// <returns>The generator and compiler results.</returns>
    public static GenerationResult Generate(string cvw, string path = DefaultPath, string extraSource = "")
    {
        var compilation = CSharpCompilation.Create(
            "TestApp_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(extraSource, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest))],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new ViewSourceGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(path, cvw)],
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            optionsProvider: new RootNamespaceOptionsProvider("TestApp"));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        var generated = driver.GetRunResult().GeneratedTrees.FirstOrDefault()?.ToString();

        using var stream = new MemoryStream();
        var emit = output.Emit(stream);
        var assembly = emit.Success ? Assembly.Load(stream.ToArray()) : null;

        return new GenerationResult(generatorDiagnostics, emit.Diagnostics, generated, assembly);
    }

    /// <summary>
    /// Generates, compiles and renders a view at <c>Views/Home/IndexView.cvw</c>.
    /// </summary>
    /// <param name="cvw">The contents of the <c>.cvw</c> file.</param>
    /// <param name="modelExpression">A C# expression creating the model (compiled with <paramref name="extraSource"/>).</param>
    /// <param name="extraSource">Additional C#, such as model classes in namespace <c>TestApp</c>.</param>
    /// <param name="width">The renderer width.</param>
    /// <param name="input">Lines of user input for <c>&lt;input&gt;</c> and <c>&lt;menu&gt;</c>.</param>
    /// <returns>The plain-text output and the navigation result returned by the view.</returns>
    /// <exception cref="Xunit.Sdk.XunitException">Thrown when generation or compilation fails.</exception>
    public static (string Output, NavigationResult Navigation) RenderWithNavigation(
        string cvw, string modelExpression = "null", string extraSource = "", int width = 40, params string[] input)
    {
        var factory = "namespace TestApp { public static class ModelFactory { public static object? Create() => " + modelExpression + "; } }";
        var result = Generate(cvw, extraSource: extraSource + "\n" + factory);

        if (result.Assembly is null || result.Errors.Any())
            Assert.Fail("View failed to compile:\n" + result.Describe());

        var view = (ConsoleView)Activator.CreateInstance(result.Assembly!.GetType("TestApp.Views.Home.IndexView", throwOnError: true)!)!;
        var writer = new StringWriter { NewLine = "\n" };
        view.Renderer = new MarkupRenderer(new PlainTextRenderTarget(writer), () => width);
        view.Input = new ScriptedInput(input);

        var model = result.Assembly.GetType("TestApp.ModelFactory")!.GetMethod("Create")!.Invoke(null, null);
        var navigation = view.RenderInternal(model);

        return (writer.ToString(), navigation);
    }

    /// <summary>
    /// Generates, compiles and renders a view, returning only its plain-text output.
    /// </summary>
    /// <inheritdoc cref="RenderWithNavigation"/>
    public static string Render(string cvw, string modelExpression = "null", string extraSource = "", int width = 40,
        params string[] input) =>
        RenderWithNavigation(cvw, modelExpression, extraSource, width, input).Output;

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).ToList();
        paths.Add(typeof(ConsoleView).Assembly.Location);

        return
        [
            ..paths.Where(p => p.Length > 0)
                .GroupBy(Path.GetFileName)
                .Select(g => MetadataReference.CreateFromFile(g.First()))
        ];
    }

    /// <summary>Scripted user input: returns the given lines, then end of input.</summary>
    private sealed class ScriptedInput(string[] lines) : ITextInput
    {
        private readonly Queue<string> _lines = new(lines);

        public string? ReadLine() => _lines.Count > 0 ? _lines.Dequeue() : null;

        public string? ReadMasked(char mask) => ReadLine();
    }

    /// <summary>An additional file held in memory.</summary>
    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }

    /// <summary>Supplies the <c>RootNamespace</c> build property, as MSBuild would.</summary>
    private sealed class RootNamespaceOptionsProvider(string rootNamespace) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(rootNamespace);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

        private sealed class Options(string rootNamespace) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                value = rootNamespace;
                return key == "build_property.RootNamespace";
            }
        }
    }
}
