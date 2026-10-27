using System.Reflection;
using ConsoleMVC.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
/// builder.Services.AddSingleton&lt;IGreetingService, GreetingService&gt;();
/// using var app = builder.Build();
/// app.Run();
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
    /// Gets or sets where <c>&lt;input&gt;</c> and <c>&lt;menu&gt;</c> elements read user input from.
    /// </summary>
    /// <value>Defaults to <see langword="null"/>, meaning <see cref="ConsoleTextInput.Instance"/>.</value>
    /// <example>
    /// <code>
    /// // Drive an application with scripted input, e.g. in an end-to-end test
    /// builder.Input = new MyScriptedInput("1", "Ada", "q");
    /// </code>
    /// </example>
    public ITextInput? Input { get; set; }

    /// <summary>
    /// Gets the services available to controllers (through their constructors) and views (through <c>@inject</c>).
    /// </summary>
    /// <remarks>
    /// Each navigation cycle — one action and the view it renders — runs in its own scope, so services registered
    /// with <c>AddScoped</c> are shared by that controller and view and disposed when the next screen starts.
    /// The framework also registers <see cref="IMarkupRenderer"/> and <see cref="ITextInput"/> unless you do.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddSingleton&lt;IGreetingService, GreetingService&gt;();
    /// builder.Services.AddScoped&lt;IBasket, Basket&gt;();
    /// </code>
    /// </example>
    public IServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>
    /// Gets or sets the theme: named styles that markup elements use through their <c>class</c> attribute
    /// (<c>&lt;text class="title"&gt;</c>).
    /// </summary>
    /// <value>Defaults to <see cref="Theme.CreateDefault"/>, which defines <c>title</c>, <c>muted</c>, <c>success</c>,
    /// <c>warning</c>, <c>danger</c>, <c>info</c> and <c>panel</c>.</value>
    /// <example>
    /// <code>
    /// builder.Theme.Set("brand", new ThemeStyle { Foreground = Colour.Magenta, Bold = true });
    /// builder.Theme = Theme.Empty; // or start from nothing
    /// </code>
    /// </example>
    public Theme Theme { get; set; } = Theme.CreateDefault();

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
    /// <remarks>
    /// <see cref="Services"/> is copied, so changing it afterwards does not affect the built application.
    /// <see cref="Renderer"/> and <see cref="Input"/>, when set, take precedence over services of the same type.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the service registrations are invalid.</exception>
    public MvcApplication Build()
    {
        return new MvcApplication(this, BuildServiceProvider());
    }

    /// <summary>
    /// Builds the root service provider from a copy of <see cref="Services"/> plus the framework's own services.
    /// Scope validation is on, so a scoped service resolved outside a navigation cycle fails immediately.
    /// </summary>
    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        foreach (var descriptor in Services)
            services.Add(descriptor);

        if (Renderer is not null)
            services.AddSingleton(Renderer); // the last registration wins
        else
            services.TryAddSingleton<IMarkupRenderer>(_ => MarkupRenderer.CreateForConsole());

        services.AddSingleton(Theme ?? Theme.Empty);

        if (Input is not null)
            services.AddSingleton(Input);
        else
            services.TryAddSingleton<ITextInput>(ConsoleTextInput.Instance);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
