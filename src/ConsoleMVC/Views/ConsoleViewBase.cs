using ConsoleMVC.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The members shared by full views (<see cref="ConsoleView"/>) and partial views (<see cref="ConsolePartialView"/>):
/// view data, model state, the renderer, user input and the form being filled in.
/// </summary>
/// <remarks>
/// Generated code relies on these members (for example <c>Renderer.Render(...)</c> for a top-level element), so markup
/// works the same way in views and partials. Do not inherit from this class directly.
/// </remarks>
public abstract class ConsoleViewBase
{
    /// <summary>Services used when a view created outside the application renders a partial that uses <c>@inject</c>.</summary>
    private static readonly Lazy<IServiceProvider> NoServices = new(() => new ServiceCollection().BuildServiceProvider());

    /// <summary>
    /// Shared console renderer used by views that are created outside the application loop
    /// (for example in unit tests or when rendering a view manually).
    /// </summary>
    private static readonly Lazy<IMarkupRenderer> DefaultRenderer = new(MarkupRenderer.CreateForConsole);

    private IMarkupRenderer? _renderer;
    private ITextInput? _input;

    /// <summary>
    /// Gets or sets the <see cref="ViewDataDictionary"/> used to pass supplementary
    /// key-value data from the controller to the view.
    /// </summary>
    /// <value>
    /// A dictionary of loosely-typed data items shared between the controller and view.
    /// </value>
    public ViewDataDictionary ViewData { get; internal set; } = new();

    /// <summary>
    /// Gets the validation errors from the action that rendered this view (see <see cref="Controller.ModelState"/>).
    /// Show them with <c>&lt;errors /&gt;</c> or <c>&lt;errors for="Name" /&gt;</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// @if (!ModelState.IsValid) { &lt;text color="red"&gt;Please correct the errors below.&lt;/text&gt; }
    /// </code>
    /// </example>
    public ModelStateDictionary ModelState { get; internal set; } = new();

    /// <summary>
    /// Gets the renderer that draws C-View Markup elements written in this view. The framework
    /// assigns the renderer configured on <see cref="MvcApplicationBuilder.Renderer"/>; views
    /// created manually fall back to a shared console renderer.
    /// </summary>
    /// <example>
    /// <code>
    /// // Generated code for a top-level &lt;text&gt; element calls:
    /// Renderer.Render(textElement);
    /// </code>
    /// </example>
    public IMarkupRenderer Renderer
    {
        get => _renderer ??= DefaultRenderer.Value;
        internal set => _renderer = value;
    }

    /// <summary>
    /// Gets the source of user input used by <c>&lt;input&gt;</c> and <c>&lt;menu&gt;</c>. The framework assigns
    /// <see cref="MvcApplicationBuilder.Input"/>; views created manually fall back to the console.
    /// </summary>
    public ITextInput Input
    {
        get => _input ??= ConsoleTextInput.Instance;
        internal set => _input = value;
    }

    /// <summary>
    /// Gets the values collected by <c>&lt;input&gt;</c> elements in this view, keyed by their <c>name</c>
    /// (case-insensitive). Post them to a controller with <c>NavigationResult.ToAction("Save", Form)</c> or a
    /// menu option marked <c>post</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;input name="Name" label="Name: " required /&gt;
    /// return NavigationResult.ToAction("Result", Form);
    /// </code>
    /// </example>
    public Dictionary<string, string> Form { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a value for an <c>&lt;input&gt;</c> element and stores it in <see cref="Form"/>. Generated code calls
    /// this for every top-level <c>&lt;input&gt;</c>.
    /// </summary>
    /// <param name="input">The input element to read.</param>
    /// <returns>The value read; an empty string when the input has ended.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
    protected internal string ReadInput(InputElement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var value = input.Read(Renderer, Input) ?? "";
        Form[input.Name] = value;
        return value;
    }

    /// <summary>Theme used by views created outside the application (for example in unit tests).</summary>
    private static readonly Lazy<Theme> DefaultTheme = new(Theme.CreateDefault);

    private Theme? _theme;

    /// <summary>
    /// Gets the theme whose named styles elements use through their <c>class</c> attribute. The framework assigns
    /// <see cref="MvcApplicationBuilder.Theme"/>; views created manually use <see cref="Rendering.Theme.CreateDefault"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// // Generated code for &lt;text class="title"&gt; calls:
    /// Theme.Apply(textElement, "title");
    /// </code>
    /// </example>
    public Theme Theme
    {
        get => _theme ??= DefaultTheme.Value;
        internal set => _theme = value;
    }

    /// <summary>
    /// Gets or sets the services of the current navigation cycle, used to fill <c>@inject</c> properties of partials.
    /// Assigned by <see cref="Router.ResolveView(string, string, IServiceProvider)"/>.
    /// </summary>
    internal IServiceProvider? Services { get; set; }

    /// <summary>
    /// Prepares a partial view to render inside this view: it shares this view's renderer, input, view data, model
    /// state and <see cref="Form"/>, and receives its <c>@inject</c> services. Generated code calls this for every
    /// <c>&lt;partial&gt;</c> element.
    /// </summary>
    /// <param name="partial">The partial view about to render.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partial"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a service the partial injects is not registered.</exception>
    /// <example>
    /// <code>
    /// // &lt;partial name="Header" model="@Model.User" /&gt; generates:
    /// var header = new MyApp.Views.Shared.Header();
    /// PreparePartial(header);
    /// header.Render(Model.User);
    /// </code>
    /// </example>
    protected internal void PreparePartial(ConsolePartialView partial)
    {
        ArgumentNullException.ThrowIfNull(partial);

        partial.ViewData = ViewData;
        partial.ModelState = ModelState;
        partial.Renderer = Renderer;
        partial.Input = Input;
        partial.Form = Form;
        partial.Theme = Theme;
        partial.Services = Services;

        ServiceInjector.InjectProperties(partial, Services ?? NoServices.Value);
    }
}
