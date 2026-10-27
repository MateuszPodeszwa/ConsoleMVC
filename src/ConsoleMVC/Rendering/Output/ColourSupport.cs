namespace ConsoleMVC.Rendering;

/// <summary>
/// How many colours a terminal can display.
/// </summary>
public enum ColourSupport
{
    /// <summary>No colour (for example when <c>NO_COLOR</c> is set). Decorations such as bold are still used.</summary>
    None,

    /// <summary>The 16 standard console colours. RGB colours are mapped to the nearest one.</summary>
    Basic,

    /// <summary>24-bit true colour.</summary>
    TrueColour
}
