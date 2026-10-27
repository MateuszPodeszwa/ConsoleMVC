namespace ConsoleMVC.Mvc;

/// <summary>
/// Provides a fluent configuration API for building an <see cref="MvcApplication"/> instance.
/// Allows setting the default controller and action before the application starts.
/// </summary>
/// <remarks>
/// This mirrors ASP.NET Core's <c>WebApplicationBuilder</c> pattern. Obtain an instance
/// via <see cref="MvcApplication.CreateBuilder"/>, configure the desired properties,
/// then call <see cref="Build"/> to create the application host.
/// </remarks>
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
    /// Builds and returns a fully configured <see cref="MvcApplication"/> instance
    /// ready to be started with <see cref="MvcApplication.Run"/>.
    /// </summary>
    /// <returns>A new <see cref="MvcApplication"/> instance configured with the current builder settings.</returns>
    public MvcApplication Build()
    {
        return new MvcApplication(this);
    }
}
