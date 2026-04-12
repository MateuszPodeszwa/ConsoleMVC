using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The application host. Discovers controllers and views at startup,
/// then runs the main loop: route → action → render → navigate.
/// </summary>
public class MvcApplication
{
    private readonly Router _router = new();
    private RouteContext _currentRoute;

    internal MvcApplication(MvcApplicationBuilder builder)
    {
        _currentRoute = new RouteContext
        {
            Controller = builder.DefaultController,
            Action = builder.DefaultAction
        };
    }

    /// <summary>
    /// Create a new application builder, mirroring ASP.NET Core's WebApplication.CreateBuilder().
    /// </summary>
    public static MvcApplicationBuilder CreateBuilder(string[] args)
    {
        return new MvcApplicationBuilder();
    }

    /// <summary>
    /// Start the application main loop.
    /// </summary>
    public void Run()
    {
        _router.DiscoverAll(Assembly.GetEntryAssembly()!);

        while (true)
        {
            // 1. Resolve the controller and action
            var controller = _router.ResolveController(_currentRoute.Controller);
            controller.RouteContext = _currentRoute;

            var actionMethod = _router.ResolveAction(controller.GetType(), _currentRoute.Action);

            // 2. Invoke the action
            var result = (ActionResult)actionMethod.Invoke(controller, null)!;

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
                    Action = navigation.Action ?? _currentRoute.Action
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
