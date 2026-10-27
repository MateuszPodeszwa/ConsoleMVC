using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

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
/// (not on <see cref="Controller"/> or <see cref="object"/>), that returns <see cref="ActionResult"/> (or
/// <see cref="Task{TResult}"/> / <see cref="ValueTask{TResult}"/> of one),
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
/// var controller = router.ResolveController("Home", services);
/// var action = router.ResolveAction(controller.GetType(), "Index");
/// </code>
/// </example>
public class Router
{
    private readonly Dictionary<string, Type> _controllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _views = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(Type Controller, string Action), MethodInfo> _actionCache = new();

    /// <summary>Names registered by more than one type, mapped to every such type, so lookups can report them.</summary>
    private readonly Dictionary<string, List<Type>> _ambiguous = new(StringComparer.OrdinalIgnoreCase);

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
    public Controller ResolveController(string controllerName) =>
        (Controller)Activator.CreateInstance(ControllerType(controllerName))!;

    /// <summary>
    /// Creates a new instance of the controller registered under the specified name, passing services from
    /// <paramref name="services"/> to its constructor.
    /// </summary>
    /// <param name="controllerName">The logical controller name (without the <c>Controller</c> suffix), case-insensitive.</param>
    /// <param name="services">The services available to the controller's constructor, typically a navigation scope.</param>
    /// <returns>A new <see cref="Controller"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no controller is registered with the given name, or a constructor parameter cannot be resolved.
    /// </exception>
    /// <example>
    /// <code>
    /// // public class HomeController(IGreetingService greetings) : Controller { ... }
    /// var controller = router.ResolveController("Home", scope.ServiceProvider);
    /// </code>
    /// </example>
    public Controller ResolveController(string controllerName, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return (Controller)ActivatorUtilities.CreateInstance(services, ControllerType(controllerName));
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
                "Ensure the method is public, returns ActionResult (or Task<ActionResult>) and is not marked [NonAction].");
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
    /// <remarks>
    /// Views created this way do not receive <c>@inject</c> services; use
    /// <see cref="ResolveView(string, string, IServiceProvider)"/> for that.
    /// </remarks>
    public ConsoleView ResolveView(string controllerName, string actionName) =>
        (ConsoleView)Activator.CreateInstance(ViewType(controllerName, actionName))!;

    /// <summary>
    /// Creates a new instance of the view registered for the specified controller and action pair, passing services
    /// to its constructor and filling its <see cref="InjectAttribute"/> properties (generated by <c>@inject</c>).
    /// </summary>
    /// <param name="controllerName">The logical controller name (without the <c>Controller</c> suffix), case-insensitive.</param>
    /// <param name="actionName">The action name corresponding to the view, case-insensitive.</param>
    /// <param name="services">The services available to the view, typically a navigation scope.</param>
    /// <returns>A new <see cref="ConsoleView"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no view is registered for the given controller and action combination, or an injected service is
    /// not registered.
    /// </exception>
    /// <example>
    /// <code>
    /// var view = router.ResolveView("Home", "Index", scope.ServiceProvider);
    /// </code>
    /// </example>
    public ConsoleView ResolveView(string controllerName, string actionName, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var view = (ConsoleView)ActivatorUtilities.CreateInstance(services, ViewType(controllerName, actionName));
        view.Services = services; // for the @inject properties of partials it renders
        ServiceInjector.InjectProperties(view, services);
        return view;
    }

    /// <summary>
    /// Returns the controller type registered under <paramref name="controllerName"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no controller has that name.</exception>
    private Type ControllerType(string controllerName)
    {
        ThrowIfAmbiguous("Controller", controllerName);

        if (!_controllers.TryGetValue(controllerName, out var type))
        {
            throw new InvalidOperationException(
                $"Controller '{controllerName}' not found. " +
                $"Available controllers: {string.Join(", ", _controllers.Keys)}.");
        }

        return type;
    }

    /// <summary>
    /// Returns the view type registered for <paramref name="controllerName"/> and <paramref name="actionName"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no such view exists.</exception>
    private Type ViewType(string controllerName, string actionName)
    {
        ThrowIfAmbiguous("View", ViewKey(controllerName, actionName));

        if (!_views.TryGetValue(ViewKey(controllerName, actionName), out var type))
        {
            throw new InvalidOperationException(
                $"View '{actionName}' not found for controller '{controllerName}'. " +
                $"Expected 'Views/{controllerName}/{actionName}View.cvw' " +
                $"(class '{actionName}View' in namespace '*.Views.{controllerName}').");
        }

        return type;
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
        && ActionInvoker.IsActionReturnType(method.ReturnType)
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
            Register(_controllers, "Controller", name, type);
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

            Register(_views, "View", ViewKey(controllerName, actionName), type);
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

    /// <summary>
    /// Registers <paramref name="type"/> under <paramref name="name"/>. When another type already has the name (for
    /// example <c>HomeController</c> in two namespaces), both are remembered and resolving the name fails with a
    /// message naming them, instead of one silently replacing the other.
    /// </summary>
    private void Register(Dictionary<string, Type> registry, string kind, string name, Type type)
    {
        if (registry.TryGetValue(name, out var existing) && existing != type)
        {
            var key = kind + ":" + name;
            if (!_ambiguous.TryGetValue(key, out var types))
                _ambiguous[key] = types = [existing];

            if (!types.Contains(type))
                types.Add(type);
        }

        registry[name] = type;
    }

    /// <exception cref="InvalidOperationException">Thrown when several types share <paramref name="name"/>.</exception>
    private void ThrowIfAmbiguous(string kind, string name)
    {
        if (!_ambiguous.TryGetValue(kind + ":" + name, out var types))
            return;

        throw new InvalidOperationException(
            $"{kind} '{name}' is ambiguous: it matches {string.Join(", ", types.Select(t => $"'{t.FullName}'"))}. " +
            "Names are matched without their namespace (and case-insensitively), so rename all but one.");
    }
}
