namespace ConsoleMVC.Preview.Tests;

/// <summary>
/// The preview's diagnostics compared with the real build's: the same files are given to the real generator and, on
/// disk, to the preview, which must report the same diagnostics (id, severity, line, column and message) for the
/// previewed view.
/// </summary>
public sealed class PreviewDiagnosticsTests : IDisposable
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private readonly string _project = Directory.CreateTempSubdirectory("cvw-diagnostics-").FullName;

    public void Dispose() => Directory.Delete(_project, recursive: true);

    private static (string, string) Shared(string name, string text) => ($"{RealApp.Project}/Views/Shared/{name}.cvw", text);

    /// <summary>Writes <paramref name="files"/> under the test's project folder, returning where each one went.</summary>
    private string Local(string path) => Path.Combine(_project, Path.GetRelativePath(RealApp.Project, path));

    private void WriteAll(IEnumerable<(string Path, string Text)> files)
    {
        foreach (var (path, text) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Local(path))!);
            File.WriteAllText(Local(path), text);
        }
    }

    private PreviewResult Preview(string path, string text) =>
        CvwPreview.Render(text, ProjectViewFiles.ForView(Local(path)), new PreviewOptions());

    /// <summary>
    /// Asserts that the preview of <paramref name="path"/> reports what the build reports, and that the build reports
    /// <paramref name="expected"/> (so the test checks the case it is named after).
    /// </summary>
    private PreviewResult AssertSameAsTheBuild(string? expected, string path, params (string Path, string Text)[] files)
    {
        var built = RealApp.Diagnostics(files, path);
        if (expected is null)
            Assert.Empty(built);
        else
            Assert.Contains(built, d => d.Id == expected);

        WriteAll(files);
        var result = Preview(path, files.Single(f => f.Path == path).Text);

        // A message naming the file (CVW001) names it where the preview found it, with the system's separators (the
        // build was given RealApp's '/' paths)
        var previewed = result.Diagnostics
            .Select(d => d with { Message = d.Message.Replace(_project, RealApp.Project, StringComparison.Ordinal).Replace('\\', '/') })
            .OrderBy(d => d.Line).ThenBy(d => d.Column).ThenBy(d => d.Id, StringComparer.Ordinal);
        Assert.Equal(built, previewed);
        return result;
    }

    private PreviewResult AssertViewSameAsTheBuild(string? expected, string view, params (string Path, string Text)[] shared) =>
        AssertSameAsTheBuild(expected, RealApp.ViewPath, [(RealApp.ViewPath, view), .. shared]);

    // ------------------------------------------------------------------ partial views

    [Fact]
    public void AnUnknownPartial_IsCVW114()
    {
        AssertViewSameAsTheBuild("CVW114", "<text>x</text>\n<partial name=\"Gone\" />" + Quit, Shared("Header", "<text>h</text>"));
    }

    [Fact]
    public void APartialNamedByAnExpression_IsCVW114()
    {
        AssertViewSameAsTheBuild("CVW114", "<partial name=\"@Model\" />" + Quit);
    }

    [Fact]
    public void APartialThatIsALayout_IsCVW114()
    {
        AssertViewSameAsTheBuild("CVW114", "@layout none\n<partial name=\"_Layout\" />" + Quit, Shared("_Layout", "<body />"));
    }

    [Fact]
    public void APartialWithErrors_IsCVW114()
    {
        AssertViewSameAsTheBuild("CVW114", "<partial name=\"Bad\" />" + Quit, Shared("Bad", "<box><blink /></box>"));
    }

    [Fact]
    public void APartialWithAModel_UsedWithoutOne_IsCVW115()
    {
        AssertViewSameAsTheBuild("CVW115", "<partial name=\"Card\" />" + Quit, Shared("Card", "@model string\n<text>@Model</text>"));
    }

    [Fact]
    public void APartialThatRendersItself_IsCVW116()
    {
        AssertSameAsTheBuild("CVW116", $"{RealApp.Project}/Views/Shared/Loop.cvw", Shared("Loop", "<text>again</text>\n<partial name=\"Loop\" />"));
    }

    [Fact]
    public void TheUnsavedText_IsWhatIsChecked_NotTheFileOnDisk()
    {
        // The editor's buffer makes the partial render itself; the saved file does not
        var path = $"{RealApp.Project}/Views/Shared/Card.cvw";
        WriteAll([Shared("Card", "<text>saved</text>")]);
        var built = RealApp.Diagnostics([Shared("Card", "<partial name=\"Card\" />")], path);
        Assert.Equal(built, Preview(path, "<partial name=\"Card\" />").Diagnostics);
        Assert.Contains(built, d => d.Id == "CVW116");
    }

    [Fact]
    public void APartialNamedInAnotherCase_IsFound_AsInTheBuild()
    {
        var result = AssertViewSameAsTheBuild(null, "<partial name=\"header\" />" + Quit, Shared("Header", "<text>Shop</text>"));
        Assert.Equal("Shop", result.Output.TrimEnd());
    }

    [Fact]
    public void AnAreaView_UsesTheProjectsSharedViews_NotTheAreas()
    {
        // In the build, Areas/Admin/Views/Shared holds views of an Admin/Shared controller, not partials
        var view = $"{RealApp.Project}/Areas/Admin/Views/Settings/IndexView.cvw";
        var result = AssertSameAsTheBuild("CVW114", view,
            (view, "<partial name=\"Header\" />" + Quit),
            ($"{RealApp.Project}/Areas/Admin/Views/Shared/Header.cvw", "<text>admin header</text>"));
        Assert.DoesNotContain("admin header", result.Output);
    }

    [Fact]
    public void AnAreaView_UsesTheProjectsPartial()
    {
        var view = $"{RealApp.Project}/Areas/Admin/Views/Settings/IndexView.cvw";
        var result = AssertSameAsTheBuild(null, view, (view, "<partial name=\"Header\" />" + Quit), Shared("Header", "<text>project header</text>"));
        Assert.Equal("project header", result.Output.TrimEnd());
    }

    [Fact]
    public void AMenuInAPartial_IsCVW103()
    {
        AssertSameAsTheBuild("CVW103", $"{RealApp.Project}/Views/Shared/Nav.cvw",
            Shared("Nav", "<menu><option key=\"q\" quit>Quit</option></menu>"));
    }

    [Fact]
    public void RefreshInAPartial_IsCVW010()
    {
        AssertSameAsTheBuild("CVW010", $"{RealApp.Project}/Views/Shared/Clock.cvw", Shared("Clock", "@refresh 5s\n<text>now</text>"));
    }

    [Fact]
    public void RefreshInALayout_IsCVW010()
    {
        AssertSameAsTheBuild("CVW010", $"{RealApp.Project}/Views/Shared/_Layout.cvw", Shared("_Layout", "@refresh 5s\n<body />"));
    }

    // ------------------------------------------------------------------ layouts

    [Fact]
    public void BodyInAView_IsCVW117()
    {
        AssertViewSameAsTheBuild("CVW117", "<text>x</text>\n<body />" + Quit);
    }

    [Fact]
    public void TwoBodies_AreCVW117()
    {
        AssertSameAsTheBuild("CVW117", $"{RealApp.Project}/Views/Shared/_Layout.cvw", Shared("_Layout", "<body />\n<text>x</text>\n<body />"));
    }

    [Fact]
    public void ALayoutWithAModel_IsCVW117()
    {
        AssertSameAsTheBuild("CVW117", $"{RealApp.Project}/Views/Shared/_Layout.cvw", Shared("_Layout", "@model string\n<body />"));
    }

    [Fact]
    public void ALayoutWithALayout_IsCVW117()
    {
        AssertSameAsTheBuild("CVW117", $"{RealApp.Project}/Views/Shared/_Layout.cvw",
            Shared("_Layout", "@layout Plain\n<body />"), Shared("Plain", "<body />"));
    }

    [Fact]
    public void BodyInsideAnIf_IsCVW117()
    {
        AssertSameAsTheBuild("CVW117", $"{RealApp.Project}/Views/Shared/_Layout.cvw",
            Shared("_Layout", "<text>top</text>\nif (ViewData.Count > 0)\n{\n    <body />\n}"));
    }

    [Fact]
    public void AnUnknownLayout_IsCVW118()
    {
        AssertViewSameAsTheBuild("CVW118", "@layout Gone\n<text>x</text>" + Quit, Shared("Plain", "<body />"));
    }

    [Fact]
    public void ALayoutNamingAPartial_IsCVW118()
    {
        AssertViewSameAsTheBuild("CVW118", "@layout Header\n<text>x</text>" + Quit, Shared("Header", "<text>h</text>"));
    }

    [Fact]
    public void APartialWithALayout_IsCVW118()
    {
        AssertSameAsTheBuild("CVW118", $"{RealApp.Project}/Views/Shared/Card.cvw",
            Shared("Card", "@layout Plain\n<text>card</text>"), Shared("Plain", "<body />"));
    }

    [Fact]
    public void AValidViewWithALayoutAndPartials_HasNoDiagnostics()
    {
        AssertViewSameAsTheBuild(null, "@layout Plain\n<partial name=\"Card\" model=\"@(\"x\")\" />\n<partial name=\"Header\" />" + Quit,
            Shared("Plain", "<text>top</text>\n<body />"), Shared("Card", "@model string\n<text>@Model</text>"),
            Shared("Header", "<text>h</text>"));
    }

    // ------------------------------------------------------------------ the file's place and name

    [Fact]
    public void AViewNameWithoutTheViewSuffix_IsCVW003()
    {
        var path = $"{RealApp.Project}/Views/Home/Index.cvw";
        AssertSameAsTheBuild("CVW003", path, (path, "<text>x</text>" + Quit));
    }

    [Fact]
    public void AViewOutsideAControllerFolder_IsCVW001_AsTheBuildSkipsIt()
    {
        // The build reports only that the file was skipped, not the markup errors in it
        var path = $"{RealApp.Project}/Other/IndexView.cvw";
        AssertSameAsTheBuild("CVW001", path, (path, "<box><blink /></box>" + Quit));
    }

    [Fact]
    public void AControllerFolderThatIsNotAName_IsCVW002()
    {
        var path = $"{RealApp.Project}/Views/my-home/IndexView.cvw";
        AssertSameAsTheBuild("CVW002", path, (path, "<text>x</text>" + Quit));
    }

    [Fact]
    public void APartialNamedRender_IsCVW009()
    {
        AssertSameAsTheBuild("CVW009", $"{RealApp.Project}/Views/Shared/Render.cvw", Shared("Render", "<text>x</text>"));
    }

    [Fact]
    public void MarkupErrors_StillComeFirst_AndHideTheProjectChecks_AsInTheBuild()
    {
        AssertViewSameAsTheBuild("CVW105", "<box><blink /></box>\n<partial name=\"Gone\" />" + Quit);
    }

    // ------------------------------------------------------------------ what the preview cannot check

    [Fact]
    public void ASharedViewOverTheSizeLimit_SkipsTheProjectChecks_WithANote()
    {
        // The build reads the big partial and is satisfied; the preview cannot read it, so it must not guess
        var big = "<text>Big</text>" + new string(' ', (int)ProjectViewFiles.MaxFileSize);
        var result = AssertViewSameAsTheBuild(null, "<partial name=\"Big\" />" + Quit, Shared("Big", big));
        Assert.Contains(result.Notes, n => n.Contains("CVW114") && n.Contains("'Big'"));
    }

    [Fact]
    public void NavTargets_AreNotChecked_WithoutABuild()
    {
        // CVW119 needs the project's controllers, which only the build knows
        var view = "<menu><option key=\"1\" nav=\"Ordrs/Index\">Orders</option></menu>";
        var built = RealApp.Diagnostics([(RealApp.ViewPath, view)], RealApp.ViewPath,
            "namespace TestApp.Controllers { public sealed class OrdersController : ConsoleMVC.Mvc.Controller { public ConsoleMVC.Mvc.ActionResult Index() => View(); } }");
        Assert.Contains(built, d => d.Id == "CVW119");

        WriteAll([(RealApp.ViewPath, view)]);
        Assert.Empty(Preview(RealApp.ViewPath, view).Diagnostics);
    }
}
