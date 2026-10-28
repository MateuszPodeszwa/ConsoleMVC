namespace ConsoleMVC.Preview.Tests;

/// <summary>Tests of the <c>cvw-preview</c> command line (<see cref="PreviewCommand"/>).</summary>
public sealed class PreviewCommandTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("cvw-command-").FullName;
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private int Run(string input, params string[] args) => PreviewCommand.Run(args, new StringReader(input), _output, _error);

    private string WriteView(string text)
    {
        var path = Path.Combine(_folder, "Views", "Home", "IndexView.cvw");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void AFile_IsDrawn()
    {
        Assert.Equal(PreviewCommand.Success, Run("", WriteView("<text>Hello</text>")));
        Assert.Equal("Hello", _output.ToString().TrimEnd());
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public void Stdin_ReplacesTheFilesText()
    {
        var path = WriteView("<text>Saved</text>");
        Assert.Equal(PreviewCommand.Success, Run("<text>Unsaved</text>", path, "--stdin"));
        Assert.Equal("Unsaved", _output.ToString().TrimEnd());
    }

    [Fact]
    public void Stdin_WorksWithoutAFile()
    {
        Assert.Equal(PreviewCommand.Success, Run("<text>Alone</text>", "--stdin"));
        Assert.Equal("Alone", _output.ToString().TrimEnd());
    }

    [Fact]
    public void TheWidth_IsUsed()
    {
        Run("<separator />", "--stdin", "--width", "30");
        Assert.Equal(30, _output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Length);
    }

    [Fact]
    public void Html_ListsTheProblemsAfterTheScreen()
    {
        Run("<text>ok</text><box><blink /></box>", "--stdin", "--format", "html");
        var html = _output.ToString();
        Assert.Contains("<pre class=\"cvw-screen\">", html);
        Assert.Contains("<ul class=\"cvw-problems\">", html);
        Assert.Contains("class=\"cvw-error\" data-line=\"1\" data-column=\"21\"", html);
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public void Text_WritesTheProblemsToStandardError()
    {
        Run("<box><blink /></box>", "--stdin");
        Assert.StartsWith("1:6: error CVW105", _error.ToString());
    }

    [Fact]
    public void Help_IsShown()
    {
        Assert.Equal(PreviewCommand.Success, Run("", "--help"));
        Assert.StartsWith("Usage: cvw-preview", _output.ToString());
    }

    [Theory]
    [InlineData]
    [InlineData("--format", "pdf")]
    [InlineData("--format", "1")]
    [InlineData("--width", "-5")]
    [InlineData("--width")]
    [InlineData("--unknown")]
    [InlineData("a.cvw", "b.cvw")]
    public void WrongArguments_AreAUsageError(params string[] args)
    {
        Assert.Equal(PreviewCommand.UsageError, Run("", args));
        Assert.Contains("Usage: cvw-preview", _error.ToString());
    }

    [Fact]
    public void AMissingFile_IsAFileError()
    {
        Assert.Equal(PreviewCommand.FileError, Run("", Path.Combine(_folder, "Missing.cvw")));
        Assert.Contains("cannot read", _error.ToString());
    }
}
