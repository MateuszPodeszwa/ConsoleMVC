using ConsoleMVC.Testing;

namespace ConsoleMVC.Tests.Testing;

/// <summary>Tests for <see cref="Snapshot"/> (docs/design/testing-package.md).</summary>
public sealed class SnapshotTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("consolemvc-snapshots-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string FileOf(string name, string suffix = ".txt") => Path.Combine(_folder, name + suffix);

    private void Match(string actual, string name, bool ci = false, bool update = false) =>
        Snapshot.Match(actual, name, new SnapshotSettings(_folder, ci, update));

    [Fact]
    public void FirstRun_CreatesTheSnapshot()
    {
        Match("Hello\n", "greeting");

        Assert.Equal("Hello\n", File.ReadAllText(FileOf("greeting")));
    }

    [Fact]
    public void FirstRun_OnABuildServer_Fails_AndLeavesTheReceivedFile()
    {
        var error = Assert.Throws<SnapshotMismatchException>(() => Match("Hello\n", "greeting", ci: true));

        Assert.Contains("does not exist", error.Message);
        Assert.False(File.Exists(FileOf("greeting")));
        Assert.Equal("Hello\n", File.ReadAllText(FileOf("greeting", ".received.txt")));
    }

    [Fact]
    public void SameOutput_Passes_AndRemovesAStaleReceivedFile()
    {
        File.WriteAllText(FileOf("greeting"), "a\r\nb\r\n"); // saved on Windows
        File.WriteAllText(FileOf("greeting", ".received.txt"), "old");

        Match("a\nb\n", "greeting");

        Assert.False(File.Exists(FileOf("greeting", ".received.txt")));
    }

    [Theory]
    [InlineData("Select an option: ", "Select an option:\n")]   // an editor trimmed the trailing space
    [InlineData("Menu\nSelect an option: ", "Menu\nSelect an option: \n\n")]   // an editor added final new lines
    [InlineData("Menu  \n\n", "Menu")]
    public void SpacesAtLineEnds_AndBlankLinesAtTheEnd_AreIgnored(string actual, string saved)
    {
        File.WriteAllText(FileOf("menu"), saved);

        Match(actual, "menu");
    }

    [Fact]
    public void ADifferenceInLeadingSpaces_IsVisibleInTheMessage()
    {
        File.WriteAllText(FileOf("menu"), "  [1] About\n");

        var error = Assert.Throws<SnapshotMismatchException>(() => Match(" [1] About\n", "menu"));

        Assert.Contains("expected: \"  [1] About\"", error.Message);
        Assert.Contains("actual:   \" [1] About\"", error.Message);
    }

    [Fact]
    public void DifferentOutput_FailsWithTheFirstDifferentLine()
    {
        File.WriteAllText(FileOf("greeting"), "Title\nHello, Ada\nEnd\n");

        var error = Assert.Throws<SnapshotMismatchException>(() => Match("Title\nHello, Grace\nEnd\n", "greeting"));

        Assert.Contains("line 2", error.Message);
        Assert.Contains("Hello, Ada", error.Message);
        Assert.Contains("Hello, Grace", error.Message);
        Assert.Equal("Title\nHello, Ada\nEnd\n", error.Expected);
        Assert.Equal(FileOf("greeting"), error.SnapshotPath);
        Assert.Equal("Title\nHello, Grace\nEnd\n", File.ReadAllText(error.ReceivedPath));
    }

    [Fact]
    public void UpdateMode_OverwritesTheSnapshot()
    {
        File.WriteAllText(FileOf("greeting"), "old\n");

        Match("new\n", "greeting", update: true);

        Assert.Equal("new\n", File.ReadAllText(FileOf("greeting")));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("sub/name")]
    [InlineData("")]
    [InlineData("na me")]
    public void Names_CannotBePaths(string name)
    {
        Assert.Throws<ArgumentException>(() => Match("x", name));
    }

    [Fact]
    public void Match_WithTheCallerFile_UsesASnapshotsFolderBesideIt()
    {
        var folder = Path.Combine(_folder, "__snapshots__");
        var settings = SnapshotSettings.ForCaller(Path.Combine(_folder, "MyTests.cs"));

        Assert.Equal(folder, settings.Directory);
    }
}
