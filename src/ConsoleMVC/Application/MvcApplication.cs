using System.Reflection;
using System.Runtime.ExceptionServices;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The application host for a ConsoleMVC console application. Responsible for discovering
/// controllers and views at startup and running the main application loop.
/// </summary>
/// <remarks>
/// <para>
/// The application loop follows a continuous cycle of:
/// <list type="number">
///   <item><description>Resolve the controller and action for the current route.</description></item>
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
/// var app = builder.Build();
/// app.Run();
/// </code>
/// </example>
public class MvcApplication
{
    /// <summary>
    /// The maximum number of consecutive redirects (without rendering a view) before the
    /// application assumes a redirect loop and stops with an error.
    /// </summary>
    public const int MaxConsecutiveRedirects = 32;

    private readonly Router _router = new();
    private readonly Assembly? _applicationAssembly;
    private readonly IMarkupRenderer? _configuredRenderer;
    private readonly ITextInput _input;
    private readonly bool _clearScreenBetweenViews;
    private RouteContext _currentRoute;

    /// <summary>
    /// Initialises a new instance of the <see cref="MvcApplication"/> class using
    /// the configuration specified in the given builder.
    /// </summary>
    /// <param name="builder">The builder containing the application configuration.</param>
    internal MvcApplication(MvcApplicationBuilder builder)
    {
        _currentRoute = new RouteContext
        {
            Controller = builder.DefaultController,
            Action = builder.DefaultAction
        };
        _applicationAssembly = builder.ApplicationAssembly;
        _configuredRenderer = builder.Renderer;
        _input = builder.Input ?? ConsoleTextInput.Instance;
        _clearScreenBetweenViews = builder.ClearScreenBetweenViews;
    }

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
    /// Starts the application main loop. The framework discovers all controllers and views
    /// in the application assembly, then begins the route-action-render cycle starting from the
    /// configured default route.
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
    public void Run()
    {
        _router.DiscoverAll(ResolveApplicationAssembly());
        var renderer = _configuredRenderer ?? MarkupRenderer.CreateForConsole();
        var consecutiveRedirects = 0;

        while (true)
        {
            var result = InvokeCurrentAction();

            switch (result)
            {
                case ViewResult viewResult:
                    consecutiveRedirects = 0;

                    var navigation = RenderView(viewResult, renderer);
                    if (navigation.Exit)
                        return;

                    _currentRoute = new RouteContext
                    {
                        Controller = navigation.Controller ?? _currentRoute.Controller,
                        Action = navigation.Action ?? _currentRoute.Action,
                        FormData = navigation.FormData
                    };
                    break;

                case RedirectToActionResult redirect:
                    if (++consecutiveRedirects > MaxConsecutiveRedirects)
                    {
                        throw new InvalidOperationException(
                            $"More than {MaxConsecutiveRedirects} consecutive redirects without rendering a view " +
                            $"(last: '{_currentRoute.Controller}/{_currentRoute.Action}' → '{redirect.ControllerName}/{redirect.ActionName}'). " +
                            "This usually means an action redirects to itself.");
                    }

                    _currentRoute = new RouteContext
                    {
                        Controller = redirect.ControllerName,
                        Action = redirect.ActionName
                    };
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Action '{_currentRoute.Controller}/{_currentRoute.Action}' returned " +
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
    /// Resolves the controller and action for the current route, binds form data and invokes the action.
    /// </summary>
    private ActionResult? InvokeCurrentAction()
    {
        var controller = _router.ResolveController(_currentRoute.Controller);
        var actionMethod = _router.ResolveAction(controller.GetType(), _currentRoute.Action);

        // Normalise the route to the declared action name so View() resolves the right view
        // regardless of the casing used in navigation.
        controller.RouteContext = new RouteContext
        {
            Controller = _currentRoute.Controller,
            Action = actionMethod.Name,
            FormData = _currentRoute.FormData
        };

        // Bind posted values, then validate them before the action runs
        var modelState = new ModelStateDictionary();
        var args = ModelBinder.BindParameters(actionMethod, _currentRoute.FormData, modelState);
        if (_currentRoute.FormData is not null)
            ModelValidator.Validate(actionMethod, args, modelState);

        controller.ModelState = modelState;

        try
        {
            return (ActionResult?)actionMethod.Invoke(controller, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            // Surface the action's own exception with its original stack trace
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw; // unreachable
        }
    }

    /// <summary>
    /// Resolves and renders the view for <paramref name="viewResult"/>.
    /// </summary>
    private NavigationResult RenderView(ViewResult viewResult, IMarkupRenderer renderer)
    {
        var view = _router.ResolveView(viewResult.ControllerName, viewResult.ViewName);
        view.ViewData = viewResult.ViewData;
        view.ModelState = viewResult.ModelState;
        view.Renderer = renderer;
        view.Input = _input;

        if (_clearScreenBetweenViews)
            renderer.Clear();

        return view.RenderInternal(viewResult.Model)
               ?? throw new InvalidOperationException(
                   $"View '{view.GetType().Name}' returned null. Views must return a NavigationResult.");
    }
}
