using System.Reflection;
using ConsoleMVC.Rendering;
using Microsoft.Extensions.DependencyInjection;

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
/// <see cref="CreateBuilder"/> and <see cref="MvcApplicationBuilder.Build"/>,
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
public class MvcApplication : IDisposable
{
    /// <summary>
    /// The maximum number of consecutive redirects (without rendering a view) before the
    /// application assumes a redirect loop and stops with an error.
    /// </summary>
    public const int MaxConsecutiveRedirects = 32;

    private readonly Router _router = new();
    private readonly Assembly? _applicationAssembly;
    private readonly ServiceProvider _services;
    private readonly List<Func<ActionDelegate, ActionDelegate>> _middleware = [];
    private ActionDelegate? _pipeline;
    private readonly bool _clearScreenBetweenViews;
    private RouteContext _currentRoute;

    /// <summary>
    /// Initialises a new instance of the <see cref="MvcApplication"/> class using
    /// the configuration specified in the given builder.
    /// </summary>
    /// <param name="builder">The builder containing the application configuration.</param>
    /// <param name="services">The root service provider built from <see cref="MvcApplicationBuilder.Services"/>.</param>
    internal MvcApplication(MvcApplicationBuilder builder, ServiceProvider services)
    {
        _currentRoute = new RouteContext
        {
            Controller = builder.DefaultController,
            Action = builder.DefaultAction
        };
        _applicationAssembly = builder.ApplicationAssembly;
        _services = services;
        _clearScreenBetweenViews = builder.ClearScreenBetweenViews;
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
        return new MvcApplicationBuilder();
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
            throw new InvalidOperationException("Middleware must be added before the application starts running.");

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
    public MvcApplication UseMiddleware<TMiddleware>() where TMiddleware : IActionMiddleware =>
        Use((context, next) => ActivatorUtilities
            .GetServiceOrCreateInstance<TMiddleware>(context.Services)
            .InvokeAsync(context, next));

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
        if (_pipeline is not null)
            throw new InvalidOperationException("The application is already running.");

        _pipeline = ActionPipeline.Build(_middleware, InvokeActionAsync);
        cancellationToken.ThrowIfCancellationRequested();

        _router.DiscoverAll(ResolveApplicationAssembly());
        var renderer = _services.GetRequiredService<IMarkupRenderer>();
        var input = _services.GetRequiredService<ITextInput>();
        var consecutiveRedirects = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // One scope per navigation cycle: the action and the view it renders share scoped services
            await using var scope = _services.CreateAsyncScope();
            using var cycle = new CycleInstances();

            var context = new ActionContext(_currentRoute, scope.ServiceProvider, cancellationToken);
            try
            {
                await _pipeline(context).ConfigureAwait(false);
            }
            finally
            {
                cycle.Track(context.Controller);
            }

            var result = context.Result;
            switch (result)
            {
                case ViewResult viewResult:
                    consecutiveRedirects = 0;

                    var navigation = RenderView(viewResult, renderer, input, scope.ServiceProvider, cycle);
                    if (navigation.Exit)
                        return;

                    _currentRoute = new RouteContext
                    {
                        Controller = navigation.Controller ?? context.Route.Controller,
                        Action = navigation.Action ?? context.Route.Action,
                        FormData = navigation.FormData
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

                    _currentRoute = new RouteContext
                    {
                        Controller = redirect.ControllerName,
                        Action = redirect.ActionName
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
        var controller = _router.ResolveController(route.Controller, context.Services);
        context.Controller = controller;
        var actionMethod = _router.ResolveAction(controller.GetType(), route.Action);

        // Normalise the route to the declared action name so View() resolves the right view
        // regardless of the casing used in navigation.
        controller.RouteContext = new RouteContext
        {
            Controller = route.Controller,
            Action = actionMethod.Name,
            FormData = route.FormData
        };

        // Bind posted values, then validate them before the action runs
        var modelState = new ModelStateDictionary();
        var args = ModelBinder.BindParameters(actionMethod, route.FormData, modelState);
        if (route.FormData is not null)
            ModelValidator.Validate(actionMethod, args, modelState);

        SupplyCancellationTokens(actionMethod, args, context.Cancellation);
        controller.ModelState = modelState;
        context.ModelState = modelState;

        context.Result = await ActionInvoker.InvokeAsync(actionMethod, controller, args).ConfigureAwait(false);
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
    /// Resolves and renders the view for <paramref name="viewResult"/>.
    /// </summary>
    private NavigationResult RenderView(ViewResult viewResult, IMarkupRenderer renderer, ITextInput input,
        IServiceProvider services, CycleInstances cycle)
    {
        var view = cycle.Track(_router.ResolveView(viewResult.ControllerName, viewResult.ViewName, services));
        view.ViewData = viewResult.ViewData;
        view.ModelState = viewResult.ModelState;
        view.Renderer = renderer;
        view.Input = input;

        if (_clearScreenBetweenViews)
            renderer.Clear();

        return view.RenderInternal(viewResult.Model)
               ?? throw new InvalidOperationException(
                   $"View '{view.GetType().Name}' returned null. Views must return a NavigationResult.");
    }

    /// <summary>
    /// Disposes the root service provider and every singleton service it created.
    /// </summary>
    /// <example><code>using var app = builder.Build();</code></example>
    public void Dispose()
    {
        _services.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The controller and view created in one navigation cycle. They are created with
    /// <see cref="ActivatorUtilities"/>, so the scope does not own them; this disposes them at the end of the cycle.
    /// </summary>
    private sealed class CycleInstances : IDisposable
    {
        private readonly List<IDisposable> _disposables = [];

        /// <summary>Remembers <paramref name="instance"/> for disposal when it is <see cref="IDisposable"/>.</summary>
        public T Track<T>(T instance)
        {
            if (instance is null)
                return instance;

            if (instance is IDisposable disposable)
                _disposables.Add(disposable);

            return instance;
        }

        /// <summary>Disposes the tracked instances, most recent first.</summary>
        public void Dispose()
        {
            for (var i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Dispose();
        }
    }
}
