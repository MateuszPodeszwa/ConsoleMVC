using System.Reflection;

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
public class MvcApplication
{
    private readonly Router _router = new();
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
    /// in the entry assembly, then begins the route-action-render cycle starting from the
    /// configured default route.
    /// </summary>
    /// <remarks>
    /// The loop continues until a view returns <see cref="NavigationResult.Quit"/>,
    /// at which point the method returns and the application exits.
    /// </remarks>
    public void Run()
    {
        _router.DiscoverAll(Assembly.GetEntryAssembly()!);

        while (true)
        {
            // 1. Resolve the controller and action
            var controller = _router.ResolveController(_currentRoute.Controller);
            controller.RouteContext = _currentRoute;

            var actionMethod = _router.ResolveAction(controller.GetType(), _currentRoute.Action);

            // 2. Invoke the action, binding form data to parameters if present
            var args = ModelBinder.BindParameters(actionMethod, _currentRoute.FormData);
            var result = (ActionResult)actionMethod.Invoke(controller, args)!;

            // 3. Process the result
            if (result is ViewResult viewResult)
            {
                var view = _router.ResolveView(viewResult.ControllerName, viewResult.ViewName);
                view.ViewData = viewResult.ViewData;

                Console.Clear();
                var navigation = view.RenderInternal(viewResult.Model);

                if (navigation.Exit)
                    break;

                _currentRoute = new RouteContext
                {
                    Controller = navigation.Controller ?? _currentRoute.Controller,
                    Action = navigation.Action ?? _currentRoute.Action,
                    FormData = navigation.FormData
                };
            }
            else if (result is RedirectToActionResult redirect)
            {
                _currentRoute = new RouteContext
                {
                    Controller = redirect.ControllerName,
                    Action = redirect.ActionName
                };
            }
        }
    }
}
