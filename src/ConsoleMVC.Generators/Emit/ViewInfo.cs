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

    /// <summary>Gets the namespace of the generated class: <c>{RootNamespace}.Views.{Controller}</c>.</summary>
    public string Namespace { get; }

    /// <summary>Gets the hint name for the generated source file, e.g. <c>Home_IndexView.g.cs</c>.</summary>
    public string HintName => ControllerName + "_" + ClassName + ".g.cs";

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

        info = new ViewInfo(filePath, rootNamespace, parts[parts.Length - 2], className);
        return true;
    }
}
