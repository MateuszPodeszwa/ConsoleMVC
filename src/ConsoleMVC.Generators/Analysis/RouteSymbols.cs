#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Analysis;

/// <summary>
/// The router's discovery rules applied to Roslyn symbols, shared by the <c>nav</c> check in the generator (CVW119)
/// and the C# navigation analyzer (CVW120, CVW121).
/// </summary>
internal static class RouteSymbols
{
    /// <summary>The framework's controller base class.</summary>
    public const string ControllerType = "ConsoleMVC.Mvc.Controller";

    /// <summary>The framework's view base class.</summary>
    public const string ConsoleViewType = "ConsoleMVC.Mvc.ConsoleView";

    /// <summary>The base class of action results.</summary>
    public const string ActionResultType = "ConsoleMVC.Mvc.ActionResult";

    /// <summary>The attribute that hides a public method from routing.</summary>
    public const string NonActionAttributeType = "ConsoleMVC.Mvc.NonActionAttribute";

    /// <summary>The navigation result returned by views.</summary>
    public const string NavigationResultType = "ConsoleMVC.Mvc.NavigationResult";

    /// <summary>The application, whose <c>MapKey(key, "Help/Index")</c> names a route.</summary>
    public const string ApplicationType = "ConsoleMVC.Mvc.MvcApplication";

    /// <summary>The attribute that puts a controller in an area.</summary>
    public const string AreaAttributeType = "ConsoleMVC.Mvc.AreaAttribute";

    /// <summary>
    /// Returns the route name of a controller (<c>Orders</c> for <c>OrdersController</c>), or <see langword="null"/>
    /// when the type is not one the router discovers: a non-abstract, non-generic class named <c>*Controller</c>
    /// deriving from <see cref="ControllerType"/>.
    /// </summary>
    public static string? ControllerName(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsGenericType
            || !type.Name.EndsWith("Controller") || type.Name.Length <= "Controller".Length || !DerivesFrom(type, ControllerType))
            return null;

        var name = type.Name.Substring(0, type.Name.Length - "Controller".Length);
        return AreaOf(type) is { } area ? area + "/" + name : name;
    }

    /// <summary>A controller's area, as the runtime finds it: <c>[Area("…")]</c> (also inherited), or the namespace segment after <c>Areas</c>.</summary>
    private static string? AreaOf(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var attribute = current.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AreaAttributeType);
            if (attribute is { ConstructorArguments.Length: 1 } && attribute.ConstructorArguments[0].Value is string { Length: > 0 } named)
                return named.Trim();
        }

        var segments = type.ContainingNamespace?.ToDisplayString().Split('.') ?? [];
        var index = System.Array.LastIndexOf(segments, "Areas");
        return index >= 0 && index + 1 < segments.Length ? segments[index + 1] : null;
    }

    /// <summary>
    /// The controller a navigation means, as the runtime resolves it: inside an area (<paramref name="current"/> is
    /// <c>Area/Controller</c>), a controller named without an area is the area's own when it has one.
    /// </summary>
    public static string Qualify(string controller, string? current, System.Func<string, bool> exists)
    {
        var slash = current?.LastIndexOf('/') ?? -1;
        if (controller.Contains("/") || slash <= 0)
            return controller;

        var inArea = current!.Substring(0, slash) + "/" + controller;
        return exists(inArea) ? inArea : controller;
    }

    /// <summary>
    /// Returns the actions of a controller: public instance methods declared on it or a base class below
    /// <see cref="ControllerType"/>, returning an action result, not marked <c>[NonAction]</c>.
    /// </summary>
    public static IEnumerable<string> ActionNames(INamedTypeSymbol controller)
    {
        for (var current = controller; current is not null && current.ToDisplayString() != ControllerType; current = current.BaseType)
        {
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (IsAction(method))
                    yield return method.Name;
            }
        }
    }

    /// <summary>Whether <paramref name="method"/> is an action by the router's rules.</summary>
    public static bool IsAction(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && !method.IsStatic
        && IsActionReturnType(method.ReturnType)
        && !method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == NonActionAttributeType);

    /// <summary>
    /// Returns the <c>Controller/Action</c> key of a view the router discovers (a non-abstract class named <c>*View</c>
    /// deriving from <see cref="ConsoleViewType"/> in a namespace ending <c>.Views.{Controller}</c>), or
    /// <see langword="null"/>.
    /// </summary>
    public static string? ViewKey(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || !type.Name.EndsWith("View") || type.Name.Length == "View".Length
            || !DerivesFrom(type, ConsoleViewType))
            return null;

        return ViewController(type) is { } controller ? controller + "/" + type.Name.Substring(0, type.Name.Length - "View".Length) : null;
    }

    /// <summary>
    /// The controller a view belongs to, from its namespace (<c>*.Views.{Controller}</c>, or <c>Area/Controller</c> for
    /// <c>*.Areas.{Area}.Views.{Controller}</c>), or <see langword="null"/>.
    /// </summary>
    public static string? ViewController(INamedTypeSymbol type)
    {
        var segments = type.ContainingNamespace?.ToDisplayString().Split('.') ?? [];
        if (segments.Length < 2 || segments[segments.Length - 2] != "Views")
            return null;

        return segments.Length >= 4 && segments[segments.Length - 4] == "Areas"
            ? segments[segments.Length - 3] + "/" + segments[segments.Length - 1]
            : segments[segments.Length - 1];
    }

    /// <summary>An <c>ActionResult</c>, or a <c>Task</c> or <c>ValueTask</c> of one, as the router accepts.</summary>
    public static bool IsActionReturnType(ITypeSymbol type)
    {
        if (DerivesFrom(type, ActionResultType))
            return true;

        if (type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } generic)
        {
            var definition = generic.OriginalDefinition.ToDisplayString();
            return definition is "System.Threading.Tasks.Task<TResult>" or "System.Threading.Tasks.ValueTask<TResult>"
                   && DerivesFrom(generic.TypeArguments[0], ActionResultType);
        }

        return false;
    }

    /// <summary>Whether <paramref name="type"/> is, or derives from, the class named <paramref name="baseType"/>.</summary>
    public static bool DerivesFrom(ITypeSymbol? type, string baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseType)
                return true;
        }

        return false;
    }

    /// <summary>Every named type declared in <paramref name="ns"/> and its nested namespaces and types.</summary>
    public static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in Nested(type))
                yield return nested;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var type in AllTypes(child))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in Nested(nested))
                yield return deeper;
        }
    }
}
