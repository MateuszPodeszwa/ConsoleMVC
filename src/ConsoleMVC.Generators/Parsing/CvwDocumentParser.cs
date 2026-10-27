#nullable enable

using System;
using System.Collections.Generic;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Parsing;

/// <summary>
/// Parses a complete <c>.cvw</c> file: the directive preamble (<c>@model</c>, <c>@using</c>) followed by
/// the body (C# with embedded markup).
/// </summary>
/// <remarks>
/// Directive rules are unchanged from ConsoleMVC 1.x: directives appear one per line at the top of the
/// file, blank lines between them are ignored, and the first other line starts the body.
/// <c>@model</c> is optional and defaults to <c>object</c>.
/// </remarks>
/// <example>
/// <code>
/// var document = CvwDocumentParser.Parse("@model string\n&lt;text&gt;@Model&lt;/text&gt;\nreturn NavigationResult.Quit();", MarkupSchema.Default);
/// </code>
/// </example>
internal static class CvwDocumentParser
{
    private const string ModelDirective = "@model ";
    private const string UsingDirective = "@using ";

    /// <summary>
    /// Parses <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The contents of the <c>.cvw</c> file.</param>
    /// <param name="schema">The known markup elements.</param>
    /// <returns>The parsed document, including any diagnostics.</returns>
    public static CvwDocument Parse(string text, MarkupSchema schema)
    {
        var diagnostics = new List<ParseDiagnostic>();
        var usings = new List<DirectiveValue>();
        DirectiveValue? model = null;
        var position = 0;

        while (position < text.Length)
        {
            var lineEnd = text.IndexOf('\n', position);
            if (lineEnd < 0)
                lineEnd = text.Length;

            var line = text.Substring(position, lineEnd - position);
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                position = lineEnd + 1;
                continue;
            }

            if (trimmed.StartsWith(ModelDirective, StringComparison.Ordinal))
            {
                model = ReadArgument(text, position, line, ModelDirective);
                position = lineEnd + 1;
                continue;
            }

            if (trimmed.StartsWith(UsingDirective, StringComparison.Ordinal))
            {
                usings.Add(ReadArgument(text, position, line, UsingDirective));
                position = lineEnd + 1;
                continue;
            }

            break; // first body line
        }

        if (position > text.Length)
            position = text.Length;

        var body = new MarkupParser(text, position, schema, diagnostics).ParseBody();
        return new CvwDocument(text, model, usings, body, diagnostics);
    }

    /// <summary>Extracts the trimmed argument of a directive line together with its exact source span.</summary>
    private static DirectiveValue ReadArgument(string text, int lineStart, string line, string directive)
    {
        var start = lineStart + line.IndexOf(directive, StringComparison.Ordinal) + directive.Length;
        var end = lineStart + line.TrimEnd().Length;

        while (start < end && char.IsWhiteSpace(text[start]))
            start++;

        return new DirectiveValue(text.Substring(start, end - start), SourceSpan.FromBounds(start, end));
    }
}
