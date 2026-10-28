using ConsoleMVC.Generators.Parsing;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Preview;

/// <summary>The output of <c>cvw-preview</c>.</summary>
internal enum PreviewFormat
{
    /// <summary>Plain text, without colours.</summary>
    Text,

    /// <summary>Text with ANSI escape sequences, for a terminal.</summary>
    Ansi,

    /// <summary>An HTML fragment for an editor's preview panel.</summary>
    Html
}

/// <summary>How a preview is drawn.</summary>
/// <param name="Width">The width in columns, from <see cref="CvwPreview.MinimumWidth"/> to <see cref="CvwPreview.MaximumWidth"/>.</param>
/// <param name="Format">The output.</param>
/// <param name="HighContrast">Draws with <see cref="Theme.CreateHighContrast"/> instead of the default theme.</param>
/// <example><code>var options = new PreviewOptions(Width: 100, Format: PreviewFormat.Html);</code></example>
internal sealed record PreviewOptions(int Width = 80, PreviewFormat Format = PreviewFormat.Text, bool HighContrast = false);

/// <summary>A problem found in the view: a build diagnostic of the markup, with where it is.</summary>
/// <param name="Id">The diagnostic, such as <c>CVW105</c>.</param>
/// <param name="IsError">Whether it is an error (the build fails) rather than a warning.</param>
/// <param name="Line">The line, counted from 1.</param>
/// <param name="Column">The column, counted from 1.</param>
/// <param name="Message">What is wrong.</param>
/// <example><code>var text = $"{d.Line}:{d.Column} {d.Id} {d.Message}";</code></example>
internal sealed record PreviewDiagnostic(string Id, bool IsError, int Line, int Column, string Message);

/// <summary>A drawn preview.</summary>
/// <param name="Output">The screen, in the chosen format.</param>
/// <param name="Diagnostics">The markup's diagnostics, as the build reports them.</param>
/// <param name="Notes">What the preview could not show, such as a partial view that was not found.</param>
internal sealed record PreviewResult(string Output, IReadOnlyList<PreviewDiagnostic> Diagnostics, IReadOnlyList<string> Notes);

/// <summary>
/// Draws a <c>.cvw</c> view without building or running the application: parses it with the generator's parser, builds
/// its elements (<see cref="PreviewBuilder"/>) and renders them with the real rendering engine.
/// See docs/design/editor-support.md, section 3.2.
/// </summary>
/// <example>
/// <code>
/// var result = CvwPreview.Render(File.ReadAllText(path), ProjectViewFiles.ForView(path), new PreviewOptions(Width: 60));
/// Console.Write(result.Output);
/// </code>
/// </example>
internal static class CvwPreview
{
    /// <summary>The narrowest preview, in columns.</summary>
    public const int MinimumWidth = 20;

    /// <summary>The widest preview, in columns.</summary>
    public const int MaximumWidth = 500;

    /// <summary>Draws the view <paramref name="text"/>.</summary>
    /// <param name="text">The view's text, which may be unsaved.</param>
    /// <param name="files">Where its partial views and layout are read from.</param>
    /// <param name="options">The width, format and theme.</param>
    /// <returns>The screen, the diagnostics and notes.</returns>
    public static PreviewResult Render(string text, IViewFiles files, PreviewOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(options);

        var document = CvwDocumentParser.Parse(text, MarkupSchema.Default);
        var lines = new LineIndex(text);
        var diagnostics = document.Diagnostics
            .Select(d =>
            {
                var (line, column) = lines.Locate(d.Span.Start);
                return new PreviewDiagnostic(d.Descriptor.Id, d.IsError, line, column, d.Message);
            })
            .ToList();

        var notes = new List<string>();
        var theme = options.HighContrast ? Theme.CreateHighContrast() : Theme.CreateDefault();
        var elements = new PreviewBuilder(theme, files, notes).BuildView(document);

        var width = Math.Clamp(options.Width, MinimumWidth, MaximumWidth);
        var output = new StringWriter();
        var html = options.Format == PreviewFormat.Html ? new HtmlRenderTarget(output) : null;
        IRenderTarget target = options.Format switch
        {
            PreviewFormat.Html => html!,
            PreviewFormat.Ansi => new AnsiRenderTarget(output, ColourSupport.TrueColour),
            _ => new PlainTextRenderTarget(output)
        };

        using var renderer = new MarkupRenderer(target, () => width);
        foreach (var element in elements)
        {
            try
            {
                renderer.Render(element);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException)
            {
                // A view being typed can hold values the element cannot draw; the rest of the screen is still useful
                notes.Add($"A <{element.GetType().Name}> could not be drawn: {e.Message}");
            }
        }

        html?.Finish();
        target.Flush();
        return new PreviewResult(output.ToString(), diagnostics, notes);
    }

    /// <summary>Finds the line and column of an offset in the text.</summary>
    private sealed class LineIndex(string text)
    {
        private readonly int[] _starts = [0, .. text.Select((c, i) => (c, i)).Where(p => p.c == '\n').Select(p => p.i + 1)];

        public (int Line, int Column) Locate(int offset)
        {
            var index = Array.BinarySearch(_starts, Math.Clamp(offset, 0, text.Length));
            var line = index >= 0 ? index : ~index - 1;
            return (line + 1, offset - _starts[line] + 1);
        }
    }
}
