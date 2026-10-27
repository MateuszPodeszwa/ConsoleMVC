using System.Reflection;
using ConsoleMVC.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Provides a fluent configuration API for building an <see cref="MvcApplication"/> instance.
/// Allows setting the default route, the assembly to scan and the markup renderer before the
/// application starts.
/// </summary>
/// <remarks>
/// This mirrors ASP.NET Core's <c>WebApplicationBuilder</c> pattern. Obtain an instance
/// via <see cref="MvcApplication.CreateBuilder(string[])"/>, configure the desired properties,
/// then call <see cref="Build"/> to create the application host.
/// </remarks>
/// <example>
/// <code>
/// var builder = MvcApplication.CreateBuilder(args);
/// builder.DefaultController = "Dashboard";
/// builder.ClearScreenBetweenViews = false;
/// builder.Services.AddSingleton&lt;IGreetingService, GreetingService&gt;();
/// builder.Services.Configure&lt;ShopOptions&gt;(builder.Configuration.GetSection("Shop"));
/// builder.Logging.AddDebug();
/// using var app = builder.Build();
/// app.Run();
/// </code>
/// </example>
public class MvcApplicationBuilder
{
    /// <summary>The environment used when neither the options nor <c>DOTNET_ENVIRONMENT</c> name one.</summary>
    public const string DefaultEnvironmentName = "Production";

    /// <summary>
    /// Initialises a builder without command-line arguments. Prefer <see cref="MvcApplication.CreateBuilder(string[])"/>.
    /// </summary>
    /// <example><code>var builder = new MvcApplicationBuilder();</code></example>
    public MvcApplicationBuilder()
        : this(new MvcApplicationOptions())
    {
    }

    /// <summary>
    /// Initialises a builder: works out the environment, reads the configuration (see <see cref="Configuration"/>)
    /// and registers configuration, options and logging services.
    /// </summary>
    /// <param name="options">The arguments, environment and content root.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when a configuration file is not valid JSON.</exception>
    /// <example><code>var builder = new MvcApplicationBuilder(new MvcApplicationOptions { Args = args });</code></example>
    public MvcApplicationBuilder(MvcApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var environmentName = options.EnvironmentName;
        if (string.IsNullOrWhiteSpace(environmentName))
            environmentName = System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environmentName))
            environmentName = DefaultEnvironmentName;

        var applicationName = options.ApplicationName ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "ConsoleMVC";
        var contentRoot = options.ContentRootPath ?? AppContext.BaseDirectory;
        Environment = new ConsoleHostEnvironment(environmentName, applicationName, contentRoot);

        // Later sources win: appsettings.json < appsettings.{Environment}.json < environment variables < command line
        Configuration.SetBasePath(contentRoot);
        Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        Configuration.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false);
        Configuration.AddEnvironmentVariables();
        if (options.Args.Length > 0)
            Configuration.AddCommandLine(options.Args);

        Services.AddSingleton<IConfiguration>(Configuration);
        Services.AddSingleton<IHostEnvironment>(Environment);
        Services.AddOptions();

        // No providers: the console is the user interface, so log output must be sent somewhere else on purpose
        Services.AddLogging();
        Logging = new MvcLoggingBuilder(Services);
        Logging.AddConfiguration(Configuration.GetSection("Logging"));
    }

    /// <summary>
    /// Gets the application's configuration, read when the builder is created from, in order (later sources win):
    /// <c>appsettings.json</c> and <c>appsettings.{Environment}.json</c> in <see cref="IHostEnvironment.ContentRootPath"/>
    /// (both optional), environment variables (<c>Shop__Name</c> for <c>Shop:Name</c>) and the command line
    /// (<c>--Shop:Name=Corner</c>). Add more sources here. It is registered as <see cref="IConfiguration"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var pageSize = builder.Configuration.GetValue("Shop:PageSize", 20);
    /// builder.Services.Configure&lt;ShopOptions&gt;(builder.Configuration.GetSection("Shop"));
    /// builder.Configuration.AddJsonFile("secrets.json", optional: true);
    /// </code>
    /// </example>
    public ConfigurationManager Configuration { get; } = new();

    /// <summary>
    /// Gets the environment: its name (from <see cref="MvcApplicationOptions.EnvironmentName"/>, the
    /// <c>DOTNET_ENVIRONMENT</c> variable, or <see cref="DefaultEnvironmentName"/>), the application name and the
    /// content root. It is registered as <see cref="IHostEnvironment"/>.
    /// </summary>
    /// <example><code>if (builder.Environment.IsDevelopment()) builder.Logging.SetMinimumLevel(LogLevel.Debug);</code></example>
    public IHostEnvironment Environment { get; }

    /// <summary>
    /// Gets the logging configuration. <see cref="ILogger{TCategoryName}"/> can be injected into controllers, views and
    /// services; levels follow the <c>Logging</c> section of the configuration. No provider is added by default,
    /// because the console shows the application: add one that writes elsewhere, such as <c>AddDebug()</c>, a file
    /// or a log service.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Logging.AddDebug();                      // Microsoft.Extensions.Logging.Debug
    /// builder.Logging.SetMinimumLevel(LogLevel.Debug);
    /// </code>
    /// </example>
    public ILoggingBuilder Logging { get; }
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
    /// Gets or sets where <c>&lt;input&gt;</c>, <c>&lt;select&gt;</c>, <c>&lt;radio&gt;</c>, <c>&lt;checkbox&gt;</c> and
    /// <c>&lt;menu&gt;</c> elements read user input from.
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
    /// Gets or sets whether the application runs on the terminal's alternate screen, like a full-screen editor: the
    /// user's scrollback is left untouched and comes back when the application ends (also after an error or Ctrl+C).
    /// Defaults to <see langword="false"/>. Ignored when the output is not a terminal that supports it.
    /// </summary>
    /// <example><code>builder.FullScreen = true;</code></example>
    public bool FullScreen { get; set; }

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
    /// <exception cref="OptionsValidationException">
    /// Thrown when options registered with <c>ValidateOnStart()</c> are invalid, so bad configuration fails at start-up.
    /// </exception>
    public MvcApplication Build()
    {
        var services = BuildServiceProvider();
        try
        {
            services.GetService<IStartupValidator>()?.Validate();
        }
        catch
        {
            services.Dispose();
            throw;
        }

        return new MvcApplication(this, services);
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
        services.TryAddSingleton<ILiveDisplay>(provider => new LiveDisplay(provider.GetRequiredService<IMarkupRenderer>()));

        // Also when Services was cleared: these calls only add what is missing
        services.TryAddSingleton<IConfiguration>(Configuration);
        services.TryAddSingleton(Environment);
        services.AddOptions();
        services.AddLogging(logging => logging.AddConfiguration(Configuration.GetSection("Logging")));

        if (Input is not null)
            services.AddSingleton(Input);
        else
            services.TryAddSingleton<ITextInput>(ConsoleTextInput.Instance);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
