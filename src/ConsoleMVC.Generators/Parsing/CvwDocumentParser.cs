#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace ConsoleMVC.Generators.Parsing;

/// <summary>
/// Parses a complete <c>.cvw</c> file: the directive preamble (<c>@model</c>, <c>@using</c>, <c>@inject</c>) followed by
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
    private const string ModelDirective = "@model";
    private const string UsingDirective = "@using";
    private const string InjectDirectiveName = "@inject";

    /// <summary>
    /// Names an <c>@inject</c> property may not use: the <c>Render</c> parameter and the members of
    /// <c>ConsoleView</c>, which the property would hide.
    /// </summary>
    private static readonly string[] ReservedInjectNames =
    [
        "Model", "ViewData", "ModelState", "Renderer", "Input", "Form", "Render", "RenderInternal", "ReadInput", "RunMenu"
    ];

    /// <summary>Identifiers starting with this prefix are reserved for generated code.</summary>
    private const string ReservedPrefix = "__cvw_";
    private const string CommentStart = "@*";
    private const string CommentEnd = "*@";

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
        var injects = new List<InjectDirective>();
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

            // @* comment *@ lines (possibly spanning several lines) may appear among the directives
            if (trimmed.StartsWith(CommentStart, StringComparison.Ordinal))
            {
                var commentStart = text.IndexOf(CommentStart, position, StringComparison.Ordinal);
                var commentEnd = text.IndexOf(CommentEnd, commentStart + CommentStart.Length, StringComparison.Ordinal);
                if (commentEnd < 0)
                    break; // unterminated: let the body parser report it

                var afterComment = commentEnd + CommentEnd.Length;
                var restOfLine = text.IndexOf('\n', afterComment);
                if (restOfLine < 0)
                    restOfLine = text.Length;

                if (text.Substring(afterComment, restOfLine - afterComment).Trim().Length > 0)
                    break; // code follows the comment on the same line: the body starts here

                position = restOfLine + 1;
                continue;
            }

            if (IsDirective(trimmed, ModelDirective))
            {
                var value = ReadArgument(text, position, line, ModelDirective);
                if (model is not null)
                    diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.DuplicateModel, value.Span, value.Value));

                model = value;
                position = lineEnd + 1;
                continue;
            }

            if (IsDirective(trimmed, UsingDirective))
            {
                usings.Add(ReadArgument(text, position, line, UsingDirective));
                position = lineEnd + 1;
                continue;
            }

            if (IsDirective(trimmed, InjectDirectiveName) || trimmed == InjectDirectiveName)
            {
                ReadInject(text, position, line, injects, diagnostics);
                position = lineEnd + 1;
                continue;
            }

            break; // first body line
        }

        if (position > text.Length)
            position = text.Length;

        var body = new MarkupParser(text, position, schema, diagnostics).ParseBody();
        return new CvwDocument(text, model, usings, injects, body, diagnostics);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="trimmedLine"/> is <paramref name="directive"/> followed by
    /// whitespace (space or tab) and an argument.
    /// </summary>
    private static bool IsDirective(string trimmedLine, string directive) =>
        trimmedLine.Length > directive.Length
        && trimmedLine.StartsWith(directive, StringComparison.Ordinal)
        && char.IsWhiteSpace(trimmedLine[directive.Length]);

    /// <summary>
    /// Extracts the trimmed argument of a directive line together with its exact source span. A trailing
    /// <c>;</c> (as in <c>@using System.Text;</c>) is ignored.
    /// </summary>
    private static DirectiveValue ReadArgument(string text, int lineStart, string line, string directive)
    {
        var start = lineStart + line.IndexOf(directive, StringComparison.Ordinal) + directive.Length;
        var end = lineStart + line.TrimEnd().Length;

        while (start < end && char.IsWhiteSpace(text[start]))
            start++;

        while (end > start && (text[end - 1] == ';' || char.IsWhiteSpace(text[end - 1])))
            end--;

        return new DirectiveValue(text.Substring(start, end - start), SourceSpan.FromBounds(start, end));
    }

    /// <summary>
    /// Reads an <c>@inject Type Name</c> line. The property name is the last word; everything before it is the type,
    /// so generic types such as <c>IOptions&lt;Dictionary&lt;string, int&gt;&gt;</c> work.
    /// </summary>
    private static void ReadInject(string text, int lineStart, string line, List<InjectDirective> injects,
        List<ParseDiagnostic> diagnostics)
    {
        var argument = line.Trim() == InjectDirectiveName
            ? new DirectiveValue("", SourceSpan.FromBounds(lineStart + line.IndexOf(InjectDirectiveName, StringComparison.Ordinal),
                lineStart + line.IndexOf(InjectDirectiveName, StringComparison.Ordinal) + InjectDirectiveName.Length))
            : ReadArgument(text, lineStart, line, InjectDirectiveName);

        var value = argument.Value;
        var split = value.Length - 1;
        while (split >= 0 && !char.IsWhiteSpace(value[split]))
            split--;

        if (split < 0)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidInject, argument.Span,
                "@inject needs a service type and a property name, e.g. '@inject IGreetingService Greetings'"));
            return;
        }

        var typeEnd = split;
        while (typeEnd > 0 && char.IsWhiteSpace(value[typeEnd - 1]))
            typeEnd--;

        var start = argument.Span.Start;
        var type = new DirectiveValue(value.Substring(0, typeEnd), SourceSpan.FromBounds(start, start + typeEnd));
        var name = new DirectiveValue(value.Substring(split + 1), SourceSpan.FromBounds(start + split + 1, start + value.Length));

        var problem = InjectNameProblem(name.Value);
        if (problem is not null)
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.InvalidInject, name.Span, problem));
            return;
        }

        if (injects.Any(i => i.Name.Value == name.Value))
        {
            diagnostics.Add(new ParseDiagnostic(CvwDiagnostics.DuplicateInject, name.Span, name.Value));
            return;
        }

        injects.Add(new InjectDirective(type, name));
    }

    /// <summary>
    /// Returns why <paramref name="name"/> cannot be used as an <c>@inject</c> property, or <see langword="null"/>
    /// when it can.
    /// </summary>
    private static string? InjectNameProblem(string name)
    {
        if (!SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
            return $"'{name}' is not a valid C# property name";

        if (ReservedInjectNames.Contains(name))
            return $"'{name}' is already a member of the view; choose another property name";

        if (name.StartsWith(ReservedPrefix, StringComparison.Ordinal))
            return $"Names starting with '{ReservedPrefix}' are reserved for generated code";

        return null;
    }
}
