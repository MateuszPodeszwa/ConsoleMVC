using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Convention-based router that discovers controllers and views via reflection at startup
/// and resolves them by name during the application loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Controller discovery:</b> Any non-abstract class inheriting from <see cref="Controller"/>
/// whose name ends with <c>Controller</c> is registered. The logical name is derived by
/// stripping the suffix (e.g. <c>HomeController</c> becomes <c>Home</c>).
/// </para>
/// <para>
/// <b>View discovery:</b> Any non-abstract class inheriting from <see cref="ConsoleView"/>
/// whose name ends with <c>View</c> is registered. The controller and action names are
/// extracted from the namespace and class name respectively — the expected namespace
/// pattern is <c>*.Views.{Controller}</c> and the class name pattern is <c>{Action}View</c>.
/// </para>
/// </remarks>
public class Router
{
    private readonly Dictionary<string, Type> _controllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Controller, string Action), Type> _views = new();

    /// <summary>
    /// Scans the specified assembly for all controllers and views, registering them
    /// for subsequent resolution.
    /// </summary>
    /// <param name="assembly">The assembly to scan, typically the entry assembly of the consuming application.</param>
    public void DiscoverAll(Assembly assembly)
    {
        DiscoverControllers(assembly);
        DiscoverViews(assembly);
    }

    /// <summary>
    /// Discovers and registers all controller types in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for controller types.</param>
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

    /// <summary>
    /// Discovers and registers all view types in the specified assembly. Views are
    /// mapped to their controller and action using namespace and naming conventions.
    /// </summary>
    /// <param name="assembly">The assembly to scan for view types.</param>
    private void DiscoverViews(Assembly assembly)
    {
        var viewBaseType = typeof(ConsoleView);

        var viewTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true }
                        && viewBaseType.IsAssignableFrom(t)
                        && t.Name.EndsWith("View"));

        foreach (var type in viewTypes)
        {
            // Extract controller name from namespace: *.Views.{Controller}
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
    /// Creates a new instance of the controller registered under the specified name.
    /// </summary>
    /// <param name="controllerName">The logical controller name (without the <c>Controller</c> suffix).</param>
    /// <returns>A new <see cref="Controller"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no controller is registered with the given name.
    /// </exception>
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
    /// Locates the public action method with the specified name on the given controller type.
    /// </summary>
    /// <param name="controllerType">The concrete controller type to search.</param>
    /// <param name="actionName">The name of the action method to find (case-insensitive).</param>
    /// <returns>The <see cref="MethodInfo"/> for the matching action method.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no matching public action method is found on the controller.
    /// </exception>
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
    /// Creates a new instance of the view registered for the specified controller and action pair.
    /// </summary>
    /// <param name="controllerName">The logical controller name (without the <c>Controller</c> suffix).</param>
    /// <param name="actionName">The action name corresponding to the view.</param>
    /// <returns>A new <see cref="ConsoleView"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no view is registered for the given controller and action combination.
    /// </exception>
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
