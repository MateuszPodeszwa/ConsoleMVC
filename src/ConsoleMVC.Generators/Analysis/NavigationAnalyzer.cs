#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ConsoleMVC.Generators.Analysis;

/// <summary>
/// Checks navigation written in C# against the controllers, actions and views of the compilation, the way CVW119 checks
/// <c>nav</c> attributes: <c>RedirectToAction("Detials")</c>, <c>NavigationResult.To("Orders", "Detials")</c> and
/// <c>NavigationResult.ToAction(...)</c> (CVW120), and <c>View("Nme")</c> or <c>View()</c> in an action without a view
/// (CVW121). Only constant names are checked (string literals, <c>const</c> fields, <c>nameof</c>); the code inside
/// <c>.cvw</c> views is checked too, and reported at the line of the view.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NavigationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The CVW121 property naming the controller (the view's folder), for the code fix that creates the view.</summary>
    public const string ControllerProperty = "Controller";

    /// <summary>The CVW121 property naming the missing view, without the <c>View</c> suffix.</summary>
    public const string ViewProperty = "View";

    /// <summary>The CVW121 property with the model's type for the new view's <c>@model</c>, when it is known.</summary>
    public const string ModelProperty = "Model";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [CvwDiagnostics.UnknownCodeRoute, CvwDiagnostics.MissingView];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Views generated from .cvw files are generated code; their C# is the user's, so it is checked too
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(start =>
        {
            var catalogue = Catalogue.Build(start.Compilation, RouteSymbols.IsModule(start.Compilation, start.Options.AnalyzerConfigOptionsProvider.GlobalOptions));
            if (catalogue.Controllers.Count == 0)
                return; // controllers compiled elsewhere: nothing to check against

            start.RegisterOperationAction(operation => Analyze(operation, catalogue), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, Catalogue catalogue)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var owner = method.ContainingType?.ToDisplayString();
        var caller = context.ContainingSymbol.ContainingType;

        if (owner == RouteSymbols.ControllerType && caller is not null && RouteSymbols.ControllerName(caller) is { } current)
        {
            if (method.Name == "RedirectToAction")
                CheckRoute(context, catalogue, invocation, $"RedirectToAction", Argument(invocation, "controller"), Argument(invocation, "action"), current);
            else if (method.Name == "View")
                CheckView(context, catalogue, invocation, current);
            return;
        }

        // app.MapKey(ConsoleKey.F1, "Help/Index", "Help"): the target is a full route, checked like nav="Help/Index"
        if (owner == RouteSymbols.ApplicationType && method.Name == "MapKey" && Argument(invocation, "target") is { } keyTarget)
        {
            if (Constant(keyTarget) is { } route)
                CheckTarget(context, catalogue, keyTarget, $"MapKey(\"{route}\")", route, current: null);
            return;
        }

        if (owner != RouteSymbols.NavigationResultType)
            return;

        // In a view, an action without a controller is on the view's own controller
        var viewController = caller is null ? null : RouteSymbols.ViewController(caller);

        switch (method.Name)
        {
            case "ToAction" when viewController is not null:
                CheckRoute(context, catalogue, invocation, "NavigationResult.ToAction", null, Argument(invocation, "action"), viewController);
                break;

            case "To" when Argument(invocation, "target") is { } target:
                if (Constant(target) is { } text)
                    CheckTarget(context, catalogue, target, $"NavigationResult.To(\"{text}\")", text, viewController);
                break;

            case "To":
                CheckRoute(context, catalogue, invocation, "NavigationResult.To", Argument(invocation, "controller"), Argument(invocation, "action"), viewController);
                break;
        }
    }

    /// <summary>
    /// Checks a target written as one string, <c>"Orders/Details?id=4"</c>; an action alone is on
    /// <paramref name="current"/>, and is not checked without one.
    /// </summary>
    private static void CheckTarget(OperationAnalysisContext context, Catalogue catalogue, IArgumentOperation target, string call,
        string text, string? current)
    {
        var path = text.Split('?')[0].Trim();
        var slash = path.LastIndexOf('/');
        if (slash >= 0)
            Report(context, catalogue, target.Syntax.GetLocation(), call,
                RouteSymbols.Qualify(path.Substring(0, slash), current, catalogue.Controllers.ContainsKey), path.Substring(slash + 1));
        else if (current is not null)
            Report(context, catalogue, target.Syntax.GetLocation(), call, current, path);
    }

    /// <summary>Checks a controller argument (or the current controller when it is absent or null) and an action argument.</summary>
    private static void CheckRoute(OperationAnalysisContext context, Catalogue catalogue, IInvocationOperation invocation, string call,
        IArgumentOperation? controllerArgument, IArgumentOperation? actionArgument, string? current)
    {
        if (actionArgument is null || Constant(actionArgument) is not { } action)
            return;

        string? controller;
        if (controllerArgument is null || controllerArgument.ArgumentKind == ArgumentKind.DefaultValue || IsNull(controllerArgument))
            controller = current;
        else if (Constant(controllerArgument) is { } named)
            controller = RouteSymbols.Qualify(named, current, catalogue.Controllers.ContainsKey);
        else
            return; // a controller computed at run time

        if (controller is null)
            return;

        var location = (controllerArgument is { ArgumentKind: not ArgumentKind.DefaultValue } && !IsNull(controllerArgument) && !catalogue.Controllers.ContainsKey(controller)
            ? controllerArgument.Syntax
            : actionArgument.Syntax).GetLocation();
        var written = controllerArgument is { ArgumentKind: not ArgumentKind.DefaultValue } && Constant(controllerArgument) is { } controllerText
            ? $"{call}(\"{controllerText}\", \"{action}\")"
            : $"{call}(\"{action}\")";
        Report(context, catalogue, location, written, controller, action);
    }

    private static void Report(OperationAnalysisContext context, Catalogue catalogue, Location location, string call, string controller, string action)
    {
        if (!catalogue.Controllers.TryGetValue(controller, out var actions))
        {
            if (catalogue.IsModule)
                return; // a module cannot see the controllers of the application that adds it

            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.UnknownCodeRoute, location,
                $"{call}: there is no controller '{controller}'.{RouteSuggestions.DidYouMean(catalogue.Controllers.Keys, controller)} " +
                $"Controllers: {RouteSuggestions.List(catalogue.Controllers.Keys, controller)}."));
            return;
        }

        if (!actions.Contains(action))
        {
            context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.UnknownCodeRoute, location,
                $"{call}: the controller '{controller}' has no action '{action}'.{RouteSuggestions.DidYouMean(actions, action)} " +
                $"Actions: {RouteSuggestions.List(actions, action)}."));
        }
    }

    /// <summary><c>View("Name")</c> needs <c>Views/{Controller}/{Name}View</c>; <c>View()</c> in an action needs the action's view.</summary>
    private static void CheckView(OperationAnalysisContext context, Catalogue catalogue, IInvocationOperation invocation, string controller)
    {
        string? name;
        Location location;
        if (Argument(invocation, "viewName") is { } viewName)
        {
            name = Constant(viewName);
            location = viewName.Syntax.GetLocation();
        }
        else if (context.ContainingSymbol is IMethodSymbol { } action && RouteSymbols.IsAction(action))
        {
            name = action.Name;
            location = invocation.Syntax.GetLocation();
        }
        else
        {
            return; // View() in a helper method: the action is only known at run time
        }

        if (name is null || catalogue.Views.Contains(controller + "/" + name))
            return;

        var existing = catalogue.Views.Where(v => v.StartsWith(controller + "/", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Substring(controller.Length + 1)).OrderBy(v => v, StringComparer.Ordinal).ToList();
        var others = existing.Count == 0 ? "" : $"{RouteSuggestions.DidYouMean(existing, name)} Views of '{controller}': {RouteSuggestions.List(existing, name)}.";

        // What the code fix needs to create the view: its folder, its name and, when known, its model type
        var properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties[ControllerProperty] = controller;
        properties[ViewProperty] = name;
        var model = Argument(invocation, "model")?.Value;
        if (model is IConversionOperation conversion)
            model = conversion.Operand;
        if (model?.Type is { SpecialType: not SpecialType.System_Object } modelType && model.ConstantValue is not { HasValue: true, Value: null })
            properties[ModelProperty] = modelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

        context.ReportDiagnostic(Diagnostic.Create(CvwDiagnostics.MissingView, location, properties.ToImmutable(),
            $"There is no view '{controller}/{name}'. Add {ViewFolder(controller)}/{name}View.cvw.{others}"));
    }

    /// <summary>Where the views of <paramref name="controller"/> live: <c>Views/Orders</c>, or <c>Areas/Admin/Views/Users</c>.</summary>
    internal static string ViewFolder(string controller)
    {
        var slash = controller.LastIndexOf('/');
        return slash < 0 ? "Views/" + controller : $"Areas/{controller.Substring(0, slash)}/Views/{controller.Substring(slash + 1)}";
    }

    private static IArgumentOperation? Argument(IInvocationOperation invocation, string parameter) =>
        invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameter);

    private static string? Constant(IArgumentOperation argument) =>
        argument.Value.ConstantValue is { HasValue: true, Value: string text } ? text : null;

    private static bool IsNull(IArgumentOperation argument) =>
        argument.Value.ConstantValue is { HasValue: true, Value: null };

    /// <summary>The controllers (with their actions) and views of a compilation, matched case-insensitively.</summary>
    private sealed class Catalogue
    {
        public Dictionary<string, HashSet<string>> Controllers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Views { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether the compilation is a module, whose application's controllers are not known.</summary>
        public bool IsModule { get; private set; }

        public static Catalogue Build(Compilation compilation, bool isModule)
        {
            var catalogue = new Catalogue { IsModule = isModule };

            // The controllers of the modules this compilation references are known too
            foreach (var module in RouteSymbols.ModuleControllers(compilation))
            {
                if (!catalogue.Controllers.TryGetValue(module.Name, out var known))
                    catalogue.Controllers[module.Name] = known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                known.UnionWith(module.Actions);
            }

            foreach (var type in RouteSymbols.AllTypes(compilation.Assembly.GlobalNamespace))
            {
                if (RouteSymbols.ControllerName(type) is { } controller)
                {
                    if (!catalogue.Controllers.TryGetValue(controller, out var actions))
                        catalogue.Controllers[controller] = actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    actions.UnionWith(RouteSymbols.ActionNames(type));
                }
                else if (RouteSymbols.ViewKey(type) is { } view)
                {
                    catalogue.Views.Add(view);
                }
            }

            return catalogue;
        }
    }
}
