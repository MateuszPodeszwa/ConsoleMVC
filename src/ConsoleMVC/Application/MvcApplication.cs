using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using ConsoleMVC.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The application host for a ConsoleMVC console application. Responsible for discovering
/// controllers and views at startup and running the main application loop.
/// </summary>
/// <remarks>
/// <para>
/// The application loop follows a continuous cycle of:
/// <list type="number">
///   <item><description>Create a service scope and resolve the controller and action for the current route.</description></item>
///   <item><description>Invoke the action method to obtain an <see cref="ActionResult"/>.</description></item>
///   <item><description>Process the result — either render a view or redirect to another action.</description></item>
///   <item><description>Repeat until a view returns <see cref="NavigationResult.Quit"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// Instances are created via the builder pattern using
/// <see cref="CreateBuilder(string[])"/> and <see cref="MvcApplicationBuilder.Build"/>,
/// mirroring ASP.NET Core's <c>WebApplication</c> approach.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var builder = MvcApplication.CreateBuilder(args);
/// builder.Services.AddSingleton&lt;IGreetingService, GreetingService&gt;();
/// using var app = builder.Build();
/// app.Run();
/// </code>
/// </example>
public class MvcApplication : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The maximum number of consecutive redirects (without rendering a view) before the
    /// application assumes a redirect loop and stops with an error.
    /// </summary>
    public const int MaxConsecutiveRedirects = 32;

    /// <summary>
    /// The most screens kept in the navigation history (<see cref="ConsoleViewBase.NavigationHistory"/>); older ones are
    /// forgotten.
    /// </summary>
    public const int MaxHistory = 50;

    private volatile Router _router = new();
    private readonly object _discoveryLock = new();
    private int _reloadRequested;
    private readonly Assembly? _applicationAssembly;
    private readonly ServiceProvider _services;
    private readonly List<Func<ActionDelegate, ActionDelegate>> _middleware = [];
    private ActionDelegate? _pipeline;
    private int _running;
    private readonly bool _clearScreenBetweenViews;
    private readonly bool _fullScreen;
    private readonly bool _mouse;
    private readonly bool _screenReader;
    private readonly MvcLocalizationOptions _localization;
    private readonly Assembly[] _modules;
    private readonly RouteContext _defaultRoute;

    /// <summary>Where local runs are; kept between runs, so running again continues from the last screen.</summary>
    private readonly NavigationState _local;

    /// <summary>The keyboard shortcuts mapped with <see cref="MapKey(KeyGesture, NavigationResult, string?)"/>.</summary>
    private readonly List<KeyShortcut> _shortcuts = [];

    /// <summary>
    /// Initialises a new instance of the <see cref="MvcApplication"/> class using
    /// the configuration specified in the given builder.
    /// </summary>
    /// <param name="builder">The builder containing the application configuration.</param>
    /// <param name="services">The root service provider built from <see cref="MvcApplicationBuilder.Services"/>.</param>
    internal MvcApplication(MvcApplicationBuilder builder, ServiceProvider services)
    {
        _defaultRoute = new RouteContext
        {
            Controller = builder.DefaultController,
            Action = builder.DefaultAction
        };
        _local = new NavigationState(_defaultRoute);
        _applicationAssembly = builder.ApplicationAssembly;
        _services = services;
        _clearScreenBetweenViews = builder.ClearScreenBetweenViews;
        _fullScreen = builder.FullScreen;
        _mouse = builder.UsesMouse;
        _screenReader = builder.UsesScreenReader;
        _localization = builder.Localization.Copy();
        _modules = builder.Modules.ToArray();
    }

    /// <summary>
    /// Gets the application's root service provider, built from <see cref="MvcApplicationBuilder.Services"/>.
    /// </summary>
    /// <remarks>
    /// Resolve singletons here, for example to seed data before <see cref="Run"/>. Scoped services can only be
    /// resolved inside a scope: <c>using var scope = app.Services.CreateScope();</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.Services.GetRequiredService&lt;IProductCatalogue&gt;().Load("products.json");
    /// app.Run();
    /// </code>
    /// </example>
    public IServiceProvider Services => _services;

    /// <summary>
    /// Gets how many times controllers and views have been discovered: once when the application starts, and again
    /// after each hot reload update (<c>dotnet watch</c>).
    /// </summary>
    internal int DiscoveryCount { get; private set; }

    /// <summary>
    /// Creates a new <see cref="MvcApplicationBuilder"/> for configuring and building
    /// an <see cref="MvcApplication"/> instance.
    /// </summary>
    /// <param name="args">The command-line arguments passed to the application.</param>
    /// <returns>A new <see cref="MvcApplicationBuilder"/> instance.</returns>
    /// <remarks>
    /// This mirrors ASP.NET Core's <c>WebApplication.CreateBuilder(args)</c> pattern.
    /// </remarks>
    public static MvcApplicationBuilder CreateBuilder(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new MvcApplicationBuilder(new MvcApplicationOptions { Args = args });
    }

    /// <summary>
    /// Creates a new <see cref="MvcApplicationBuilder"/> with the environment and content root chosen in code.
    /// </summary>
    /// <param name="options">The command-line arguments, environment name, content root and application name.</param>
    /// <returns>A new <see cref="MvcApplicationBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions { Args = args, EnvironmentName = "Development" });
    /// </code>
    /// </example>
    public static MvcApplicationBuilder CreateBuilder(MvcApplicationOptions options) => new(options);

    /// <summary>
    /// Gets the application's configuration (<see cref="MvcApplicationBuilder.Configuration"/>).
    /// </summary>
    /// <example><code>var title = app.Configuration["App:Title"];</code></example>
    public IConfiguration Configuration => _services.GetRequiredService<IConfiguration>();

    /// <summary>
    /// Gets the environment the application runs in (<see cref="MvcApplicationBuilder.Environment"/>).
    /// </summary>
    /// <example><code>if (app.Environment.IsDevelopment()) { /* seed test data */ }</code></example>
    public IHostEnvironment Environment => _services.GetRequiredService<IHostEnvironment>();

    /// <summary>Gets the keyboard shortcuts mapped so far.</summary>
    /// <example><code>foreach (var shortcut in app.Shortcuts) Console.WriteLine(shortcut.Gesture);</code></example>
    public IReadOnlyList<KeyShortcut> Shortcuts => _shortcuts;

    /// <summary>
    /// Maps an application-wide keyboard shortcut: pressing <paramref name="key"/> while any screen waits for input (a
    /// menu, a text field or an arrow-key field) leaves the screen and navigates to <paramref name="target"/>.
    /// </summary>
    /// <param name="key">A key that typing does not need: F1 to F24, Esc or Insert.</param>
    /// <param name="target"><c>Controller/Action</c> or <c>Action</c>, optionally with <c>?key=value</c> route values.</param>
    /// <param name="description">What it does, shown by <c>&lt;shortcuts /&gt;</c>; leave it out to keep the shortcut unlisted.</param>
    /// <returns>The application, for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the key is needed for typing, is already mapped, or the target is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application has already run.</exception>
    /// <example><code>app.MapKey(ConsoleKey.F1, "Help/Index", "Help");</code></example>
    public MvcApplication MapKey(ConsoleKey key, string target, string? description = null)
    {
        RouteValues.ParseTarget(target); // throws ArgumentException for an invalid target
        return MapKey(new KeyGesture(key), () => NavigationResult.To(target), description);
    }

    /// <summary>
    /// Maps an application-wide keyboard shortcut to a navigation, such as <see cref="NavigationResult.Back"/> or
    /// <see cref="NavigationResult.Quit"/>.
    /// </summary>
    /// <param name="gesture">The key, e.g. <c>new KeyGesture(ConsoleKey.Escape)</c> or <c>KeyGesture.Parse("Ctrl+X")</c>.</param>
    /// <param name="navigation">Where to go.</param>
    /// <param name="description">What it does, shown by <c>&lt;shortcuts /&gt;</c>.</param>
    /// <returns>The application, for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the key is needed for typing or is already mapped.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application has already run.</exception>
    /// <example>
    /// <code>
    /// app.MapKey(new KeyGesture(ConsoleKey.Escape), NavigationResult.Back(), "Back");
    /// app.MapKey(KeyGesture.Parse("Ctrl+X"), NavigationResult.Quit(), "Quit");
    /// </code>
    /// </example>
    public MvcApplication MapKey(KeyGesture gesture, NavigationResult navigation, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return MapKey(gesture, () => navigation, description);
    }

    /// <summary>
    /// Maps an application-wide keyboard shortcut whose navigation is decided when the key is pressed.
    /// </summary>
    /// <param name="gesture">The key.</param>
    /// <param name="navigate">Returns where to go.</param>
    /// <param name="description">What it does, shown by <c>&lt;shortcuts /&gt;</c>.</param>
    /// <returns>The application, for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the key is needed for typing or is already mapped.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application has already run.</exception>
    /// <example><code>app.MapKey(new KeyGesture(ConsoleKey.F5), () => NavigationResult.ToAction("Index"), "Refresh");</code></example>
    public MvcApplication MapKey(KeyGesture gesture, Func<NavigationResult> navigate, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(navigate);
        if (_pipeline is not null)
            throw new InvalidOperationException("Shortcuts must be mapped before the application first runs.");

        if (!IsShortcutKey(gesture))
        {
            throw new ArgumentException(
                $"{gesture} is needed for typing or for the input itself. Use F1 to F24, Esc, Insert, or a key with Ctrl or Alt " +
                "(except Ctrl+C, Ctrl+D, Ctrl+Z, and Ctrl+Q and Ctrl+S, which terminals use for flow control).", nameof(gesture));
        }

        if (_shortcuts.Any(s => s.Gesture == gesture))
            throw new ArgumentException($"{gesture} is already mapped.", nameof(gesture));

        _shortcuts.Add(new KeyShortcut(gesture, navigate, description));
        return this;
    }

    /// <summary>Keys a shortcut may use: ones that typing, editing and the arrow-key fields do not need.</summary>
    private static bool IsShortcutKey(KeyGesture gesture)
    {
        var withModifier = (gesture.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0;
        if (withModifier)
            // Ctrl+C interrupts, Ctrl+D and Ctrl+Z end the input, and terminals keep Ctrl+Q and Ctrl+S for flow control
            return !(gesture.Modifiers == ConsoleModifiers.Control
                     && gesture.Key is ConsoleKey.C or ConsoleKey.D or ConsoleKey.Z or ConsoleKey.Q or ConsoleKey.S);

        return gesture.Key is >= ConsoleKey.F1 and <= ConsoleKey.F24 or ConsoleKey.Escape or ConsoleKey.Insert;
    }

    /// <summary>
    /// Adds a middleware that runs around every action, in registration order (the first one added is the outermost).
    /// Call <paramref name="middleware"/>'s <c>next</c> argument to continue to the action, or set
    /// <see cref="ActionContext.Result"/> without calling it to skip the action.
    /// </summary>
    /// <param name="middleware">The middleware: receives the cycle's context and the rest of the pipeline.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="middleware"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application is already running.</exception>
    /// <example>
    /// <code>
    /// app.Use(async (context, next) =>
    /// {
    ///     try { await next(context); }
    ///     catch (IOException) { context.RedirectTo("Offline", "Errors"); }
    /// });
    /// </code>
    /// </example>
    public MvcApplication Use(Func<ActionContext, ActionDelegate, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        if (_pipeline is not null)
            throw new InvalidOperationException("Middleware must be added before the application first runs.");

        _middleware.Add(next => context => middleware(context, next));
        return this;
    }

    /// <summary>
    /// Adds a class-based middleware. A new <typeparamref name="TMiddleware"/> is created for every navigation cycle
    /// from the cycle's services (or with <see cref="ActivatorUtilities"/> when it is not registered), so its
    /// constructor may take scoped services.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the application is already running.</exception>
    /// <example><code>app.UseMiddleware&lt;RequireSignInMiddleware&gt;();</code></example>
    public MvcApplication UseMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : IActionMiddleware =>
        Use((context, next) =>
        {
            // A registered middleware belongs to the container; one created here is disposed with the cycle
            var middleware = context.Services.GetService<TMiddleware>();
            if (middleware is null)
                middleware = context.Own(ActivatorUtilities.CreateInstance<TMiddleware>(context.Services));

            return middleware.InvokeAsync(context, next);
        });

    /// <summary>
    /// Starts the application main loop and blocks until it ends. Equivalent to waiting for <see cref="RunAsync"/>.
    /// </summary>
    /// <remarks>
    /// The loop continues until a view returns <see cref="NavigationResult.Quit"/>,
    /// at which point the method returns and the application exits. Exceptions thrown by
    /// actions or views propagate unchanged (not wrapped in <see cref="TargetInvocationException"/>).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when routing fails, an action returns an unsupported result, a view receives an
    /// incompatible model, or a redirect loop is detected.
    /// </exception>
    /// <example><code>app.Run();</code></example>
    public void Run() => RunAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Starts the application main loop. The framework discovers all controllers and views
    /// in the application assembly, then begins the route-action-render cycle starting from the
    /// configured default route. Asynchronous actions are awaited.
    /// </summary>
    /// <param name="cancellationToken">
    /// Stops the loop before the next navigation cycle, and is passed to actions that declare a
    /// <see cref="CancellationToken"/> parameter.
    /// </param>
    /// <returns>A task that completes when a view returns <see cref="NavigationResult.Quit"/>.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when routing fails, an action returns an unsupported result, a view receives an
    /// incompatible model, a redirect loop is detected, or the application is already running.
    /// </exception>
    /// <example>
    /// <code>
    /// using var cancellation = new CancellationTokenSource();
    /// Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    /// await app.RunAsync(cancellation.Token);
    /// </code>
    /// </example>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The ConsoleMVC:Serve setting serves the application instead of running it here
        if (TerminalServerOptions.FromConfiguration(Configuration) is { } serve)
        {
            await ServeUntilStoppedAsync(serve, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (Interlocked.Exchange(ref _running, 1) == 1)
            throw new InvalidOperationException("The application is already running.");

        try
        {
            await WithCultureAsync(() => RunOnceAsync(Pipeline(), _local, null, null, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    /// <summary>
    /// Runs one session of the application on a remote terminal, from the default route until a view quits, the client
    /// disconnects or <paramref name="cancellationToken"/> is cancelled. Sessions run side by side, each with its own
    /// screen, history and <see cref="ITerminalSession"/>; controllers, views, middleware and services are shared.
    /// <see cref="TerminalServer"/> calls this for every client; call it yourself to serve another transport.
    /// </summary>
    /// <param name="terminal">The client's terminal.</param>
    /// <param name="cancellationToken">Ends the session; also passed to actions, together with the client disconnecting.</param>
    /// <returns>A task that completes when the session ends, including when the client disconnects.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="terminal"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <example>
    /// <code>
    /// await using var terminal = new RemoteTerminal(stream, new RemoteTerminalOptions { Columns = 100, Rows = 30 });
    /// await app.RunSessionAsync(terminal, cancellationToken);
    /// </code>
    /// </example>
    public Task RunSessionAsync(RemoteTerminal terminal, CancellationToken cancellationToken = default) =>
        RunSessionAsync(terminal, cancellationToken, started: null);

    /// <summary>Runs a session, telling <paramref name="started"/> which session it is once it has started.</summary>
    internal async Task RunSessionAsync(RemoteTerminal terminal, CancellationToken cancellationToken, Action<ITerminalSession>? started)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        cancellationToken.ThrowIfCancellationRequested();

        // Cancelling disconnects the client, so a session waiting for a key ends too
        using var disconnect = cancellationToken.Register(terminal.Disconnect);
        using var ended = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, terminal.Disconnected);
        try
        {
            // Views wait for keys synchronously: the session gets a thread of its own rather than blocking the caller
            // or a thread-pool thread
            await Task.Factory.StartNew(
                    () => WithCultureAsync(() => RunOnceAsync(Pipeline(), new NavigationState(_defaultRoute), terminal, started, ended.Token)),
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .Unwrap().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (terminal.Disconnected.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The client disconnected: the session is over
        }

        // Cancelling disconnected the client, which can end the session as if it had quit: report the cancellation
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Serves the application to remote terminals (<see cref="TerminalServer"/>) until <paramref name="cancellationToken"/>
    /// is cancelled, then disconnects every client and waits for their sessions to end. Setting <c>ConsoleMVC:Serve</c>
    /// makes <see cref="Run"/> and <see cref="RunAsync"/> do the same until Ctrl+C.
    /// </summary>
    /// <param name="options">Where to listen, the protocol, TLS and limits.</param>
    /// <param name="cancellationToken">Stops the server.</param>
    /// <returns>A task that completes when the server has stopped.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the options are not secure enough (see <see cref="TerminalServerOptions.AllowUnencryptedNetworkAccess"/>).</exception>
    /// <example><code>await app.ServeAsync(new TerminalServerOptions { EndPoint = new IPEndPoint(IPAddress.Loopback, 2323) }, token);</code></example>
    public async Task ServeAsync(TerminalServerOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using var server = new TerminalServer(this, options);
        server.Start();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopping is the normal end of serving
        }
        finally
        {
            await server.StopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Serves until the token is cancelled or the user presses Ctrl+C in the server's console.</summary>
    private async Task ServeUntilStoppedAsync(TerminalServerOptions options, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true; // stop gracefully: the sessions are told and the terminals put back
            stop.Cancel();
        };

        Console.CancelKeyPress += onCancel;
        try
        {
            await ServeAsync(options, stop.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    /// <summary>Runs <paramref name="run"/> in the chosen culture with the framework's messages, then puts the culture back.</summary>
    private async Task WithCultureAsync(Func<Task> run)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            if (ResolveCulture() is { } chosen)
            {
                CultureInfo.CurrentCulture = chosen;
                CultureInfo.CurrentUICulture = chosen;
            }

            using var text = FrameworkText.Use(ResolveFrameworkText());
            await run().ConfigureAwait(false);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    /// <summary>The middleware pipeline, built once when the application first runs (by whichever session comes first).</summary>
    private ActionDelegate Pipeline()
    {
        if (_pipeline is { } built)
            return built;

        Interlocked.CompareExchange(ref _pipeline, ActionPipeline.Build(_middleware, InvokeActionAsync), null);
        return _pipeline;
    }

    /// <summary>Where a run or a session is: the screen to show next and the screens shown, oldest first.</summary>
    private sealed class NavigationState(RouteContext start)
    {
        /// <summary>The route of the next navigation cycle; the screen on show while it waits for input.</summary>
        public RouteContext Current { get; set; } = start;

        /// <summary>The screens shown, oldest first; the last one is on screen now.</summary>
        public List<RouteContext> History { get; } = [];
    }

    /// <summary>The culture chosen in code, or else in the <see cref="MvcLocalizationOptions.CultureSetting"/> setting.</summary>
    private CultureInfo? ResolveCulture()
    {
        if (_localization.Culture is { } culture)
            return culture;

        var name = Configuration[MvcLocalizationOptions.CultureSetting];
        if (string.IsNullOrWhiteSpace(name))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(name.Trim(), predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            throw new InvalidOperationException(
                $"The {MvcLocalizationOptions.CultureSetting} setting '{name}' is not a known culture; use a name such as 'en-GB' or 'pl-PL'.");
        }
    }

    /// <summary>The framework's messages, translated from <see cref="MvcLocalizationOptions.FrameworkTextResource"/> when set.</summary>
    private FrameworkText ResolveFrameworkText() =>
        _localization.FrameworkTextResource is { } resource
            ? FrameworkText.FromLocalizer(_services.GetRequiredService<IStringLocalizerFactory>().Create(resource), _localization.Text)
            : _localization.Text;

    /// <summary>Returns the localiser for validation messages of <paramref name="type"/>, when they are translated.</summary>
    private IStringLocalizer ValidationLocalizer(Type type) =>
        _services.GetRequiredService<IStringLocalizerFactory>().Create(_localization.ValidationResource ?? type);

    /// <summary>
    /// Discovers controllers and views, then runs navigation cycles until a view quits: on the console, or on
    /// <paramref name="terminal"/> for a remote session.
    /// </summary>
    private async Task RunOnceAsync(ActionDelegate pipeline, NavigationState state, RemoteTerminal? terminal,
        Action<ITerminalSession>? started, CancellationToken cancellationToken)
    {
        var assembly = ResolveApplicationAssembly();
        IMarkupRenderer renderer;
        ITextInput input;
        TextWriter echo;
        TerminalSession session;
        if (terminal is null)
        {
            Discover(assembly);
            renderer = _services.GetRequiredService<IMarkupRenderer>();
            input = _services.GetRequiredService<ITextInput>();
            echo = input is ConsoleTextInput && !Console.IsOutputRedirected ? Console.Out : TextWriter.Null;
            session = TerminalSession.ForLocalRun();
        }
        else
        {
            // Sessions share what the first one discovered; a hot reload still rediscovers
            EnsureDiscovered(assembly);
            renderer = _screenReader ? terminal.CreateScreenReaderRenderer() : terminal.Renderer;
            input = terminal.Input;
            echo = terminal.Output;
            session = TerminalSession.ForRemote(terminal, new LiveDisplay(renderer));
            terminal.Start();
        }

        using var current = TerminalSession.Use(session);
        started?.Invoke(session);

        // Innermost: clicks become keys before shortcuts and line editing see them
        if (_mouse)
            input = new MouseDecoder(input, renderer);

        // Lines are then read key by key so shortcuts and clicks work in menus and text fields; the keys are echoed
        if (_shortcuts.Count > 0)
            input = new ShortcutInput(input, _shortcuts.ToArray(), echo);
        else if (_mouse)
            input = new LineEditingInput(input, echo);

        // Outermost, so a refreshing screen notices the time passing before any other input reads a key
        var refreshing = new RefreshingInput(input);
        var theme = _services.GetRequiredService<Theme>();
        var logger = _services.GetRequiredService<ILogger<MvcApplication>>();
        MvcApplicationLog.Starting(logger, state.Current, Environment.EnvironmentName);

        HotReloadHandler.Updated += OnHotReload;
        using var screen = _fullScreen ? TerminalModeSession.FullScreen(renderer) : null;
        using var mouse = _mouse ? TerminalModeSession.Mouse(renderer) : null;
        try
        {
            await RunLoopAsync(pipeline, assembly, state, renderer, refreshing, theme, logger, cancellationToken).ConfigureAwait(false);
            MvcApplicationLog.Stopped(logger);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Logged where it happened, then rethrown unchanged for the caller (and the terminal) to see
            MvcApplicationLog.UnhandledException(logger, e, state.Current);
            throw;
        }
        finally
        {
            HotReloadHandler.Updated -= OnHotReload;
        }
    }

    /// <summary>Marks the router for rediscovery; called on a hot reload thread.</summary>
    private void OnHotReload() => Interlocked.Exchange(ref _reloadRequested, 1);

    /// <summary>Discovers controllers and views into a fresh router, so stale cached actions are dropped.</summary>
    private void Discover(Assembly assembly)
    {
        lock (_discoveryLock)
        {
            // The generated route tables describe the assemblies as built; after a hot reload they may be out of date
            var useRouteTables = DiscoveryCount == 0;
            var router = new Router();
            router.DiscoverAll(assembly, useRouteTables);
            foreach (var module in _modules.Where(m => m != assembly))
                router.DiscoverAll(module, useRouteTables);
            _router = router;
            DiscoveryCount++;
        }
    }

    /// <summary>Discovers controllers and views unless that has been done already.</summary>
    private void EnsureDiscovered(Assembly assembly)
    {
        lock (_discoveryLock)
        {
            if (DiscoveryCount == 0)
                Discover(assembly);
        }
    }

    /// <summary>
    /// Runs navigation cycles until a view quits: the pipeline, then rendering the view or following the redirect.
    /// </summary>
    private async Task RunLoopAsync(ActionDelegate pipeline, Assembly assembly, NavigationState state, IMarkupRenderer renderer, RefreshingInput input,
        Theme theme, ILogger logger, CancellationToken cancellationToken)
    {
        var consecutiveRedirects = 0;
        var refreshing = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // dotnet watch applied an edit: pick up new or changed controllers and views from this screen on
            if (Interlocked.Exchange(ref _reloadRequested, 0) == 1)
                Discover(assembly);

            MvcApplicationLog.Running(logger, state.Current);

            // One scope per navigation cycle: the action and the view it renders share scoped services
            await using var scope = _services.CreateAsyncScope();
            var context = new ActionContext(state.Current, scope.ServiceProvider, cancellationToken);

            try
            {
                await pipeline(context).ConfigureAwait(false);

                var result = context.Result;
                switch (result)
                {
                    case ViewResult viewResult:
                        consecutiveRedirects = 0;
                        RecordScreen(state, context);

                        NavigationResult navigation;
                        try
                        {
                            navigation = RenderView(viewResult, state, renderer, input, theme, context, clear: refreshing);
                            refreshing = false;
                        }
                        catch (ShortcutPressedException shortcut)
                        {
                            navigation = shortcut.Navigation;
                            refreshing = false;
                        }
                        catch (ViewRefreshException)
                        {
                            // Nothing typed in time: run the same route again for fresh data, on a cleared screen
                            refreshing = true;
                            state.Current = context.Route;
                            break;
                        }
                        finally
                        {
                            input.Disarm();
                        }

                        if (navigation.Exit)
                            return;

                        state.Current = navigation.IsBack
                            ? PreviousScreen(state)
                            : new RouteContext
                            {
                                // Inside an area, a controller named without one is looked for in the area first
                                Controller = navigation.Controller is { } named ? _router.Qualify(named, context.Route.Controller) : context.Route.Controller,
                                Action = navigation.Action ?? context.Route.Action,
                                FormData = navigation.FormData,
                                RouteValues = navigation.RouteValues
                            };
                        break;

                    case RedirectToActionResult redirect:
                        if (++consecutiveRedirects > MaxConsecutiveRedirects)
                        {
                            throw new InvalidOperationException(
                                $"More than {MaxConsecutiveRedirects} consecutive redirects without rendering a view " +
                                $"(last: '{context.Route}' → '{redirect.ControllerName}/{redirect.ActionName}'). " +
                                "This usually means an action redirects to itself.");
                        }

                        state.Current = new RouteContext
                        {
                            Controller = _router.Qualify(redirect.ControllerName, context.Route.Controller),
                            Action = redirect.ActionName,
                            RouteValues = redirect.RouteValues
                        };
                        break;

                    case null when context.Controller is null:
                        throw new InvalidOperationException(
                            $"No result was produced for '{context.Route}': a middleware neither called next(context) " +
                            "nor set context.Result.");

                    default:
                        throw new InvalidOperationException(
                            $"Action '{context.Route}' returned " +
                            $"{(result is null ? "null" : $"an unsupported result of type '{result.GetType().Name}'")}. " +
                            "Return View(...) or RedirectToAction(...).");
                }
            }
            finally
            {
                // Controllers, views and middleware created for this cycle; the scope disposes its own services next
                await context.DisposeOwnedAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Returns the configured application assembly, or the entry assembly.
    /// </summary>
    private Assembly ResolveApplicationAssembly() =>
        _applicationAssembly
        ?? Assembly.GetEntryAssembly()
        ?? throw new InvalidOperationException(
            "Could not determine the application assembly. Set MvcApplicationBuilder.ApplicationAssembly, " +
            "e.g. builder.ApplicationAssembly = typeof(Program).Assembly;");

    /// <summary>
    /// The end of the pipeline: resolves the controller and action for the context's route, binds and validates
    /// form data, invokes the action (awaiting it when asynchronous) and stores its result in the context.
    /// </summary>
    private async Task InvokeActionAsync(ActionContext context)
    {
        var route = context.Route;
        var controller = context.Own(_router.ResolveController(route.Controller, context.Services));
        context.Controller = controller;
        var actionMethod = _router.ResolveAction(controller.GetType(), route.Action);

        // Normalise the route to the declared action name so View() resolves the right view
        // regardless of the casing used in navigation.
        controller.RouteContext = new RouteContext
        {
            Controller = route.Controller,
            Action = actionMethod.Name,
            FormData = route.FormData,
            RouteValues = route.RouteValues
        };

        // Bind posted values, then validate them before the action runs
        var modelState = new ModelStateDictionary();
        var args = ModelBinder.BindParameters(actionMethod, route.FormData, route.RouteValues, modelState);
        if (route.FormData is not null)
            ModelValidator.Validate(actionMethod, args, modelState, _localization.LocalizeValidation ? ValidationLocalizer : null);

        SupplyCancellationTokens(actionMethod, args, context.Cancellation);
        controller.ModelState = modelState;
        controller.ValidationLocalizer = _localization.LocalizeValidation ? ValidationLocalizer : null;
        context.ModelState = modelState;

        // A generated route table calls the action directly; otherwise it is called by reflection
        var invoker = _router.InvokerFor(actionMethod);
        context.Result = invoker is not null
            ? await invoker(controller, args).ConfigureAwait(false)
            : await ActionInvoker.InvokeAsync(actionMethod, controller, args).ConfigureAwait(false);
    }

    /// <summary>Passes the run's token to every <see cref="CancellationToken"/> parameter of the action.</summary>
    private static void SupplyCancellationTokens(MethodInfo method, object?[]? args, CancellationToken token)
    {
        if (args is null)
            return;

        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken))
                args[i] = token;
        }
    }

    /// <summary>
    /// Adds the screen about to be shown to the history. Showing the same screen again (a form after validation failed,
    /// or the screen reached with Back) replaces the last entry instead.
    /// </summary>
    private static void RecordScreen(NavigationState state, ActionContext context)
    {
        // The controller's route has the action's declared name, whatever casing navigation used
        var route = context.Controller?.RouteContext ?? context.Route;
        var screen = new RouteContext { Controller = route.Controller, Action = route.Action, RouteValues = route.RouteValues };

        var history = state.History;
        if (history.Count > 0 && SameScreen(history[^1], screen))
            history[^1] = screen;
        else
            history.Add(screen);

        if (history.Count > MaxHistory)
            history.RemoveAt(0);
    }

    /// <summary>
    /// Leaves the current screen for the one before it, which stays in the history as the current screen. On the first
    /// screen, the current screen is shown again.
    /// </summary>
    private static RouteContext PreviousScreen(NavigationState state)
    {
        var history = state.History;
        if (history.Count > 1)
            history.RemoveAt(history.Count - 1);

        var previous = history[^1];
        return new RouteContext { Controller = previous.Controller, Action = previous.Action, RouteValues = previous.RouteValues };
    }

    private static bool SameScreen(RouteContext a, RouteContext b) =>
        string.Equals(a.Controller, b.Controller, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Action, b.Action, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves and renders the view for <paramref name="viewResult"/>.
    /// </summary>
    private NavigationResult RenderView(ViewResult viewResult, NavigationState state, IMarkupRenderer renderer, RefreshingInput input, Theme theme,
        ActionContext context, bool clear)
    {
        var view = context.Own(_router.ResolveView(viewResult.ControllerName, viewResult.ViewName, context.Services));
        view.ViewData = viewResult.ViewData;
        view.ModelState = viewResult.ModelState;
        view.Renderer = renderer;
        view.Input = input;
        view.Theme = theme;
        view.NavigationHistory = state.History.ToArray();
        view.ShortcutHints = _shortcuts.Where(s => !string.IsNullOrWhiteSpace(s.Description))
            .Select(s => new ShortcutHint(s.Gesture.ToString(), s.Description!)).ToArray();

        // A refresh always clears, or the screen would be drawn again below itself
        if (_clearScreenBetweenViews || clear)
            renderer.Clear();

        input.Arm(viewResult.ResolveRefresh(view.DeclaredRefreshInterval));
        return view.RenderInternal(viewResult.Model)
               ?? throw new InvalidOperationException(
                   $"View '{view.GetType().Name}' returned null. Views must return a NavigationResult.");
    }

    /// <summary>
    /// Disposes the root service provider and every singleton service it created. Prefer <see cref="DisposeAsync"/>
    /// (<c>await using var app = builder.Build();</c>) when services implement only <see cref="IAsyncDisposable"/>;
    /// this method still disposes them, synchronously.
    /// </summary>
    /// <example><code>using var app = builder.Build();</code></example>
    public void Dispose()
    {
        // ServiceProvider.Dispose refuses services that implement only IAsyncDisposable; DisposeAsync handles both
        // kinds and completes synchronously when no service needs to wait
        _services.DisposeAsync().AsTask().GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the root service provider and every singleton service it created, asynchronously.
    /// </summary>
    /// <returns>A task that completes when everything is disposed.</returns>
    /// <example><code>await using var app = builder.Build();</code></example>
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
