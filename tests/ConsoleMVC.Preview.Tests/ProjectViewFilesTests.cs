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
    public void AnAreaView_PrefersTheAreasSharedFolder_ThenTheProjects()
    {
        Write("Views/Shared/Header.cvw", "project header");
        Write("Views/Shared/Footer.cvw", "project footer");
        Write("Areas/Admin/Views/Shared/Header.cvw", "admin header");
        var files = ProjectViewFiles.ForView(Write("Areas/Admin/Views/Settings/IndexView.cvw", ""));
        Assert.Equal("admin header", files.ReadShared("Header")?.Text);
        Assert.Equal("project footer", files.ReadShared("Footer")?.Text);
    }

    [Fact]
    public void AViewOutsideAViewsFolder_HasNoSharedViews()
    {
        Write("Shared/Header.cvw", "header");
        var files = ProjectViewFiles.ForView(Write("Other/IndexView.cvw", ""));
        Assert.Same(ProjectViewFiles.None, files);
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
        var note = Assert.Single(result.Notes);
        Assert.Equal("The partial view 'Big' is larger than 1 MB, so the preview does not read it.", note);
        Assert.Contains("[partial view Big]", result.Output);
    }

    [Fact]
    public void ANamedLayoutOverTheSizeLimit_IsSaidToBeTooLarge_NotMissing()
    {
        WriteHugeShared("Big", "<text>Top</text><body />");
        var result = Preview("@layout Big\n<text>Page</text>");
        var note = Assert.Single(result.Notes);
        Assert.Equal("The layout 'Big' is larger than 1 MB, so the preview does not read it; the view is shown without it.", note);
        Assert.Equal("Page", result.Output.TrimEnd());
    }

    [Fact]
    public void TheDefaultLayoutOverTheSizeLimit_IsSaidToBeTooLarge()
    {
        // Without the note the view would silently be shown without the layout every screen of the application has
        WriteHugeShared("_Layout", "<text>Top</text><body />");
        var result = Preview("<text>Page</text>");
        var note = Assert.Single(result.Notes);
        Assert.Equal("The layout '_Layout' is larger than 1 MB, so the preview does not read it; the view is shown without it.", note);
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
