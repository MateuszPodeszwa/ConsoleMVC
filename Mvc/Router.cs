using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Convention-based router that discovers controllers and views via reflection.
/// Controllers are matched by name suffix "Controller".
/// Views are matched by namespace convention: ConsoleMVC.Views.{Controller}.{Action}View.
/// </summary>
public class Router
{
    private readonly Dictionary<string, Type> _controllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Controller, string Action), Type> _views = new();

    /// <summary>
    /// Scan the given assembly for all controllers and views.
    /// </summary>
    public void DiscoverAll(Assembly assembly)
    {
        DiscoverControllers(assembly);
        DiscoverViews(assembly);
    }

    private void DiscoverControllers(Assembly assembly)
    {
        var controllerBaseType = typeof(Controller);

        var controllerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true }
                        && controllerBaseType.IsAssignableFrom(t)
                        && t.Name.EndsWith("Controller"));

        foreach (var type in controllerTypes)
        {
            var name = type.Name[..^"Controller".Length];
            _controllers[name] = type;
        }
    }

    private void DiscoverViews(Assembly assembly)
    {
        var viewBaseType = typeof(ConsoleView);

        var viewTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true }
                        && viewBaseType.IsAssignableFrom(t)
                        && t.Name.EndsWith("View"));

        foreach (var type in viewTypes)
        {
            // Extract controller name from namespace: ConsoleMVC.Views.{Controller}
            var ns = type.Namespace ?? "";
            var segments = ns.Split('.');
            var viewsIndex = Array.IndexOf(segments, "Views");

            if (viewsIndex < 0 || viewsIndex + 1 >= segments.Length)
            {
                Console.Error.WriteLine(
                    $"[ConsoleMVC] Warning: View '{type.FullName}' could not be mapped — " +
                    $"expected namespace pattern '*.Views.{{Controller}}'.");
                continue;
            }

            var controllerName = segments[viewsIndex + 1];
            var actionName = type.Name[..^"View".Length];

            _views[(controllerName, actionName)] = type;
        }
    }

    /// <summary>
    /// Create an instance of the controller for the given name.
    /// </summary>
    public Controller ResolveController(string controllerName)
    {
        if (!_controllers.TryGetValue(controllerName, out var type))
        {
            throw new InvalidOperationException(
                $"Controller '{controllerName}' not found. " +
                $"Available controllers: {string.Join(", ", _controllers.Keys)}.");
        }

        return (Controller)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Find the action method on the controller type.
    /// </summary>
    public MethodInfo ResolveAction(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (method is null || method.DeclaringType == typeof(object))
        {
            throw new InvalidOperationException(
                $"Action '{actionName}' not found on controller '{controllerType.Name}'. " +
                $"Ensure the method is public and returns ActionResult.");
        }

        return method;
    }

    /// <summary>
    /// Create an instance of the view for the given controller/action pair.
    /// </summary>
    public ConsoleView ResolveView(string controllerName, string actionName)
    {
        if (!_views.TryGetValue((controllerName, actionName), out var type))
        {
            throw new InvalidOperationException(
                $"View '{actionName}' not found for controller '{controllerName}'. " +
                $"Expected class '{actionName}View' in namespace '*.Views.{controllerName}'.");
        }

        return (ConsoleView)Activator.CreateInstance(type)!;
    }
}
