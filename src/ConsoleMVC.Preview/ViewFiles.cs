using ConsoleMVC.Generators.Emit;

namespace ConsoleMVC.Preview;

/// <summary>A shared view (a partial view or a layout) found by <see cref="IViewFiles.ReadShared"/>.</summary>
/// <param name="Name">Its name as the file is called, such as <c>Header</c>.</param>
/// <param name="Text">Its text, or <see langword="null"/> when the file is larger than <see cref="ProjectViewFiles.MaxFileSize"/>.</param>
/// <example><code>if (files.ReadShared("Header") is { IsTooLarge: false } header) Parse(header.Text!);</code></example>
internal sealed record SharedView(string Name, string? Text)
{
    /// <summary>Gets whether the file exists but is too large to be read, so its content is unknown.</summary>
    /// <example><code>if (view.IsTooLarge) notes.Add($"'{view.Name}' is too large");</code></example>
    public bool IsTooLarge => Text is null;
}

/// <summary>
/// Finds the partial views and layouts a view uses, by name, as the build finds them. Implemented over the project's
/// files by <see cref="ProjectViewFiles"/>, and in memory by tests.
/// </summary>
/// <example>
/// <code>
/// IViewFiles files = ProjectViewFiles.ForView("Views/Home/IndexView.cvw");
/// string? header = files.ReadShared("Header")?.Text; // the text of Views/Shared/Header.cvw
/// </code>
/// </example>
internal interface IViewFiles
{
    /// <summary>
    /// Gets the full path of the view being previewed, which decides its controller, its class name and whether it is
    /// a shared view; <see langword="null"/> when it is not known (text from standard input without a file name), and
    /// the checks that need the project are then left out.
    /// </summary>
    /// <example><code>var path = files.ViewPath; // "/src/Shop/Views/Home/IndexView.cvw"</code></example>
    string? ViewPath { get; }

    /// <summary>Gets whether the view being previewed is itself a shared view (a partial or a layout).</summary>
    /// <example><code>if (files.IsShared) Console.WriteLine("drawn without a layout");</code></example>
    bool IsShared { get; }

    /// <summary>
    /// Gets the names of the project's shared views, in the order the build considers them (by path), so that of two
    /// names differing only in case the first is the one used.
    /// </summary>
    /// <example><code>foreach (var name in files.SharedNames) Console.WriteLine(name); // _Layout, Header</code></example>
    IReadOnlyList<string> SharedNames { get; }

    /// <summary>Reads a partial view or layout from the shared views, matching the name in any case, as the build does.</summary>
    /// <param name="name">The name, such as <c>Header</c> or <c>_Layout</c>; already checked to be a plain name.</param>
    /// <returns>The shared view, or <see langword="null"/> when there is no such file.</returns>
    /// <example><code>var header = files.ReadShared("header"); // Views/Shared/Header.cvw</code></example>
    SharedView? ReadShared(string name);
}

/// <summary>
/// The shared views of a project on disk, as the build sees them: the files directly in the project's
/// <c>Views/Shared</c>, which an area's views use too (an <c>Areas/{Area}/Views/Shared</c> folder holds ordinary views
/// of an <c>{Area}/Shared</c> controller, not partials). The project is found from the previewed view's path. Only
/// files directly in that folder are read, and only by plain names, so a view cannot make the tool read anything else.
/// </summary>
/// <example><code>var files = ProjectViewFiles.ForView(@"C:\Shop\Views\Orders\ListView.cvw");</code></example>
internal sealed class ProjectViewFiles : IViewFiles
{
    /// <summary>The largest shared view read, in bytes; a bigger file is found but not read (<see cref="SharedView.IsTooLarge"/>).</summary>
    /// <example><code>var tooLarge = new FileInfo(path).Length > ProjectViewFiles.MaxFileSize;</code></example>
    public const long MaxFileSize = 1024 * 1024;

    /// <summary>
    /// The root namespace views are given in the preview. The project's own is not read (CVW005 needs a build); only
    /// its validity matters to the checks, and this one is valid.
    /// </summary>
    /// <example><code>ViewInfo.TryCreate(path, ProjectViewFiles.RootNamespace, out var view);</code></example>
    public const string RootNamespace = "Preview";

    private const string Extension = ".cvw";

    // The shared view files, by path, as the build orders them
    private readonly IReadOnlyList<FileInfo> _shared;

    private ProjectViewFiles(string? viewPath, IReadOnlyList<FileInfo> shared, bool isShared)
    {
        ViewPath = viewPath;
        _shared = shared;
        IsShared = isShared;
    }

    /// <summary>A view whose place is not known (read from standard input): no partials or layouts can be found.</summary>
    /// <example><code>var result = CvwPreview.Render(text, ProjectViewFiles.None, new PreviewOptions());</code></example>
    public static ProjectViewFiles None { get; } = new(null, [], isShared: false);

    /// <inheritdoc />
    public string? ViewPath { get; }

    /// <inheritdoc />
    public bool IsShared { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> SharedNames => _shared.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList();

    /// <summary>Finds the shared views for the view at <paramref name="viewPath"/>.</summary>
    /// <param name="viewPath">The view's path, such as <c>Views/Home/IndexView.cvw</c>.</param>
    /// <returns>The view's shared views; none when the path is not inside a controller folder of a <c>Views</c> folder.</returns>
    /// <example><code>var files = ProjectViewFiles.ForView("Areas/Admin/Views/Users/IndexView.cvw"); // uses Views/Shared</code></example>
    public static ProjectViewFiles ForView(string viewPath)
    {
        var full = Path.GetFullPath(viewPath);
        if (!ViewInfo.TryCreate(full, RootNamespace, out var view))
            return new ProjectViewFiles(full, [], isShared: false);

        // …/Views/{Controller}/X.cvw, or …/Areas/{Area}/Views/{Controller}/X.cvw whose partials are the project's
        var views = Path.GetDirectoryName(Path.GetDirectoryName(full));
        if (view.Area is not null)
        {
            var project = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(views)));
            views = project is null ? null : Path.Combine(project, "Views");
        }

        return new ProjectViewFiles(full, views is null ? [] : SharedFiles(views), view.IsPartial);
    }

    /// <summary>The <c>.cvw</c> files directly in <c>Shared</c> (any case, as for the build) inside <paramref name="views"/>.</summary>
    private static List<FileInfo> SharedFiles(string views)
    {
        try
        {
            // The folders' names are matched in any case, as the build matches them: views/shared is Views/Shared
            var wanted = new DirectoryInfo(views);
            var folders = wanted.Parent is { Exists: true } project
                ? project.EnumerateDirectories().Where(d => string.Equals(d.Name, wanted.Name, StringComparison.OrdinalIgnoreCase))
                : [];

            return folders
                .SelectMany(f => f.EnumerateDirectories().Where(d => string.Equals(d.Name, ViewInfo.SharedFolder, StringComparison.OrdinalIgnoreCase)))
                .SelectMany(d => d.EnumerateFiles())
                .Where(f => f.Name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && IsPlainName(Path.GetFileNameWithoutExtension(f.Name)))
                .OrderBy(f => f.FullName, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public SharedView? ReadShared(string name)
    {
        if (!IsPlainName(name))
            return null;

        var file = _shared.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f.Name), name, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return null;

        file.Refresh();
        if (!file.Exists)
            return null;

        return new SharedView(Path.GetFileNameWithoutExtension(file.Name), file.Length <= MaxFileSize ? File.ReadAllText(file.FullName) : null);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is a name, not a path: letters (of any alphabet), digits and underscores only,
    /// as in a C# identifier.
    /// </summary>
    /// <param name="name">The partial or layout name.</param>
    /// <returns><see langword="true"/> for a plain name.</returns>
    /// <example><code>ProjectViewFiles.IsPlainName("../Secret"); // false</code></example>
    public static bool IsPlainName(string name) =>
        name.Length is > 0 and <= 128 && name.All(c => char.IsLetterOrDigit(c) || c == '_');
}
