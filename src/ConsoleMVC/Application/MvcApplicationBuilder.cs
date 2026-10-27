using System.Reflection;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Provides a fluent configuration API for building an <see cref="MvcApplication"/> instance.
/// Allows setting the default route, the assembly to scan and the markup renderer before the
/// application starts.
/// </summary>
/// <remarks>
/// This mirrors ASP.NET Core's <c>WebApplicationBuilder</c> pattern. Obtain an instance
/// via <see cref="MvcApplication.CreateBuilder"/>, configure the desired properties,
/// then call <see cref="Build"/> to create the application host.
/// </remarks>
/// <example>
/// <code>
/// var builder = MvcApplication.CreateBuilder(args);
/// builder.DefaultController = "Dashboard";
/// builder.ClearScreenBetweenViews = false;
/// builder.Build().Run();
/// </code>
/// </example>
public class MvcApplicationBuilder
{
    /// <summary>
    /// Gets or sets the name of the default controller to navigate to on application startup.
    /// </summary>
    /// <value>The controller name without the <c>Controller</c> suffix. Defaults to <c>"Home"</c>.</value>
    public string DefaultController { get; set; } = "Home";

    /// <summary>
    /// Gets or sets the name of the default action to invoke on application startup.
    /// </summary>
    /// <value>The action method name. Defaults to <c>"Index"</c>.</value>
    public string DefaultAction { get; set; } = "Index";

    /// <summary>
    /// Gets or sets the assembly scanned for controllers and views.
    /// </summary>
    /// <value>
    /// Defaults to <see langword="null"/>, meaning the entry assembly (<see cref="Assembly.GetEntryAssembly"/>).
    /// Set it explicitly when hosting the application from a test runner or another process.
    /// </value>
    /// <example><code>builder.ApplicationAssembly = typeof(Program).Assembly;</code></example>
    public Assembly? ApplicationAssembly { get; set; }

    /// <summary>
    /// Gets or sets the renderer used to clear the screen and draw C-View Markup.
    /// </summary>
    /// <value>
    /// Defaults to <see langword="null"/>, meaning <see cref="MarkupRenderer.CreateForConsole"/>, which detects
    /// colour support, terminal width and redirected output automatically.
    /// </value>
    /// <example>
    /// <code>
    /// // Render plain text at a fixed width, e.g. for snapshot tests
    /// builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(writer), () => 60);
    /// </code>
    /// </example>
    public IMarkupRenderer? Renderer { get; set; }

    /// <summary>
    /// Gets or sets whether the screen is cleared before each view renders.
    /// </summary>
    /// <value>Defaults to <see langword="true"/>.</value>
    public bool ClearScreenBetweenViews { get; set; } = true;

    /// <summary>
    /// Builds and returns a fully configured <see cref="MvcApplication"/> instance
    /// ready to be started with <see cref="MvcApplication.Run"/>.
    /// </summary>
    /// <returns>A new <see cref="MvcApplication"/> instance configured with the current builder settings.</returns>
    public MvcApplication Build()
    {
        return new MvcApplication(this);
    }
}
