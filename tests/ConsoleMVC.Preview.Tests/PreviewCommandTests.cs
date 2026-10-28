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
        // A whole view: it ends by returning (one that never does is CVW012, as in the build)
        Assert.Equal(PreviewCommand.Success, Run("", WriteView("<text>Hello</text>\nreturn NavigationResult.Quit();")));
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

    [Theory]
    [InlineData("20", 20)]
    [InlineData("500", 500)]
    public void TheWidthLimits_AreAccepted(string width, int drawn)
    {
        Assert.Equal(PreviewCommand.Success, Run("<separator />", "--stdin", "--width", width));
        Assert.Equal(drawn, _output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Length);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("19")]
    [InlineData("501")]
    [InlineData("5000")]
    [InlineData("99999999999")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("4.5")]
    [InlineData("")]
    public void AWidthOutsideTheRange_IsAUsageError_ThatSaysWhatIsAllowed(string width)
    {
        Assert.Equal(PreviewCommand.UsageError, Run("<separator />", "--stdin", "--width", width));
        Assert.StartsWith($"cvw-preview: --width needs a whole number from 20 to 500, not '{width}'.", _error.ToString());
        Assert.Equal("", _output.ToString());
    }

    [Fact]
    public void AMissingWidth_IsAUsageError_ThatSaysWhatIsAllowed()
    {
        Assert.Equal(PreviewCommand.UsageError, Run("", "--stdin", "--width"));
        Assert.StartsWith("cvw-preview: --width needs a whole number from 20 to 500.", _error.ToString());
    }

    [Theory]
    [InlineData("--width=30", 30)]
    [InlineData("--WIDTH=30", null)]
    public void TheWidth_MayBeJoinedWithAnEqualsSign(string argument, int? drawn)
    {
        var exit = Run("<separator />", "--stdin", argument);
        if (drawn is null)
        {
            // Options are lower case, like every other option of the tool
            Assert.Equal(PreviewCommand.UsageError, exit);
            return;
        }

        Assert.Equal(PreviewCommand.Success, exit);
        Assert.Equal(drawn, _output.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Length);
    }

    [Fact]
    public void AJoinedWidthOutsideTheRange_IsAUsageError()
    {
        Assert.Equal(PreviewCommand.UsageError, Run("", "--stdin", "--width=19"));
        Assert.StartsWith("cvw-preview: --width needs a whole number from 20 to 500, not '19'.", _error.ToString());
    }

    [Fact]
    public void TheFormat_MayBeJoinedWithAnEqualsSign()
    {
        Assert.Equal(PreviewCommand.Success, Run("<text>x</text>", "--stdin", "--format=html"));
        Assert.StartsWith("<pre class=\"cvw-screen\">", _output.ToString());
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("1")]
    [InlineData("")]
    public void AnUnknownFormat_IsAUsageError_ThatSaysWhatIsAllowed(string format)
    {
        Assert.Equal(PreviewCommand.UsageError, Run("", "--stdin", "--format", format));
        Assert.StartsWith($"cvw-preview: --format needs text, ansi or html, not '{format}'.", _error.ToString());
    }

    [Theory]
    [InlineData("--stdin=yes")]
    [InlineData("--high-contrast=1")]
    public void AFlag_TakesNoValue(string argument)
    {
        Assert.Equal(PreviewCommand.UsageError, Run("", "--stdin", argument));
        Assert.StartsWith($"cvw-preview: unexpected argument '{argument}'.", _error.ToString());
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
