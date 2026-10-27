namespace ConsoleMVC.Mvc;

/// <summary>
/// Base class of layout views: a <c>.cvw</c> file in <c>Views/Shared</c> that contains a <c>&lt;body /&gt;</c>
/// placeholder. Views use it with <c>@layout Name</c>; <c>Views/Shared/_Layout.cvw</c> is used by every view that does
/// not say otherwise.
/// </summary>
/// <remarks>
/// The markup before <c>&lt;body /&gt;</c> (the header) is drawn before the view. The markup after it (the footer) is
/// drawn just before the view's first <c>&lt;input&gt;</c> or <c>&lt;menu&gt;</c>, or when the view returns if it has
/// neither: a console screen ends where the view starts waiting for the user, so a footer drawn later would never be
/// seen. A layout shares the view's renderer, view data, model state, form and theme, and may use <c>@inject</c>.
/// </remarks>
/// <example>
/// <code>
/// @* Views/Shared/_Layout.cvw *@
/// &lt;box class="panel" title="My App"&gt;&lt;text&gt;@DateTime.Now:t&lt;/text&gt;&lt;/box&gt;
/// &lt;body /&gt;
/// &lt;separator /&gt;
/// </code>
/// </example>
public abstract class ConsoleLayoutView : ConsoleViewBase
{
    /// <summary>Draws the part of the layout before <c>&lt;body /&gt;</c>.</summary>
    public abstract void RenderHeader();

    /// <summary>Draws the part of the layout after <c>&lt;body /&gt;</c>.</summary>
    public abstract void RenderFooter();
}
