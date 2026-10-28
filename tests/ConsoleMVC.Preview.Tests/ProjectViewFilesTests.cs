namespace ConsoleMVC.Preview.Tests;

/// <summary>
/// Tests of <see cref="ProjectViewFiles"/>: shared views are found next to the view, and nothing outside the project's
/// shared view folders can be read.
/// </summary>
public sealed class ProjectViewFilesTests : IDisposable
{
    private readonly string _project = Directory.CreateTempSubdirectory("cvw-preview-").FullName;

    public void Dispose() => Directory.Delete(_project, recursive: true);

    /// <summary>Writes a file under the project, creating its folders.</summary>
    private string Write(string relativePath, string text)
    {
        var path = Path.Combine(_project, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void AControllerView_ReadsTheSharedFolder()
    {
        Write("Views/Shared/Header.cvw", "header");
        var files = ProjectViewFiles.ForView(Write("Views/Home/IndexView.cvw", ""));
        Assert.Equal("header", files.ReadShared("Header")?.Text);
        Assert.False(files.IsShared);
    }

    [Fact]
    public void ASharedView_KnowsItIsShared()
    {
        Assert.True(ProjectViewFiles.ForView(Write("Views/Shared/_Layout.cvw", "")).IsShared);
    }

    [Fact]
    public void AnAreaView_ReadsTheProjectsSharedFolder_NotTheAreas()
    {
        // As in the build (PreviewDiagnosticsTests compares it): an area's Views/Shared holds views, not partials
        Write("Views/Shared/Header.cvw", "project header");
        Write("Areas/Admin/Views/Shared/Header.cvw", "admin header");
        Write("Areas/Admin/Views/Shared/Footer.cvw", "admin footer");
        var files = ProjectViewFiles.ForView(Write("Areas/Admin/Views/Settings/IndexView.cvw", ""));
        Assert.Equal("project header", files.ReadShared("Header")?.Text);
        Assert.Null(files.ReadShared("Footer"));
        Assert.Equal(["Header"], files.SharedNames);
    }

    [Fact]
    public void AViewInAnAreasSharedFolder_IsNotASharedView()
    {
        Assert.False(ProjectViewFiles.ForView(Write("Areas/Admin/Views/Shared/MenuView.cvw", "")).IsShared);
    }

    [Fact]
    public void AName_IsMatchedInAnyCase_AndTheFirstFileByPathWins()
    {
        Write("Views/Shared/Header.cvw", "upper");
        Write("Views/Shared/header.cvw", "lower"); // a second file only where names are case-sensitive
        var files = ProjectViewFiles.ForView(Write("Views/Home/IndexView.cvw", ""));
        Assert.Equal("upper", files.ReadShared("HEADER")?.Text);
        Assert.Equal("Header", files.ReadShared("header")?.Name);
    }

    [Fact]
    public void OnlyCvwFilesDirectlyInTheSharedFolder_AreSharedViews()
    {
        Write("Views/Shared/Header.cvw", "");
        Write("Views/Shared/Notes.txt", "");
        Write("Views/Shared/Sub/Nested.cvw", "");
        Write("Views/Shared/not-a-name.cvw", "");
        var files = ProjectViewFiles.ForView(Write("Views/Home/IndexView.cvw", ""));
        Assert.Equal(["Header"], files.SharedNames);
    }

    [Fact]
    public void AViewOutsideAViewsFolder_HasNoSharedViews()
    {
        Write("Shared/Header.cvw", "header");
        var path = Write("Other/IndexView.cvw", "");
        var files = ProjectViewFiles.ForView(path);
        Assert.Equal(path, files.ViewPath);
        Assert.Empty(files.SharedNames);
        Assert.Null(files.ReadShared("Header"));
    }

    [Theory]
    [InlineData("../Home/IndexView")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win")]
    [InlineData("Sub/Header")]
    [InlineData("Header.cvw")]
    [InlineData("")]
    public void ANameThatIsAPath_IsRefused(string name)
    {
        Write("Views/Home/IndexView.cvw", "secret");
        Write("Views/Shared/Sub/Header.cvw", "nested");
        var files = ProjectViewFiles.ForView(Path.Combine(_project, "Views/Shared/Any.cvw"));
        Assert.False(ProjectViewFiles.IsPlainName(name));
        Assert.Null(files.ReadShared(name));
    }

    [Fact]
    public void AVeryLongName_IsRefused()
    {
        Assert.False(ProjectViewFiles.IsPlainName(new string('a', 129)));
        Assert.True(ProjectViewFiles.IsPlainName(new string('a', 128)));
    }

    [Fact]
    public void AFileOverTheSizeLimit_IsNotRead()
    {
        Write("Views/Shared/Huge.cvw", new string('x', (int)ProjectViewFiles.MaxFileSize + 1));
        var files = ProjectViewFiles.ForView(Write("Views/Home/IndexView.cvw", ""));
        var huge = files.ReadShared("Huge");
        Assert.NotNull(huge);
        Assert.True(huge.IsTooLarge);
        Assert.Null(huge.Text);
    }

    /// <summary>A shared view one byte over the limit: real markup followed by spaces.</summary>
    private string WriteHugeShared(string name, string markup) =>
        Write($"Views/Shared/{name}.cvw", markup + new string(' ', (int)ProjectViewFiles.MaxFileSize + 1 - markup.Length));

    private PreviewResult Preview(string view)
    {
        var path = Write("Views/Home/IndexView.cvw", view);
        return CvwPreview.Render(view, ProjectViewFiles.ForView(path), new PreviewOptions());
    }

    [Fact]
    public void APartialOverTheSizeLimit_IsSaidToBeTooLarge_NotMissing()
    {
        WriteHugeShared("Big", "<text>Big</text>");
        var result = Preview("<partial name=\"Big\" />");
        Assert.DoesNotContain(result.Notes, n => n.Contains("not found"));
        Assert.Contains("The partial view 'Big' is larger than 1 MB, so the preview does not read it.", result.Notes);
        Assert.Contains("[partial view Big]", result.Output);
    }

    [Fact]
    public void ANamedLayoutOverTheSizeLimit_IsSaidToBeTooLarge_NotMissing()
    {
        WriteHugeShared("Big", "<text>Top</text><body />");
        var result = Preview("@layout Big\n<text>Page</text>");
        Assert.DoesNotContain(result.Notes, n => n.Contains("not found"));
        Assert.Contains("The layout 'Big' is larger than 1 MB, so the preview does not read it; the view is shown without it.", result.Notes);
        Assert.Equal("Page", result.Output.TrimEnd());
    }

    [Fact]
    public void TheDefaultLayoutOverTheSizeLimit_IsSaidToBeTooLarge()
    {
        // Without the note the view would silently be shown without the layout every screen of the application has
        WriteHugeShared("_Layout", "<text>Top</text><body />");
        var result = Preview("<text>Page</text>");
        Assert.DoesNotContain(result.Notes, n => n.Contains("not found"));
        Assert.Contains("The layout '_Layout' is larger than 1 MB, so the preview does not read it; the view is shown without it.", result.Notes);
    }

    [Fact]
    public void APreview_UsesTheProjectsLayoutAndPartials()
    {
        Write("Views/Shared/_Layout.cvw", "<text>Top</text><body />");
        Write("Views/Shared/Header.cvw", "<text>Header</text>");
        var path = Write("Views/Home/IndexView.cvw", "<partial name=\"Header\" />");
        var output = CvwPreview.Render(File.ReadAllText(path), ProjectViewFiles.ForView(path), new PreviewOptions()).Output;
        Assert.Equal(["Top", "Header"], output.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Select(l => l.TrimEnd()));
    }
}
