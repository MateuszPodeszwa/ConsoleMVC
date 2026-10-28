using ConsoleMVC.Generators;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Emit;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Preview;

/// <summary>
/// The diagnostics the build reports for a view, found with the generator's own checks: where the file is and what it
/// is called (<see cref="ViewSourceGenerator.CheckFile"/>), the markup, and the checks that need the project's shared
/// views (<see cref="ViewSourceGenerator.CheckMarkup"/> with a catalogue of <c>Views/Shared</c> built as the generator
/// builds it). Literal <c>nav</c> targets (CVW119) need the project's controllers, which only a build knows, so they
/// are not checked.
/// </summary>
/// <example>
/// <code>
/// var notes = new List&lt;string&gt;();
/// var diagnostics = ViewChecks.Check(text, document, ProjectViewFiles.ForView(path), notes);
/// </code>
/// </example>
internal static class ViewChecks
{
    /// <summary>The most shared views read for the checks; with more, the checks that need them are left out.</summary>
    /// <example><code>var checkedFully = files.SharedNames.Count &lt;= ViewChecks.MaxSharedViews;</code></example>
    public const int MaxSharedViews = 1000;

    /// <summary>No controllers: <c>nav</c> targets are not checked, as in a build of a project without controllers.</summary>
    private static readonly RouteCatalogue NoRoutes = new([]);

    /// <summary>Returns the diagnostics the build would report for the view.</summary>
    /// <param name="text">The view's text, which may be unsaved.</param>
    /// <param name="document">The view, parsed from <paramref name="text"/>.</param>
    /// <param name="files">Where the view is, and the project's shared views.</param>
    /// <param name="notes">Receives what could not be checked, and why.</param>
    /// <returns>The diagnostics, in the order the build reports them.</returns>
    /// <example>
    /// <code>
    /// var document = CvwDocumentParser.Parse(text, MarkupSchema.Default);
    /// foreach (var d in ViewChecks.Check(text, document, files, notes)) Console.WriteLine($"{d.Descriptor.Id} {d.Message}");
    /// </code>
    /// </example>
    public static IReadOnlyList<ParseDiagnostic> Check(string text, CvwDocument document, IViewFiles files, List<string> notes)
    {
        // Text from standard input without a file name: where it lives is unknown, so only its markup is checked
        if (files.ViewPath is not { } path)
            return document.Diagnostics;

        var problems = ViewSourceGenerator.CheckFile(path, ProjectViewFiles.RootNamespace, out var view);
        if (view is null)
            return problems; // the build skips the file, so its markup is not checked either

        if (!document.HasErrors && Catalogue(text, view, files, notes) is { } partials)
            return [.. problems, .. ViewSourceGenerator.CheckMarkup(document, view, partials, NoRoutes)];

        return [.. problems, .. document.Diagnostics];
    }

    /// <summary>
    /// The project's partial views and layouts as the generator catalogues them, with the view's unsaved text in place
    /// of its file when it is a shared view itself; <see langword="null"/> when a shared view cannot be read.
    /// </summary>
    private static PartialCatalogue? Catalogue(string text, ViewInfo view, IViewFiles files, List<string> notes)
    {
        var names = files.SharedNames;
        if (names.Count > MaxSharedViews)
        {
            notes.Add($"Views/Shared has more than {MaxSharedViews} views, so partial views and layouts (CVW010, CVW103, CVW114 to CVW118) are not checked.");
            return null;
        }

        var entries = new List<PartialEntry>();
        var self = false;
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? source;
            if (view.IsPartial && name == view.ClassName)
            {
                source = text;
                self = true;
            }
            else if (files.ReadShared(name) is not { } shared)
            {
                continue; // removed since it was listed
            }
            else if (shared.Text is null)
            {
                notes.Add($"The shared view '{shared.Name}' is larger than {ProjectViewFiles.MaxFileSize / (1024 * 1024)} MB, " +
                          "so partial views and layouts (CVW010, CVW103, CVW114 to CVW118) are not checked.");
                return null;
            }
            else
            {
                source = shared.Text;
            }

            if (Entry(name, source) is { } entry)
                entries.Add(entry);
        }

        // A shared view not saved yet is in the catalogue once it is
        if (view.IsPartial && !self && Entry(view.ClassName, text) is { } unsaved)
            entries.Add(unsaved);

        return new PartialCatalogue(entries);
    }

    /// <summary>
    /// The generator's entry for a shared view. The path only places it in a <c>Views/Shared</c> folder: every entry is
    /// given the same one, so the catalogue orders them by name, as the build orders the files of one folder.
    /// </summary>
    private static PartialEntry? Entry(string name, string text) =>
        ViewSourceGenerator.PartialEntryFor($"/Views/{ViewInfo.SharedFolder}/{name}.cvw", text, ProjectViewFiles.RootNamespace);
}
