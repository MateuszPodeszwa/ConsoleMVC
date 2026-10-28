using ConsoleMVC.Generators.Emit;
using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Preview.Tests;

/// <summary>
/// What the real build and the real application make of a set of views: the generator's diagnostics, and the screen a
/// compiled view draws. Tests compare a preview with these, so they check the preview against the framework rather
/// than against an expectation written by hand.
/// </summary>
/// <example>
/// <code>
/// var files = new[] { ("/app/Views/Home/IndexView.cvw", "&lt;partial name=\"Gone\" /&gt;") };
/// var built = RealApp.Diagnostics(files, "/app/Views/Home/IndexView.cvw"); // CVW114 at 1:10
/// </code>
/// </example>
internal static class RealApp
{
    /// <summary>The project folder the tests' views live in.</summary>
    public const string Project = "/app";

    /// <summary>The usual view under test.</summary>
    public const string ViewPath = Project + "/Views/Home/IndexView.cvw";

    /// <summary>The root namespace the views are compiled in.</summary>
    private const string RootNamespace = "TestApp";

    /// <summary>
    /// Runs the real generator over <paramref name="files"/> and returns what it reports in the file at
    /// <paramref name="path"/>, in the preview's form, ordered by position.
    /// </summary>
    /// <param name="files">The project's <c>.cvw</c> files as (path, text) pairs.</param>
    /// <param name="path">The file whose diagnostics are wanted.</param>
    /// <param name="source">C# compiled with the views, such as controllers.</param>
    /// <returns>The diagnostics the build reports in that file.</returns>
    public static IReadOnlyList<PreviewDiagnostic> Diagnostics(IEnumerable<(string Path, string Text)> files, string path,
        string source = "")
    {
        var result = ViewHarness.GenerateFiles(files, source, RootNamespace);
        return result.GeneratorDiagnostics
            .Where(d => d.Location.GetLineSpan().Path == path)
            .Select(d =>
            {
                var start = d.Location.GetLineSpan().StartLinePosition;
                return new PreviewDiagnostic(d.Id, d.Severity == DiagnosticSeverity.Error, start.Line + 1, start.Character + 1, d.GetMessage());
            })
            .OrderBy(d => d.Line).ThenBy(d => d.Column).ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Compiles <paramref name="files"/> with the real generator and runs the view at <paramref name="path"/> until it
    /// first waits for an answer that never comes (the input ends at once), or returns.
    /// </summary>
    /// <param name="files">The project's <c>.cvw</c> files as (path, text) pairs.</param>
    /// <param name="path">The view to run.</param>
    /// <param name="terminal">
    /// <see langword="true"/> for a terminal (ANSI output that can redraw, and key presses), <see langword="false"/>
    /// for plain output and typed answers, as when the input is piped.
    /// </param>
    /// <param name="width">The width in columns.</param>
    /// <returns>Everything the view wrote.</returns>
    public static string Run(IEnumerable<(string Path, string Text)> files, string path = ViewPath, bool terminal = true,
        int width = 40)
    {
        var result = ViewHarness.GenerateFiles(files, "", RootNamespace);
        if (result.Assembly is null || result.Errors.Any())
            Assert.Fail("The views do not compile:\n" + result.Describe());

        Assert.True(ViewInfo.TryCreate(path, RootNamespace, out var info));
        var view = (ConsoleView)Activator.CreateInstance(result.Assembly!.GetType(info.FullClassName, throwOnError: true)!)!;

        var writer = new StringWriter { NewLine = "\n" };
        IRenderTarget target = terminal ? new AnsiRenderTarget(writer, ColourSupport.TrueColour) : new PlainTextRenderTarget(writer);
        view.Renderer = new MarkupRenderer(target, () => width);
        view.Input = new EndedInput(terminal);

        try
        {
            view.RenderInternal(null);
        }
        catch (InputEndedException)
        {
            // A form field waited for an answer: the screen up to its prompt is what the user saw
        }

        return writer.ToString();
    }

    /// <summary>Input that has already ended, read as key presses in a terminal or as lines otherwise.</summary>
    private sealed class EndedInput(bool keys) : ITextInput
    {
        public bool CanReadKeys => keys;

        public string? ReadLine() => null;

        public string? ReadMasked(char mask) => null;

        public ConsoleKeyInfo? ReadKey() => null;
    }
}
