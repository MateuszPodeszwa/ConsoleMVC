namespace ConsoleMVC.Rendering;

/// <summary>
/// Named styles that markup elements use through their <c>class</c> attribute, so colours and borders are defined once
/// for the whole application: <c>&lt;text class="title"&gt;</c>, <c>&lt;box class="panel"&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Class names are case-insensitive. An element may list several classes separated by spaces; they are applied in
/// order, and attributes written on the element override them all. Unknown class names are ignored, so a view keeps
/// working when a theme does not define a class.
/// </para>
/// <para>
/// <see cref="CreateDefault"/> defines <c>title</c>, <c>muted</c>, <c>success</c>, <c>warning</c>, <c>danger</c>,
/// <c>info</c> and <c>panel</c>; replace or extend them on <c>MvcApplicationBuilder.Theme</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.Theme
///     .Set("brand", new ThemeStyle { Foreground = Colour.Magenta, Bold = true })
///     .Set("panel", new ThemeStyle { Border = BorderStyle.Double, BorderColour = Colour.Magenta });
/// </code>
/// </example>
public sealed class Theme
{
    private readonly Dictionary<string, ThemeStyle> _styles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets a new, empty theme: every class is ignored until you add some with <see cref="Set"/>.</summary>
    /// <example><code>builder.Theme = Theme.Empty.Set("brand", new ThemeStyle { Bold = true });</code></example>
    public static Theme Empty => new();

    /// <summary>Gets the names of the classes defined.</summary>
    public IEnumerable<string> Names => _styles.Keys;

    /// <summary>
    /// Creates a theme with the built-in classes, which use the terminal's standard colours.
    /// </summary>
    /// <returns>A new, modifiable theme.</returns>
    /// <example><code>var theme = Theme.CreateDefault().Set("brand", new ThemeStyle { Bold = true });</code></example>
    public static Theme CreateDefault() => new Theme()
        .Set("title", new ThemeStyle { Foreground = Colour.Cyan, Bold = true })
        .Set("muted", new ThemeStyle { Dim = true })
        .Set("success", new ThemeStyle { Foreground = Colour.Green })
        .Set("warning", new ThemeStyle { Foreground = Colour.Yellow })
        .Set("danger", new ThemeStyle { Foreground = Colour.Red, Bold = true })
        .Set("info", new ThemeStyle { Foreground = Colour.Blue })
        .Set("panel", new ThemeStyle { Border = BorderStyle.Rounded, BorderColour = Colour.Cyan, Padding = 2 });

    /// <summary>
    /// Creates a theme with the built-in classes in bold, bright colours with strong contrast and no dim text, for people
    /// who find the default colours hard to read. Turned on with <c>builder.HighContrast = true</c> or the
    /// <c>ConsoleMVC:HighContrast</c> setting.
    /// </summary>
    /// <returns>A new, modifiable theme.</returns>
    /// <example><code>builder.Theme = Theme.CreateHighContrast();</code></example>
    public static Theme CreateHighContrast() => new Theme()
        .Set("title", new ThemeStyle { Foreground = Colour.White, Bold = true, Underline = true })
        .Set("muted", new ThemeStyle { Foreground = Colour.White })
        .Set("success", new ThemeStyle { Foreground = Colour.Green, Bold = true })
        .Set("warning", new ThemeStyle { Foreground = Colour.Yellow, Bold = true })
        .Set("danger", new ThemeStyle { Foreground = Colour.Red, Bold = true, Underline = true })
        .Set("info", new ThemeStyle { Foreground = Colour.Cyan, Bold = true })
        .Set("panel", new ThemeStyle { Border = BorderStyle.Heavy, BorderColour = Colour.White, Padding = 1 });

    /// <summary>
    /// Defines or replaces a class.
    /// </summary>
    /// <param name="name">The class name: letters, digits, <c>-</c> or <c>_</c>.</param>
    /// <param name="style">The style to apply.</param>
    /// <returns>This theme, for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or contains other characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="style"/> is <see langword="null"/>.</exception>
    /// <example><code>theme.Set("price", new ThemeStyle { Foreground = Colour.Green, Align = Alignment.Right });</code></example>
    public Theme Set(string name, ThemeStyle style)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(style);
        if (name.Length == 0 || !name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            throw new ArgumentException($"'{name}' is not a valid class name: use letters, digits, '-' or '_'.", nameof(name));

        _styles[name] = style;
        return this;
    }

    /// <summary>Returns whether a class is defined.</summary>
    /// <param name="name">The class name (case-insensitive).</param>
    /// <example><code>theme.Contains("title"); // true for the default theme</code></example>
    public bool Contains(string name) => _styles.ContainsKey(name);

    /// <summary>
    /// Applies the classes in <paramref name="classNames"/> to <paramref name="element"/>, in order. Generated code calls
    /// this for an element's <c>class</c> attribute before assigning its other attributes.
    /// </summary>
    /// <param name="element">The element to style.</param>
    /// <param name="classNames">Class names separated by spaces; unknown names are ignored.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is <see langword="null"/>.</exception>
    /// <example><code>theme.Apply(text, "title danger");</code></example>
    public void Apply(Element element, string? classNames)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrWhiteSpace(classNames))
            return;

        foreach (var name in classNames.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (_styles.TryGetValue(name, out var style))
                style.ApplyTo(element);
        }
    }
}
