#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// Checks literal <c>nav</c> targets (<c>nav="Orders/Details"</c>, <c>nav="About"</c>) against the controllers and
/// actions of the compilation, reporting CVW119 for a typo. Targets built from expressions are only known at run
/// time and are not checked.
/// </summary>
internal static class RouteChecker
{
    /// <summary>
    /// Returns a warning for every literal <c>nav</c> target that names a missing controller or action.
    /// </summary>
    /// <param name="document">The parsed view.</param>
    /// <param name="view">The view, whose folder is the controller for targets without one.</param>
    /// <param name="routes">The controllers and actions of the compilation.</param>
    /// <returns>The warnings, empty when the compilation has no controllers.</returns>
    public static IReadOnlyList<ParseDiagnostic> Check(CvwDocument document, ViewInfo view, RouteCatalogue routes)
    {
        if (routes.IsEmpty)
            return [];

        var warnings = new List<ParseDiagnostic>();

        foreach (var attribute in CvwDocument.Elements(document.Body).SelectMany(e => e.Attributes))
        {
            if (attribute.Schema.Property != "Target" || attribute.Parts.Count != 1 || attribute.Parts[0].IsExpression)
                continue;

            var part = attribute.Parts[0];
            var error = Explain(part.Text, view, routes, out var length);
            if (error is not null)
                warnings.Add(new ParseDiagnostic(CvwDiagnostics.UnknownRoute, new SourceSpan(part.Span.Start, length), error));
        }

        return warnings;
    }

    /// <summary>Explains why <paramref name="target"/> does not exist, or returns <see langword="null"/> when it does.</summary>
    private static string? Explain(string target, ViewInfo view, RouteCatalogue routes, out int length)
    {
        var text = target.Trim();
        var path = text.Split('?')[0];
        length = Math.Max(path.Length, 1);

        var slash = path.LastIndexOf('/');
        string controller;
        string action;
        if (slash < 0)
        {
            // An action of the view's own controller; a folder without a controller (or Views/Shared) cannot be checked
            if (view.IsPartial || !routes.TryGetController(view.ControllerName, out _))
                return null;

            controller = view.ControllerName;
            action = path;
        }
        else
        {
            // Inside an area, a controller named without one is the area's own when it has one
            controller = Analysis.RouteSymbols.Qualify(path.Substring(0, slash), view.IsPartial ? null : view.ControllerName,
                name => routes.TryGetController(name, out _));
            action = path.Substring(slash + 1);
        }

        // A module cannot see the controllers of the application that adds it
        if (!routes.TryGetController(controller, out var actions) && routes.IsModule)
            return null;

        if (!routes.TryGetController(controller, out actions))
            return $"nav=\"{text}\": there is no controller '{controller}'.{Analysis.RouteSuggestions.DidYouMean(routes.ControllerNames, controller)} " +
                   $"Controllers: {Analysis.RouteSuggestions.List(routes.ControllerNames, controller)}.";

        if (!actions.Contains(action))
            return $"nav=\"{text}\": the controller '{controller}' has no action '{action}'.{Analysis.RouteSuggestions.DidYouMean(routes.ActionNamesOf(controller), action)} " +
                   $"Actions: {Analysis.RouteSuggestions.List(routes.ActionNamesOf(controller), action)}.";

        return null;
    }
}
