using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ConsoleMVC.Testing;

/// <summary>
/// Where snapshots are kept and how differences are handled. <see cref="ForCaller"/> gives the defaults used by
/// <see cref="Snapshot.Match(string, string, string)"/>.
/// </summary>
/// <param name="Directory">The folder snapshot files are kept in.</param>
/// <param name="IsContinuousIntegration">Whether a missing snapshot fails (on a build server) instead of being created.</param>
/// <param name="Update">Whether a different output replaces the snapshot instead of failing.</param>
/// <example><code>Snapshot.Match(screen, "home", new SnapshotSettings("snapshots", IsContinuousIntegration: false, Update: false));</code></example>
public sealed record SnapshotSettings(string Directory, bool IsContinuousIntegration, bool Update)
{
    /// <summary>The environment variable that, set to <c>1</c> or <c>true</c>, accepts every new output as the snapshot.</summary>
    public const string UpdateVariable = "CONSOLEMVC_UPDATE_SNAPSHOTS";

    /// <summary>The folder, beside the test file, that snapshots are kept in.</summary>
    public const string FolderName = "__snapshots__";

    /// <summary>
    /// Returns the default settings for a test file: the <see cref="FolderName"/> folder beside it, continuous
    /// integration when the <c>CI</c> environment variable is set (as GitHub Actions, Azure Pipelines and most other
    /// services do), and update mode when <see cref="UpdateVariable"/> is <c>1</c> or <c>true</c>.
    /// </summary>
    /// <param name="sourceFile">The test's source file path.</param>
    /// <returns>The settings.</returns>
    /// <example><code>var settings = SnapshotSettings.ForCaller("/src/Tests/HomeTests.cs");</code></example>
    public static SnapshotSettings ForCaller(string sourceFile)
    {
        var folder = Path.GetDirectoryName(sourceFile);
        if (string.IsNullOrEmpty(folder))
            folder = System.IO.Directory.GetCurrentDirectory();

        var update = Environment.GetEnvironmentVariable(UpdateVariable);
        return new SnapshotSettings(
            Path.Combine(folder, FolderName),
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")),
            update is "1" || string.Equals(update, "true", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Compares text, such as a screen from <see cref="TestRun"/>, with a file kept under version control. The first run
/// creates the file; later runs fail when the text changes, leaving the new text in a <c>.received.txt</c> file beside
/// it for a diff tool. Works with any test framework: a difference throws <see cref="SnapshotMismatchException"/>.
/// </summary>
/// <example>
/// <code>
/// Snapshot.Match(run.Screens[0], "home-menu");   // Tests/__snapshots__/home-menu.txt
///
/// // Accept intended changes:  CONSOLEMVC_UPDATE_SNAPSHOTS=1 dotnet test
/// </code>
/// </example>
public static partial class Snapshot
{
    /// <summary>
    /// Compares <paramref name="actual"/> with the snapshot <paramref name="name"/> in the <c>__snapshots__</c> folder
    /// beside the calling test file.
    /// </summary>
    /// <param name="actual">The text to check.</param>
    /// <param name="name">The snapshot name: letters, digits, <c>.</c>, <c>-</c> and <c>_</c>.</param>
    /// <param name="sourceFile">Filled in by the compiler with the calling file; do not pass it.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is not a valid snapshot name.</exception>
    /// <exception cref="SnapshotMismatchException">Thrown when the text differs, or when a snapshot is missing on a build server.</exception>
    /// <example><code>Snapshot.Match(run.LastScreen, "receipt");</code></example>
    public static void Match(string actual, string name, [CallerFilePath] string sourceFile = "") =>
        Match(actual, name, SnapshotSettings.ForCaller(sourceFile));

    /// <summary>
    /// Compares <paramref name="actual"/> with the snapshot <paramref name="name"/> using explicit settings.
    /// </summary>
    /// <param name="actual">The text to check.</param>
    /// <param name="name">The snapshot name: letters, digits, <c>.</c>, <c>-</c> and <c>_</c>.</param>
    /// <param name="settings">The folder and behaviour.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is not a valid snapshot name.</exception>
    /// <exception cref="SnapshotMismatchException">Thrown when the text differs, or when a snapshot is missing on a build server.</exception>
    /// <example><code>Snapshot.Match(screen, "home", SnapshotSettings.ForCaller(path) with { Update = true });</code></example>
    public static void Match(string actual, string name, SnapshotSettings settings)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(settings);

        // A name is never a path, so a snapshot cannot be read or written outside its folder
        if (name is null || !ValidName().IsMatch(name) || name.Contains(".."))
            throw new ArgumentException("A snapshot name may contain only letters, digits, '.', '-' and '_', e.g. \"home-menu\".", nameof(name));

        var snapshotPath = Path.Combine(settings.Directory, name + ".txt");
        var receivedPath = Path.Combine(settings.Directory, name + ".received.txt");
        var text = Normalise(actual);

        if (!File.Exists(snapshotPath))
        {
            if (settings.IsContinuousIntegration)
            {
                Write(receivedPath, text);
                throw new SnapshotMismatchException(
                    $"The snapshot '{snapshotPath}' does not exist. Run the tests locally to create it and commit it; the " +
                    $"output was saved to '{receivedPath}'.", null, text, snapshotPath, receivedPath);
            }

            Write(snapshotPath, text);
            return;
        }

        var expected = Normalise(File.ReadAllText(snapshotPath));
        if (expected == text)
        {
            File.Delete(receivedPath);
            return;
        }

        if (settings.Update)
        {
            Write(snapshotPath, text);
            File.Delete(receivedPath);
            return;
        }

        Write(receivedPath, text);
        throw new SnapshotMismatchException(Describe(expected, text, snapshotPath, receivedPath), expected, text, snapshotPath, receivedPath);
    }

    /// <summary>Line endings as <c>\n</c>, so snapshots saved on Windows match on other systems.</summary>
    private static string Normalise(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Names the first line that differs, with both versions of it.</summary>
    private static string Describe(string expected, string actual, string snapshotPath, string receivedPath)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var line = 0;
        while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
            line++;

        var was = line < expectedLines.Length ? expectedLines[line] : "(end of snapshot)";
        var now = line < actualLines.Length ? actualLines[line] : "(end of output)";

        return $"The output differs from the snapshot '{snapshotPath}' at line {line + 1}:\n" +
               $"  expected: {was}\n" +
               $"  actual:   {now}\n" +
               $"The new output was saved to '{receivedPath}'. If the change is intended, replace the snapshot with it, " +
               $"or run the tests with {SnapshotSettings.UpdateVariable}=1.";
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex ValidName();
}

/// <summary>
/// Thrown by <see cref="Snapshot"/> when the output differs from its snapshot, or when a snapshot is missing on a build
/// server.
/// </summary>
/// <example><code>catch (SnapshotMismatchException e) { Console.WriteLine(e.ReceivedPath); }</code></example>
public sealed class SnapshotMismatchException : Exception
{
    /// <summary>
    /// Initialises the exception.
    /// </summary>
    /// <param name="message">What differs and how to accept the change.</param>
    /// <param name="expected">The snapshot's text, or <see langword="null"/> when it does not exist.</param>
    /// <param name="actual">The new text.</param>
    /// <param name="snapshotPath">The snapshot file.</param>
    /// <param name="receivedPath">The file the new text was saved to.</param>
    /// <example><code>throw new SnapshotMismatchException("differs", "a", "b", "a.txt", "a.received.txt");</code></example>
    public SnapshotMismatchException(string message, string? expected, string actual, string snapshotPath, string receivedPath)
        : base(message)
    {
        Expected = expected;
        Actual = actual;
        SnapshotPath = snapshotPath;
        ReceivedPath = receivedPath;
    }

    /// <summary>Gets the snapshot's text, or <see langword="null"/> when it does not exist.</summary>
    public string? Expected { get; }

    /// <summary>Gets the new text.</summary>
    public string Actual { get; }

    /// <summary>Gets the snapshot file.</summary>
    public string SnapshotPath { get; }

    /// <summary>Gets the file the new text was saved to.</summary>
    public string ReceivedPath { get; }
}
