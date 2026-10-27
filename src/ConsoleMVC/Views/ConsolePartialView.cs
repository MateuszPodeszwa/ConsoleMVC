namespace ConsoleMVC.Mvc;

/// <summary>
/// Non-generic base class of partial views: reusable pieces of markup, written as <c>.cvw</c> files in
/// <c>Views/Shared</c>, that views render with <c>&lt;partial name="…" /&gt;</c>.
/// </summary>
/// <remarks>
/// A partial renders into the view that includes it, sharing its renderer, view data, model state and form. It cannot
/// navigate, so it has no <c>&lt;menu&gt;</c> and returns nothing. The router never routes to a partial.
/// </remarks>
/// <example>
/// <code>
/// @* Views/Shared/Header.cvw *@
/// @model string
/// &lt;box border="double" align="centre"&gt;@Model&lt;/box&gt;
/// </code>
/// </example>
public abstract class ConsolePartialView : ConsoleViewBase
{
}

/// <summary>
/// Base class of partial views with a strongly-typed model. <c>.cvw</c> files in <c>Views/Shared</c> are compiled into
/// subclasses of this type by the source generator.
/// </summary>
/// <typeparam name="TModel">The model the partial expects, declared with <c>@model</c> (defaults to <see cref="object"/>).</typeparam>
/// <example>
/// <code>
/// &lt;partial name="Header" model="@Model.Title" /&gt;
/// </code>
/// </example>
public abstract class ConsolePartialView<TModel> : ConsolePartialView
{
    /// <summary>
    /// Renders the partial with the model given in the <c>&lt;partial&gt;</c> element.
    /// </summary>
    /// <param name="Model">The model passed by the including view.</param>
    public abstract void Render(TModel Model);
}
