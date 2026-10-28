#nullable enable

using System;
using System.Collections.Generic;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// Checks a parsed view against what it is generated into, its class, so a mistake the C# compiler would report in the
/// generated file (where the <c>.cvw</c> file has no such line) is a CVW error in the view instead.
/// </summary>
/// <example><code>var errors = ViewChecker.Check(document, info); // CVW007 for "@inject IClock IndexView" in IndexView</code></example>
internal static class ViewChecker
{
    /// <summary>
    /// Returns the view's errors.
    /// </summary>
    /// <param name="document">The parsed view, free of parse errors.</param>
    /// <param name="info">The class the view is generated into.</param>
    /// <returns>The errors; empty when the view can be generated.</returns>
    /// <example><code>if (ViewChecker.Check(document, info).Count == 0) { /* emit */ }</code></example>
    public static IReadOnlyList<ParseDiagnostic> Check(CvwDocument document, ViewInfo info)
    {
        var errors = new List<ParseDiagnostic>();

        // A member cannot have its class's name (CS0542)
        foreach (var inject in document.Injects)
        {
            if (string.Equals(inject.Name.Value, info.ClassName, StringComparison.Ordinal))
            {
                errors.Add(new ParseDiagnostic(CvwDiagnostics.InvalidInject, inject.Name.Span,
                    $"'{inject.Name.Value}' is the name of the view's own class; choose another property name"));
            }
        }

        return errors;
    }
}
