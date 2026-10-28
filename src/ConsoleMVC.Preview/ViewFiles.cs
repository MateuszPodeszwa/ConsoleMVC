namespace ConsoleMVC.Preview;

/// <summary>
/// Finds the partial views and layouts a view uses, by name. Implemented over the project's files by
/// <see cref="ProjectViewFiles"/>, and in memory by tests.
/// </summary>
/// <example>
/// <code>
/// IViewFiles files = ProjectViewFiles.ForView("Views/Home/IndexView.cvw");
/// string? header = files.ReadShared("Header"); // the text of Views/Shared/Header.cvw
/// </code>
/// </example>
internal interface IViewFiles
{
    /// <summary>Reads a partial view or layout from the shared views.</summary>
    /// <param name="name">The name, such as <c>Header</c> or <c>_Layout</c>; already checked to be a plain name.</param>
    /// <returns>The file's text, or <see langword="null"/> when there is no such file.</returns>
    string? ReadShared(string name);

    /// <summary>Gets whether the view being previewed is itself a shared view (a partial or a layout).</summary>
    bool IsShared { get; }
}

/// <summary>
/// The shared views of a project on disk: <c>Views/Shared</c>, and first <c>Areas/{Area}/Views/Shared</c> for a view in
/// an area, found from the previewed view's path. Only files directly in those folders are read, and only by plain
/// names, so a view cannot make the tool read anything else.
/// </summary>
/// <example><code>var files = ProjectViewFiles.ForView(@"C:\Shop\Views\Orders\ListView.cvw");</code></example>
internal sealed class ProjectViewFiles : IViewFiles
{
    /// <summary>The largest shared view read, in bytes; a bigger file is treated as missing.</summary>
    public const long MaxFileSize = 1024 * 1024;

    private readonly IReadOnlyList<string> _sharedFolders;

    private ProjectViewFiles(IReadOnlyList<string> sharedFolders, bool isShared)
    {
        _sharedFolders = sharedFolders;
        IsShared = isShared;
    }

    /// <summary>A view with no project around it: no partials or layouts can be found.</summary>
    public static ProjectViewFiles None { get; } = new([], isShared: false);

    /// <inheritdoc />
    public bool IsShared { get; }

    /// <summary>Finds the shared folders for the view at <paramref name="viewPath"/>.</summary>
    /// <param name="viewPath">The view's path, such as <c>Views/Home/IndexView.cvw</c>.</param>
    /// <returns>The view's shared views; <see cref="None"/> when the path is not inside a <c>Views</c> folder.</returns>
    public static ProjectViewFiles ForView(string viewPath)
    {
        var full = Path.GetFullPath(viewPath);
        var directory = Path.GetDirectoryName(full);

        // Views/{Controller}/X.cvw or Views/Shared/X.cvw: the folder above is Views
        var views = directory is null ? null : Path.GetDirectoryName(directory);
        if (views is null || !string.Equals(Path.GetFileName(views), "Views", StringComparison.OrdinalIgnoreCase))
            return None;

        var isShared = string.Equals(Path.GetFileName(directory), "Shared", StringComparison.OrdinalIgnoreCase);
        var folders = new List<string> { Path.Combine(views, "Shared") };

        // Areas/{Area}/Views: the area's shared views first, then the project's
        var area = Path.GetDirectoryName(views);
        var areas = area is null ? null : Path.GetDirectoryName(area);
        if (areas is not null && string.Equals(Path.GetFileName(areas), "Areas", StringComparison.OrdinalIgnoreCase))
        {
            var project = Path.GetDirectoryName(areas);
            if (project is not null)
                folders.Add(Path.Combine(project, "Views", "Shared"));
        }

        return new ProjectViewFiles(folders, isShared);
    }

    /// <inheritdoc />
    public string? ReadShared(string name)
    {
        if (!IsPlainName(name))
            return null;

        foreach (var folder in _sharedFolders)
        {
            var file = new FileInfo(Path.Combine(folder, name + ".cvw"));
            if (file.Exists && file.Length <= MaxFileSize)
                return File.ReadAllText(file.FullName);
        }

        return null;
    }

    /// <summary>Whether <paramref name="name"/> is a name, not a path: letters, digits and underscores only.</summary>
    /// <param name="name">The partial or layout name.</param>
    /// <returns><see langword="true"/> for a plain name.</returns>
    public static bool IsPlainName(string name) =>
        name.Length is > 0 and <= 128 && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
