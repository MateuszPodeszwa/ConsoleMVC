#nullable enable

using System;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// Where a view lives and what the generated class is called, derived from the <c>.cvw</c> file path.
/// </summary>
/// <example>
/// <code>
/// // /src/MyApp/Views/Home/IndexView.cvw with RootNamespace "MyApp":
/// ViewInfo.TryCreate(path, "MyApp", out var info);
/// // info.Namespace == "MyApp.Views.Home", info.ClassName == "IndexView", info.ControllerName == "Home"
/// </code>
/// </example>
internal sealed class ViewInfo
{
    private ViewInfo(string filePath, string rootNamespace, string controllerName, string className)
    {
        FilePath = filePath;
        ControllerName = controllerName;
        ClassName = className;
        Namespace = rootNamespace + ".Views." + controllerName;
    }

    /// <summary>Gets the path of the <c>.cvw</c> file, used in <c>#line</c> directives.</summary>
    public string FilePath { get; }

    /// <summary>Gets the controller name (the folder directly inside <c>Views</c>).</summary>
    public string ControllerName { get; }

    /// <summary>Gets the generated class name (the file name without extension).</summary>
    public string ClassName { get; }

    /// <summary>The folder whose views are partials (<c>Views/Shared</c>).</summary>
    public const string SharedFolder = "Shared";

    /// <summary>
    /// Gets a value indicating whether this file is a partial view (<c>Views/Shared/{Name}.cvw</c>), compiled to a
    /// <c>ConsolePartialView&lt;TModel&gt;</c> rendered with <c>&lt;partial name="{Name}" /&gt;</c>.
    /// </summary>
    public bool IsPartial => ControllerName == SharedFolder;

    /// <summary>Gets the namespace of the generated class: <c>{RootNamespace}.Views.{Controller}</c>.</summary>
    public string Namespace { get; }

    /// <summary>Gets the fully-qualified name of the generated class.</summary>
    public string FullClassName => Namespace + "." + ClassName;

    /// <summary>
    /// Gets the hint name for the generated source file, e.g. <c>MyApp.Views.Home.IndexView.1a2b3c4d.g.cs</c>.
    /// The path hash keeps hint names unique even for views whose names differ only in case or placement.
    /// </summary>
    public string HintName => FullClassName + "." + StablePathHash(FilePath) + ".g.cs";

    /// <summary>
    /// A 32-bit FNV-1a hash of the path, as eight hex digits. Stable across processes (unlike <c>string.GetHashCode</c>).
    /// </summary>
    private static string StablePathHash(string path)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in path)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Derives view information from a file path of the form <c>…/Views/{Controller}/{Name}.cvw</c>.
    /// </summary>
    /// <param name="filePath">The full path of the <c>.cvw</c> file.</param>
    /// <param name="rootNamespace">The consuming project's root namespace.</param>
    /// <param name="info">The view information when the path follows the convention.</param>
    /// <returns><see langword="false"/> when the file is not directly inside a controller folder under <c>Views</c>.</returns>
    public static bool TryCreate(string filePath, string rootNamespace, out ViewInfo info)
    {
        info = null!;
        var parts = filePath.Replace('\\', '/').Split('/');

        if (parts.Length < 3 || !string.Equals(parts[parts.Length - 3], "Views", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = parts[parts.Length - 1];
        var dot = fileName.LastIndexOf('.');
        var className = dot > 0 ? fileName.Substring(0, dot) : fileName;

        // Views/shared and Views/Shared are both the partial view folder; its namespace is always *.Views.Shared
        var folder = parts[parts.Length - 2];
        if (string.Equals(folder, SharedFolder, StringComparison.OrdinalIgnoreCase))
            folder = SharedFolder;

        info = new ViewInfo(filePath, rootNamespace, folder, className);
        return true;
    }
}
