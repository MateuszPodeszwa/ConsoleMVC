using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Convention-based router that discovers controllers and views via reflection at startup
/// and resolves them by name during the application loop. All lookups are case-insensitive.
/// </summary>
/// <remarks>
/// <para>
/// <b>Controller discovery:</b> Any non-abstract class inheriting from <see cref="Controller"/>
/// whose name ends with <c>Controller</c> is registered. The logical name is derived by
/// stripping the suffix (e.g. <c>HomeController</c> becomes <c>Home</c>).
/// </para>
/// <para>
/// <b>Action resolution:</b> An action is a public instance method declared on a controller
/// (not on <see cref="Controller"/> or <see cref="object"/>), that returns <see cref="ActionResult"/>,
/// is not a property accessor and is not marked <see cref="NonActionAttribute"/>. Overloaded
/// actions are not supported because navigation identifies actions by name only.
/// </para>
/// <para>
/// <b>View discovery:</b> Any non-abstract class inheriting from <see cref="ConsoleView"/>
/// whose name ends with <c>View</c> is registered. The controller and action names are
/// extracted from the namespace and class name respectively — the expected namespace
/// pattern is <c>*.Views.{Controller}</c> and the class name pattern is <c>{Action}View</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var router = new Router();
/// router.DiscoverAll(typeof(Program).Assembly);
/// var controller = router.ResolveController("Home");
/// var action = router.ResolveAction(controller.GetType(), "Index");
/// </code>
/// </example>
public class Router
{
    private readonly Dictionary<string, Type> _controllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _views = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(Type Controller, string Action), MethodInfo> _actionCache = new();

    /// <summary>
    /// Scans the specified assembly for all controllers and views, registering them
    /// for subsequent resolution.
    /// </summary>
    /// <param name="assembly">The assembly to scan, typically the entry assembly of the consuming application.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is <see langword="null"/>.</exception>
    public void DiscoverAll(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var types = GetLoadableTypes(assembly);
        DiscoverControllers(types);
        DiscoverViews(types);
    }

    /// <summary>
    /// Creates a new instance of the controller registered under the specified name.
    /// </summary>
    /// <param name="controllerName">The logical controller name (without the <c>Controller</c> suffix), case-insensitive.</param>
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
    /// Locates the action method with the specified name on the given controller type.
    /// </summary>
    /// <param name="controllerType">The concrete controller type to search.</param>
    /// <param name="actionName">The name of the action method to find (case-insensitive).</param>
    /// <returns>The <see cref="MethodInfo"/> for the matching action method.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no matching action exists, or when the action is overloaded.
    /// </exception>
    public MethodInfo ResolveAction(Type controllerType, string actionName)
    {
        if (_actionCache.TryGetValue((controllerType, actionName), out var cached))
            return cached;

        var candidates = controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => string.Equals(m.Name, actionName, StringComparison.OrdinalIgnoreCase) && IsAction(m))
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"Action '{actionName}' not found on controller '{controllerType.Name}'. " +
                "Ensure the method is public, returns ActionResult and is not marked [NonAction].");
        }

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                $"Action '{actionName}' on controller '{controllerType.Name}' has {candidates.Length} overloads. " +
                "Actions are resolved by name, so each action name must be unique; rename one of the methods.");
        }

        _actionCache[(controllerType, actionName)] = candidates[0];
        return candidates[0];
    }

    /// <summary>
    /// Creates a new instance of the view registered for the specified controller and action pair.
    /// </summary>
    /// <param name="controllerName">The logical controller name (without the <c>Controller</c> suffix), case-insensitive.</param>
    /// <param name="actionName">The action name corresponding to the view, case-insensitive.</param>
    /// <returns>A new <see cref="ConsoleView"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no view is registered for the given controller and action combination.
    /// </exception>
    public ConsoleView ResolveView(string controllerName, string actionName)
    {
        if (!_views.TryGetValue(ViewKey(controllerName, actionName), out var type))
        {
            throw new InvalidOperationException(
                $"View '{actionName}' not found for controller '{controllerName}'. " +
                $"Expected 'Views/{controllerName}/{actionName}View.cvw' " +
                $"(class '{actionName}View' in namespace '*.Views.{controllerName}').");
        }

        return (ConsoleView)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="method"/> qualifies as a routable action.
    /// </summary>
    private static bool IsAction(MethodInfo method) =>
        !method.IsSpecialName
        && !method.IsGenericMethodDefinition
        && method.DeclaringType is not null
        && method.DeclaringType != typeof(Controller)
        && method.DeclaringType != typeof(object)
        && typeof(ActionResult).IsAssignableFrom(method.ReturnType)
        && !method.IsDefined(typeof(NonActionAttribute), inherit: true);

    /// <summary>
    /// Discovers and registers all controller types.
    /// </summary>
    private void DiscoverControllers(IEnumerable<Type> types)
    {
        var controllerBaseType = typeof(Controller);

        foreach (var type in types.Where(t => t is { IsAbstract: false, IsClass: true }
                                              && controllerBaseType.IsAssignableFrom(t)
                                              && t.Name.EndsWith("Controller", StringComparison.Ordinal)))
        {
            var name = type.Name[..^"Controller".Length];
            _controllers[name] = type;
        }
    }

    /// <summary>
    /// Discovers and registers all view types, mapping them to their controller and action
    /// using namespace and naming conventions.
    /// </summary>
    private void DiscoverViews(IEnumerable<Type> types)
    {
        var viewBaseType = typeof(ConsoleView);

        foreach (var type in types.Where(t => t is { IsAbstract: false, IsClass: true }
                                              && viewBaseType.IsAssignableFrom(t)
                                              && t.Name.EndsWith("View", StringComparison.Ordinal)))
        {
            // Extract controller name from namespace: *.Views.{Controller} (controller is the last segment)
            var segments = (type.Namespace ?? "").Split('.');

            if (segments.Length < 2 || segments[^2] != "Views")
            {
                Console.Error.WriteLine(
                    $"[ConsoleMVC] Warning: View '{type.FullName}' could not be mapped — " +
                    "expected namespace pattern '*.Views.{Controller}'.");
                continue;
            }

            var controllerName = segments[^1];
            var actionName = type.Name[..^"View".Length];

            _views[ViewKey(controllerName, actionName)] = type;
        }
    }

    /// <summary>
    /// Returns the types of <paramref name="assembly"/> that could be loaded. When some types depend on
    /// assemblies that are missing at run time, the remaining types are still usable.
    /// </summary>
    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t is not null).ToArray()!;
        }
    }

    private static string ViewKey(string controllerName, string actionName) => $"{controllerName}/{actionName}";
}
