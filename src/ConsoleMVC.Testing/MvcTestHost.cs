using System.Reflection;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Testing;

/// <summary>
/// Runs a ConsoleMVC application in a test: the screens are recorded as plain text at a fixed width
/// (<see cref="Width"/>), the user's input comes from a <see cref="ScriptedInput"/>, and every route is recorded.
/// Each <see cref="Run(ScriptedInput)"/> builds a fresh application, so runs do not share state.
/// </summary>
/// <remarks>
/// Services are not taken from the application's <c>Program.cs</c>: register what the screens under test need in the
/// <c>configure</c> callback, with fakes where you want them. The environment is <see cref="EnvironmentName"/>
/// (<c>Testing</c>), so <c>appsettings.Testing.json</c> is read if it is copied next to the tests.
/// </remarks>
/// <example>
/// <code>
/// var host = MvcTestHost.For&lt;HomeController&gt;(builder =>
///     builder.Services.AddSingleton&lt;IGreetingService, FakeGreetings&gt;());
///
/// var run = host.Run("2", "Ada", "q");
/// Assert.Contains("Hello, Ada", run.Output);
/// </code>
/// </example>
public sealed class MvcTestHost
{
    /// <summary>The default value of <see cref="Width"/>.</summary>
    public const int DefaultWidth = 80;

    /// <summary>The default value of <see cref="MaxScreens"/>.</summary>
    public const int DefaultMaxScreens = 100;

    /// <summary>The default value of <see cref="EnvironmentName"/>.</summary>
    public const string DefaultEnvironmentName = "Testing";

    private readonly Assembly _assembly;
    private readonly Action<MvcApplicationBuilder>? _configure;

    private MvcTestHost(Assembly assembly, Action<MvcApplicationBuilder>? configure)
    {
        _assembly = assembly;
        _configure = configure;
    }

    /// <summary>Gets or sets the width, in columns, that screens are laid out to.</summary>
    /// <example><code>host.Width = 40; // test a narrow terminal</code></example>
    public int Width { get; set; } = DefaultWidth;

    /// <summary>
    /// Gets or sets how many navigation cycles a run may have. More means the application keeps navigating without
    /// waiting for input, and the run fails instead of hanging.
    /// </summary>
    /// <example><code>host.MaxScreens = 500;</code></example>
    public int MaxScreens { get; set; } = DefaultMaxScreens;

    /// <summary>Gets or sets the environment name the application runs in.</summary>
    /// <example><code>host.EnvironmentName = "Development";</code></example>
    public string EnvironmentName { get; set; } = DefaultEnvironmentName;

    /// <summary>
    /// Creates a host for the application that contains <typeparamref name="TMarker"/>, usually one of its controllers.
    /// </summary>
    /// <typeparam name="TMarker">Any type of the application assembly.</typeparam>
    /// <param name="configure">Configures the builder: services, the default route, the theme…</param>
    /// <returns>The host.</returns>
    /// <example><code>var host = MvcTestHost.For&lt;HomeController&gt;();</code></example>
    public static MvcTestHost For<TMarker>(Action<MvcApplicationBuilder>? configure = null) =>
        For(typeof(TMarker).Assembly, configure);

    /// <summary>
    /// Creates a host for the application in <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly with the controllers and views.</param>
    /// <param name="configure">Configures the builder: services, the default route, the theme…</param>
    /// <returns>The host.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is <see langword="null"/>.</exception>
    /// <example><code>var host = MvcTestHost.For(typeof(Program).Assembly);</code></example>
    public static MvcTestHost For(Assembly assembly, Action<MvcApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return new MvcTestHost(assembly, configure);
    }

    /// <summary>
    /// Runs the application with typed lines until a view quits or the lines run out.
    /// </summary>
    /// <param name="lines">The lines the user types, in order.</param>
    /// <returns>The screens and routes.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the run exceeds <see cref="MaxScreens"/>.</exception>
    /// <example><code>var run = host.Run("1", "Ada", "q");</code></example>
    public TestRun Run(params string[] lines) => Run(new ScriptedInput(lines));

    /// <summary>
    /// Runs the application with a script of lines and keys until a view quits or the script runs out.
    /// </summary>
    /// <param name="input">The script.</param>
    /// <returns>The screens and routes.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the run exceeds <see cref="MaxScreens"/>.</exception>
    /// <example><code>var run = host.Run(new ScriptedInput("1").Keys(ConsoleKey.DownArrow, ConsoleKey.Enter));</code></example>
    public TestRun Run(ScriptedInput input) => RunAsync(input).GetAwaiter().GetResult();

    /// <summary>
    /// Runs the application asynchronously with a script of lines and keys.
    /// </summary>
    /// <param name="input">The script.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The screens and routes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the run exceeds <see cref="MaxScreens"/>.</exception>
    /// <example><code>var run = await host.RunAsync(new ScriptedInput("q"));</code></example>
    public async Task<TestRun> RunAsync(ScriptedInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var recorder = new ScreenRecorder();
        var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions
        {
            EnvironmentName = EnvironmentName,
            ApplicationName = _assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.ApplicationAssembly = _assembly;
        builder.Renderer = new MarkupRenderer(recorder, () => Width);
        builder.Input = input;
        _configure?.Invoke(builder);

        var routes = new List<string>();
        var limit = MaxScreens;

        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            routes.Add(context.Route.ToString());
            if (routes.Count > limit)
            {
                throw new InvalidOperationException(
                    $"The application ran more than {limit} screens without finishing (last: {context.Route}). It probably " +
                    "navigates without waiting for input; raise MvcTestHost.MaxScreens if the run is meant to be this long.");
            }

            await next(context).ConfigureAwait(false);
        });

        await app.RunAsync(cancellationToken).ConfigureAwait(false);
        return new TestRun(recorder.Screens, routes, input.Remaining);
    }
}
