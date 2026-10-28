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

    /// <summary>Previews the view at <paramref name="view"/> with the files written to a project on disk.</summary>
    private string Preview((string Path, string Text)[] files, PreviewFormat format = PreviewFormat.Text, string view = RealApp.ViewPath)
    {
        string? viewPath = null;
        foreach (var (path, text) in files)
        {
            var local = Path.Combine(_project, Path.GetRelativePath(RealApp.Project, path));
            Directory.CreateDirectory(Path.GetDirectoryName(local)!);
            File.WriteAllText(local, text);
            if (path == view)
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

    // ------------------------------------------------------------------ fields, as a terminal first shows them

    [Theory]
    [InlineData("<checkbox name=\"News\" label=\"Send me news\" checked />")]
    [InlineData("<checkbox name=\"News\" label=\"Send me news\" />")]
    [InlineData("<number name=\"Count\" label=\"Count:\" default=\"1\" />")]
    [InlineData("<number name=\"Count\" label=\"Count:\" />")]
    [InlineData("<date name=\"Due\" label=\"Due:\" default=\"today+7\" />")]
    [InlineData("<date name=\"Due\" label=\"Due:\" />")]
    [InlineData("<select name=\"Colour\" label=\"Colour:\" default=\"g\"><choice value=\"r\">Red</choice><choice value=\"g\">Green</choice><choice value=\"b\">Blue</choice></select>")]
    [InlineData("<select name=\"Colour\" label=\"Colour:\" multiple default=\"r,b\"><choice value=\"r\">Red</choice><choice value=\"g\">Green</choice><choice value=\"b\">Blue</choice></select>")]
    [InlineData("<radio name=\"Size\" label=\"Size:\" default=\"M\"><choice>S</choice><choice>M</choice><choice>L</choice></radio>")]
    [InlineData("<input name=\"Name\" label=\"Name: \" default=\"Ada\" />")]
    [InlineData("<menu prompt=\"Pick:\"><option key=\"1\" nav=\"About\">About</option><option key=\"q\" quit>Quit</option></menu>")]
    public void AField_IsDrawnAsTheApplicationFirstDrawsIt(string field)
    {
        AssertPreviewStartsLikeTheApp(View("<text>Form</text>\n" + field + Quit));
    }

    [Fact]
    public void ADateWithAComputedDefault_IsDrawnWithToday()
    {
        // The model's date is unknown without running the action; the field is still drawn, as without a default
        var preview = Lines(Preview([View("<date name=\"Due\" label=\"Due:\" default=\"@Model.Due\" min=\"@Model.From\" />" + Quit)]));
        Assert.Equal(Lines(RealApp.Run([View("<date name=\"Due\" label=\"Due:\" />" + Quit)])), preview);
    }

    [Fact]
    public void AField_HasTheApplicationsColours()
    {
        // The ANSI preview is the application's output, escape sequences included, up to where it waits
        var files = new[] { View("<select name=\"C\" label=\"Colour:\" default=\"b\"><choice value=\"r\">Red</choice><choice value=\"b\">Blue</choice></select>" + Quit) };
        Assert.Equal(RealApp.Run(files), Preview(files, PreviewFormat.Ansi).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void EachField_IsDrawnAsItFirstWaits()
    {
        // The application waits at the first field; the preview shows every field as it will first be drawn
        var preview = Lines(Preview(
        [
            View("<checkbox name=\"A\" label=\"First\" checked />\n<input name=\"B\" label=\"Name: \" />\n<checkbox name=\"C\" label=\"Third\" />" + Quit)
        ]));
        Assert.Equal(Lines(RealApp.Run([View("<checkbox name=\"A\" label=\"First\" checked />" + Quit)]))
            .Concat(Lines(RealApp.Run([View("<input name=\"B\" label=\"Name: \" />" + Quit)])))
            .Concat(Lines(RealApp.Run([View("<checkbox name=\"C\" label=\"Third\" />" + Quit)]))), preview);
    }

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
    public void AViewInAnAreasSharedFolder_IsAnOrdinaryView_DrawnInTheLayout()
    {
        const string view = RealApp.Project + "/Areas/Admin/Views/Shared/HelpView.cvw";
        (string, string)[] files = [(view, "<text>help</text>" + Quit), Shared("_Layout", Layout)];
        Assert.Equal(Lines(RealApp.Run(files, view)), Lines(Preview(files, view: view)));
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
