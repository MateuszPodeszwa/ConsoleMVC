namespace ConsoleMVC.Rendering;

/// <summary>
/// Horizontal alignment of text within the width available to it.
/// </summary>
/// <example><code>var text = new TextElement { Align = Alignment.Centre };</code></example>
public enum Alignment
{
    /// <summary>Align to the left edge (default).</summary>
    Left,

    /// <summary>Centre within the available width. In markup both <c>centre</c> and <c>center</c> are accepted.</summary>
    Centre,

    /// <summary>Align to the right edge.</summary>
    Right
}
