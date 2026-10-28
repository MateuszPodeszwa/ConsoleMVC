#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using ConsoleMVC.Generators.Analysis;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Emit;

/// <summary>One controller of the route table, as the C# that creates its <c>ControllerRoute</c>, and the model types to keep.</summary>
internal sealed class TableController : IEquatable<TableController>
{
    /// <param name="typeName">The controller's fully qualified name, to sort and remove duplicates (partial classes).</param>
    /// <param name="code">The <c>new ControllerRoute(…)</c> expression.</param>
    /// <param name="preserved">The fully qualified model types (one per line) whose members model binding uses.</param>
    public TableController(string typeName, string code, string preserved)
    {
        TypeName = typeName;
        Code = code;
        Preserved = preserved;
    }

    public string TypeName { get; }

    public string Code { get; }

    public string Preserved { get; }

    public bool Equals(TableController? other) =>
        other is not null && TypeName == other.TypeName && Code == other.Code && Preserved == other.Preserved;

    public override bool Equals(object? obj) => Equals(obj as TableController);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);
}

/// <summary>One view of the route table, as the C# that creates its <c>ViewRoute</c>.</summary>
internal sealed class TableView : IEquatable<TableView>
{
    /// <param name="typeName">The view's fully qualified name.</param>
    /// <param name="code">The <c>new ViewRoute(…)</c> expression.</param>
    public TableView(string typeName, string code)
    {
        TypeName = typeName;
        Code = code;
    }

    public string TypeName { get; }

    public string Code { get; }

    public bool Equals(TableView? other) => other is not null && TypeName == other.TypeName && Code == other.Code;

    public override bool Equals(object? obj) => Equals(obj as TableView);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);
}

/// <summary>
/// Writes the generated route table (<c>ConsoleMVC.Generated.RouteTable</c>, an <c>IRouteTable</c>) and the assembly's
/// <c>RouteTable</c> attribute: typed factories for controllers and views, typed invokers for actions, and
/// <c>DynamicDependency</c> attributes that keep model types for trimming and Native AOT. See docs/design/native-aot.md.
/// </summary>
internal static class RouteTableEmitter
{
    /// <summary>The generated file's hint name.</summary>
    public const string HintName = "ConsoleMVC.RouteTable.g.cs";

    private const string Mvc = "global::ConsoleMVC.Mvc.";

    /// <summary>Fully qualified names without nullable annotations, which <c>typeof</c> does not accept.</summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                                  & ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>How deep model types are followed through their properties and collections.</summary>
    private const int MaxModelDepth = 4;

    /// <summary>Describes a controller for the table, or <see langword="null"/> when <paramref name="type"/> is not one.</summary>
    public static TableController? ForController(INamedTypeSymbol type)
    {
        if (RouteSymbols.ControllerName(type) is not { } name)
            return null;

        var full = Name(type);
        var actions = new List<string>();
        var preserved = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var method in Actions(type))
        {
            if (method.IsGenericMethod || method.Parameters.Any(p => p.RefKind != RefKind.None || p.IsParams))
                continue; // called by reflection instead

            var parameters = method.Parameters;
            var types = parameters.Length == 0
                ? "global::System.Type.EmptyTypes"
                : "new global::System.Type[] { " + string.Join(", ", parameters.Select(p => $"typeof({Name(p.Type)})")) + " }";
            var arguments = string.Join(", ", parameters.Select((p, i) => $"({Name(p.Type)})a![{i}]!"));
            var call = $"(({full})c).{Escape(method.Name)}({arguments})";

            actions.Add($"new {Mvc}ActionRoute(\"{method.Name}\", typeof({full}).GetMethod(\"{method.Name}\", {types})!, " +
                        $"static (c, a) => {Wrap(method.ReturnType, call)})");

            foreach (var parameter in parameters)
                Preserve(parameter.Type, preserved, 0);
        }

        var code = $"new {Mvc}ControllerRoute(\"{name}\", typeof({full}), {Factory(type)}, new {Mvc}ActionRoute[]\n" +
                   "            {\n" + string.Concat(actions.Select(a => "                " + a + ",\n")) + "            })";
        return new TableController(full, code, string.Join("\n", preserved));
    }

    /// <summary>Describes a hand-written view for the table, or <see langword="null"/> when <paramref name="type"/> is not one.</summary>
    public static TableView? ForView(INamedTypeSymbol type)
    {
        if (type.IsGenericType || RouteSymbols.ViewKey(type) is not { } key)
            return null;

        var create = type.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0)
            ? $"static () => new {Name(type)}()"
            : "null"; // constructor injection: created by ActivatorUtilities
        return new TableView(Name(type), $"new {Mvc}ViewRoute(\"{key}\", typeof({Name(type)}), {create})");
    }

    /// <summary>
    /// The assembly attribute a view compiled from a <c>.cvw</c> file adds to its own file, so the router finds it
    /// without scanning (<c>GeneratedViewAttribute</c>).
    /// </summary>
    public static string RegistrationFor(ViewInfo info) =>
        $"[assembly: {Mvc}GeneratedViewAttribute(\"{info.ControllerName}/{info.ClassName.Substring(0, info.ClassName.Length - "View".Length)}\", " +
        $"typeof(global::{info.FullClassName}))]";

    /// <summary>Writes the table, or returns <see langword="null"/> when the project has no controllers or views at all.</summary>
    public static string? Emit(IEnumerable<TableController> controllers, IEnumerable<TableView> views, bool hasViewFiles)
    {
        var controllerList = controllers.GroupBy(c => c.TypeName).Select(g => g.First()).OrderBy(c => c.TypeName, StringComparer.Ordinal).ToList();
        var viewList = views.GroupBy(v => v.TypeName).Select(g => g.First()).OrderBy(v => v.TypeName, StringComparer.Ordinal).ToList();
        if (controllerList.Count == 0 && viewList.Count == 0 && !hasViewFiles)
            return null;

        var preserved = controllerList.SelectMany(c => c.Preserved.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal);

        var output = new StringBuilder();
        output.Append("// <auto-generated/>\n");
        output.Append("// The route table of this assembly, written by the ConsoleMVC source generator: the router finds, creates and\n");
        output.Append("// calls controllers, actions and views through it instead of reflection (docs/design/native-aot.md).\n");
        output.Append("#nullable disable\n");
        output.Append("#pragma warning disable CS1591, CS0618\n\n");
        output.Append("[assembly: global::ConsoleMVC.Mvc.RouteTableAttribute(typeof(global::ConsoleMVC.Generated.RouteTable))]\n\n");
        output.Append("namespace ConsoleMVC.Generated\n{\n");
        output.Append("    [global::System.CodeDom.Compiler.GeneratedCode(\"ConsoleMVC.Generators\", null)]\n");
        output.Append("    internal sealed class RouteTable : global::ConsoleMVC.Mvc.IRouteTable\n    {\n");

        // Model binding reads and writes these types' properties and constructors by reflection: keep them when trimming
        foreach (var type in preserved)
        {
            output.Append("        [global::System.Diagnostics.CodeAnalysis.DynamicDependency(")
                .Append("global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties | ")
                .Append("global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors, typeof(")
                .Append(type).Append("))]\n");
        }

        output.Append("        public RouteTable()\n        {\n        }\n\n");

        output.Append("        public global::System.Collections.Generic.IReadOnlyList<global::ConsoleMVC.Mvc.ControllerRoute> Controllers { get; } =\n");
        output.Append("            new global::ConsoleMVC.Mvc.ControllerRoute[]\n            {\n");
        foreach (var controller in controllerList)
            output.Append("            ").Append(controller.Code).Append(",\n");
        output.Append("            };\n\n");

        output.Append("        public global::System.Collections.Generic.IReadOnlyList<global::ConsoleMVC.Mvc.ViewRoute> Views { get; } =\n");
        output.Append("            new global::ConsoleMVC.Mvc.ViewRoute[]\n            {\n");
        foreach (var view in viewList)
            output.Append("                ").Append(view.Code).Append(",\n");
        output.Append("            };\n    }\n}\n");

        return output.ToString();
    }

    /// <summary>The public actions of a controller and its base classes, as the router finds them (most derived first).</summary>
    private static IEnumerable<IMethodSymbol> Actions(INamedTypeSymbol controller)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = controller; current is not null && current.ToDisplayString() != RouteSymbols.ControllerType; current = current.BaseType)
        {
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>().Where(RouteSymbols.IsAction))
            {
                // An override and the method it overrides are one action
                var signature = method.Name + "(" + string.Join(",", method.Parameters.Select(p => p.Type.ToDisplayString())) + ")";
                if (seen.Add(signature))
                    yield return method;
            }
        }
    }

    /// <summary>
    /// <c>services =&gt; new T(…)</c> when the controller has one public constructor whose parameters can all be resolved,
    /// or <c>null</c> to let the router use <c>ActivatorUtilities</c>, which also handles defaults and several constructors.
    /// </summary>
    private static string Factory(INamedTypeSymbol type)
    {
        var constructors = type.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();
        if (constructors.Count != 1 || constructors[0].Parameters.Any(p => p.HasExplicitDefaultValue || p.RefKind != RefKind.None || p.IsParams))
            return "null";

        var arguments = constructors[0].Parameters.Select(p => p.Type.ToDisplayString() == "System.IServiceProvider"
            ? "services"
            : $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{Name(p.Type)}>(services)");
        return $"static services => new {Name(type)}({string.Join(", ", arguments)})";
    }

    /// <summary>Turns the call into a <c>Task&lt;ActionResult&gt;</c> without reflection.</summary>
    private static string Wrap(ITypeSymbol returnType, string call)
    {
        if (returnType is INamedTypeSymbol { IsGenericType: true } generic)
        {
            var definition = generic.OriginalDefinition.ToDisplayString();
            if (definition == "System.Threading.Tasks.Task<TResult>")
                return $"{Mvc}ActionRoute.FromTask({call})";
            if (definition == "System.Threading.Tasks.ValueTask<TResult>")
                return $"{Mvc}ActionRoute.FromValueTask({call})";
        }

        return $"{Mvc}ActionRoute.FromResult({call})";
    }

    /// <summary>Adds a model type, the types of its public properties and its collections' element types.</summary>
    private static void Preserve(ITypeSymbol type, ISet<string> preserved, int depth)
    {
        if (depth > MaxModelDepth || IsSimple(type))
            return;

        switch (type)
        {
            case IArrayTypeSymbol array:
                Preserve(array.ElementType, preserved, depth + 1);
                return;
            case INamedTypeSymbol { IsGenericType: true } generic when generic.AllInterfaces.Any(i => i.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal))
                                                                     || generic.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal):
                foreach (var argument in generic.TypeArguments)
                    Preserve(argument, preserved, depth + 1);
                return;
            case INamedTypeSymbol named when named.TypeKind is TypeKind.Class or TypeKind.Struct && !named.IsAbstract:
                if (!preserved.Add(Name(named)))
                    return;

                foreach (var property in named.GetMembers().OfType<IPropertySymbol>().Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic))
                    Preserve(property.Type, preserved, depth + 1);
                return;
        }
    }

    /// <summary>Types the binder converts from text by itself, which need nothing kept.</summary>
    private static bool IsSimple(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return IsSimple(nullable.TypeArguments[0]);

        if (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum)
            return true;

        return type.ToDisplayString() is "System.DateTimeOffset" or "System.TimeSpan" or "System.Guid" or "System.DateOnly"
            or "System.TimeOnly" or "System.Uri" or "System.Threading.CancellationToken" or "object";
    }

    private static string Name(ITypeSymbol type) => type.ToDisplayString(TypeFormat);

    private static string Escape(string identifier) =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(identifier) == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ? identifier : "@" + identifier;
}
