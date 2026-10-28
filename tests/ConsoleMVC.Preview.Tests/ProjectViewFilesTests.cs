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
        Assert.Equal("header", files.ReadShared("Header"));
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
        Assert.Equal("admin header", files.ReadShared("Header"));
        Assert.Equal("project footer", files.ReadShared("Footer"));
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
    public void AFileOverTheSizeLimit_IsTreatedAsMissing()
    {
        Write("Views/Shared/Huge.cvw", new string('x', (int)ProjectViewFiles.MaxFileSize + 1));
        var files = ProjectViewFiles.ForView(Write("Views/Home/IndexView.cvw", ""));
        Assert.Null(files.ReadShared("Huge"));
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
