namespace ConsoleMVC.Preview.Tests;

/// <summary>Shared views held in memory, for previews that use partial views and layouts without files on disk.</summary>
/// <param name="shared">The shared views by name, such as <c>["_Layout"] = "&lt;body /&gt;"</c>.</param>
/// <param name="isShared">Whether the previewed view is itself a shared view.</param>
/// <example>
/// <code>
/// var files = new InMemoryViewFiles(new() { ["Header"] = "&lt;text&gt;Shop&lt;/text&gt;" });
/// var result = CvwPreview.Render("&lt;partial name=\"Header\" /&gt;", files, new PreviewOptions());
/// </code>
/// </example>
internal sealed class InMemoryViewFiles(Dictionary<string, string> shared, bool isShared = false) : IViewFiles
{
    /// <summary>Gets the names asked for, in order, so a test can check what was read.</summary>
    public List<string> Requested { get; } = [];

    /// <inheritdoc />
    /// <remarks>Unknown, so only the markup is checked, as for text from standard input.</remarks>
    public string? ViewPath => null;

    /// <inheritdoc />
    public bool IsShared { get; } = isShared;

    /// <inheritdoc />
    public IReadOnlyList<string> SharedNames => shared.Keys.Order(StringComparer.Ordinal).ToList();

    /// <inheritdoc />
    public SharedView? ReadShared(string name)
    {
        Requested.Add(name);
        return shared.TryGetValue(name, out var text) ? new SharedView(name, text) : null;
    }
}
