using System.Text.RegularExpressions;

namespace ConsoleMVC.Preview.Tests;

/// <summary>
/// Previews compared with the real application: the same views are compiled by the real generator and run with the
/// real runtime in a terminal whose input has ended, and the preview must draw what the application drew. A preview
/// goes on past the point where the application stopped (the first prompt), so the application's screen must be the
/// start of the preview.
/// </summary>
public sealed partial class PreviewMatchesTheAppTests : IDisposable
{
    private const string Quit = "\nreturn NavigationResult.Quit();";
    private const string Layout = "<text>== header ==</text>\n<body />\n<text>== footer ==</text>";

    private readonly string _project = Directory.CreateTempSubdirectory("cvw-match-").FullName;

    public void Dispose() => Directory.Delete(_project, recursive: true);

    private static (string, string) View(string text) => (RealApp.ViewPath, text);

    private static (string, string) Shared(string name, string text) => ($"{RealApp.Project}/Views/Shared/{name}.cvw", text);

    /// <summary>Previews the view at <see cref="RealApp.ViewPath"/> with the files written to a project on disk.</summary>
    private string Preview((string Path, string Text)[] files, PreviewFormat format = PreviewFormat.Text)
    {
        string? viewPath = null;
        foreach (var (path, text) in files)
        {
            var local = Path.Combine(_project, Path.GetRelativePath(RealApp.Project, path));
            Directory.CreateDirectory(Path.GetDirectoryName(local)!);
            File.WriteAllText(local, text);
            if (path == RealApp.ViewPath)
                viewPath = local;
        }

        var result = CvwPreview.Render(File.ReadAllText(viewPath!), ProjectViewFiles.ForView(viewPath!), new PreviewOptions(Width: 40, Format: format));
        return result.Output;
    }

    /// <summary>The lines of a screen, without colours, trailing spaces or trailing empty lines.</summary>
    private static List<string> Lines(string screen)
    {
        var lines = Ansi().Replace(screen, "").ReplaceLineEndings("\n").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    [GeneratedRegex("\u001b\\[[0-9;?]*[A-Za-z]")]
    private static partial Regex Ansi();

    /// <summary>Asserts that the preview starts with what the application drew.</summary>
    private void AssertPreviewStartsLikeTheApp(params (string Path, string Text)[] files)
    {
        var app = Lines(RealApp.Run(files));
        var preview = Lines(Preview(files));
        Assert.True(preview.Count >= app.Count && preview.Take(app.Count).SequenceEqual(app),
            $"The application drew:\n{string.Join("\n", app)}\n\nThe preview drew:\n{string.Join("\n", preview)}");
    }

    /// <summary>Asserts that the preview is exactly what the application drew.</summary>
    private void AssertPreviewIsTheApp(params (string Path, string Text)[] files) =>
        Assert.Equal(Lines(RealApp.Run(files)), Lines(Preview(files)));

    // ------------------------------------------------------------------ the layout's footer

    [Fact]
    public void TheFooter_IsDrawnBeforeAMenu()
    {
        AssertPreviewIsTheApp(
            View("<text>content</text>\n<menu><option key=\"1\" nav=\"About\">About</option><option key=\"q\" quit>Quit</option></menu>"),
            Shared("_Layout", Layout));
    }

    [Fact]
    public void TheFooter_IsDrawnBeforeTheFirstField()
    {
        AssertPreviewStartsLikeTheApp(
            View("<text>content</text>\n<input name=\"Name\" label=\"Name: \" />\n<text>after</text>" + Quit),
            Shared("_Layout", Layout));
    }

    [Fact]
    public void TheFooter_IsDrawnBeforeTheFirstFieldOfAPartial()
    {
        AssertPreviewStartsLikeTheApp(
            View("<text>content</text>\n<partial name=\"Form\" />\n<text>after</text>" + Quit),
            Shared("Form", "<text>form</text>\n<input name=\"Name\" label=\"Name: \" />"),
            Shared("_Layout", Layout));
    }

    [Fact]
    public void TheFooter_IsDrawnAtTheEnd_WhenTheViewNeverWaits()
    {
        AssertPreviewIsTheApp(View("<text>content</text>\n<text>more</text>" + Quit), Shared("_Layout", Layout));
    }

    [Fact]
    public void TheFooter_IsDrawnOnce_BeforeTheFirstOfSeveralFields()
    {
        // The application stops at the first field; the preview must not repeat the footer at a later one
        var preview = Lines(Preview(
        [
            View("<input name=\"A\" label=\"A: \" />\n<input name=\"B\" label=\"B: \" />" + Quit),
            Shared("_Layout", Layout)
        ]));
        Assert.Single(preview, l => l == "== footer ==");
        AssertPreviewStartsLikeTheApp(View("<input name=\"A\" label=\"A: \" />\n<input name=\"B\" label=\"B: \" />" + Quit),
            Shared("_Layout", Layout));
    }
}
