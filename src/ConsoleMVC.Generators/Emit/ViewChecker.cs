#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Parsing;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// Checks a parsed view against what it is generated into, its class, so a mistake the C# compiler would report in the
/// generated file (where the <c>.cvw</c> file has no such line) is a CVW error in the view instead.
/// </summary>
/// <remarks>
/// The checks: an <c>@inject</c> property named after the view's class (CVW007), and a view that never returns a
/// <c>NavigationResult</c> because it has neither a <c>&lt;menu&gt;</c> nor a <c>return</c> (CVW012).
/// </remarks>
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

        // A view's Render returns a NavigationResult: with no <menu> and no return at all, the compiler's CS0161 would
        // point at the method's declaration in the generated file. A view that returns on some paths only, or never
        // finishes (a loop, a throw), is left to the compiler.
        if (!info.IsPartial && !CvwDocument.Elements(document.Body).Any(e => e.Schema.Tag == MarkupSchema.MenuTag)
            && !Code(document.Body).Any(code => EndsAPath(code.Code)))
        {
            errors.Add(new ParseDiagnostic(CvwDiagnostics.MissingResult, LastLine(document.Text)));
        }

        return errors;
    }

    /// <summary>Keywords after which the end of <c>Render</c> may never be reached, so CS0161 may not apply.</summary>
    private static readonly HashSet<string> PathEndingKeywords = new(StringComparer.Ordinal) { "return", "throw", "while", "for", "goto" };

    /// <summary>Every C# code node, including those inside elements (the bodies of <c>@if</c>, <c>@foreach</c> and <c>@{ }</c>).</summary>
    private static IEnumerable<CodeNode> Code(IEnumerable<Node> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is CodeNode code)
                yield return code;
            else if (node is ElementNode element)
            {
                foreach (var child in Code(element.Children))
                    yield return child;
            }
        }
    }

    /// <summary>Whether <paramref name="code"/> has a keyword that can end a path through the method, outside strings and comments.</summary>
    private static bool EndsAPath(string code)
    {
        var i = 0;
        while (i < code.Length)
        {
            var skip = CSharpLexer.Skip(code, i);
            if (skip.Kind != TriviaKind.None)
            {
                i = skip.End;
                continue;
            }

            if (!char.IsLetter(code[i]) && code[i] != '_')
            {
                i++;
                continue;
            }

            // A verbatim identifier such as @return is not the keyword
            var verbatim = i > 0 && code[i - 1] == '@';
            var start = i;
            while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                i++;

            if (!verbatim && PathEndingKeywords.Contains(code.Substring(start, i - start)))
                return true;
        }

        return false;
    }

    /// <summary>The last line with text in the view, where a return would go; the start of the file when it is empty.</summary>
    private static SourceSpan LastLine(string text)
    {
        var end = text.Length;
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
            end--;

        var start = end == 0 ? 0 : text.LastIndexOf('\n', end - 1) + 1;
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;

        return SourceSpan.FromBounds(start, end);
    }
}
