#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConsoleMVC.Generators.Diagnostics;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Parsing;

/// <summary>
/// Parses the body of a <c>.cvw</c> file: C# code with embedded C-View Markup elements.
/// </summary>
/// <remarks>
/// <para>
/// The parser alternates between two modes (see <c>docs/design/c-view-markup.md</c> §2 and §6):
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Code mode</b> copies C# verbatim into <see cref="CodeNode"/>s, skipping strings and comments, and switches
///     to markup when <c>&lt;</c> followed by a known tag name appears where a statement may begin
///     (start of code, or after <c>;</c>, <c>{</c>, <c>}</c> or <c>:</c>).
///   </description></item>
///   <item><description>
///     <b>Markup mode</b> parses an element's attributes and content. Inside content, <c>@</c> introduces
///     expressions, code blocks and control flow, whose bodies are parsed in code mode again.
///   </description></item>
/// </list>
/// </remarks>
internal sealed class MarkupParser
{
    /// <summary>Keywords that start control flow after <c>@</c> inside markup.</summary>
    private static readonly HashSet<string> ControlKeywords = new(StringComparer.Ordinal)
    {
        "if", "foreach", "for", "while", "switch", "do", "try", "lock"
    };

    private readonly string _text;
    /// <summary>The deepest element nesting accepted before parsing stops with CVW113 (guards against stack overflow).</summary>
    public const int MaxNestingDepth = 100;

    private readonly MarkupSchema _schema;
    private readonly List<ParseDiagnostic> _diagnostics;
    private int _position;
    private int _depth;
    private bool _depthReported;

    /// <summary>Offset just past the most recent expression, so <c>@a@b</c> is not mistaken for an e-mail address.</summary>
    private int _lastExpressionEnd = -1;

    /// <summary>
    /// Creates a parser.
    /// </summary>
    /// <param name="text">The full <c>.cvw</c> text (offsets in nodes refer to it).</param>
    /// <param name="start">Offset where the body begins (after the directives).</param>
    /// <param name="schema">The known elements.</param>
    /// <param name="diagnostics">Receives parse errors.</param>
    public MarkupParser(string text, int start, MarkupSchema schema, List<ParseDiagnostic> diagnostics)
    {
        _text = text;
        _position = start;
        _schema = schema;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Parses from the start offset to the end of the text in code mode.
    /// </summary>
    /// <returns>The body nodes.</returns>
    public List<Node> ParseBody() => ParseCode(container: null, stopAtUnmatchedBrace: false, out _);

    // ------------------------------------------------------------------ code mode

    /// <summary>
    /// Parses C# code (with embedded markup) until the end of the text or, when
    /// <paramref name="stopAtUnmatchedBrace"/> is set, until a <c>}</c> that closes the enclosing block.
    /// The closing brace is not consumed.
    /// </summary>
    /// <param name="container">
    /// The element whose content this code belongs to (e.g. the <c>&lt;table&gt;</c> around an <c>@foreach</c>),
    /// or <see langword="null"/> at the top level of the view. Used to validate where elements appear.
    /// </param>
    /// <param name="stopAtUnmatchedBrace">Whether to stop at the brace closing the enclosing block.</param>
    /// <param name="foundClosingBrace">Whether that closing brace was found.</param>
    private List<Node> ParseCode(ElementSchema? container, bool stopAtUnmatchedBrace, out bool foundClosingBrace)
    {
        var nodes = new List<Node>();
        var codeStart = _position;
        var depth = 0;
        var lastSignificant = '\0';
        foundClosingBrace = false;

        while (_position < _text.Length)
        {
            var c = _text[_position];

            if (char.IsWhiteSpace(c))
            {
                _position++;
                continue;
            }

            // Preprocessor directive (#region, #if …): copied as-is, never lexed, and it does not change whether
            // the next line starts a statement
            if (c == '#' && IsFirstOnLine(_position))
            {
                var lineEnd = _text.IndexOf('\n', _position);
                _position = lineEnd < 0 ? _text.Length : lineEnd;
                continue;
            }

            // Razor-style comment @* … *@ — removed from the generated code
            if (c == '@' && Peek(1) == '*')
            {
                AddCode(nodes, codeStart, _position);
                SkipComment("@*", "*@", "@* comment *@");
                codeStart = _position;
                continue;
            }

            if (c is '/' or '\'' or '"' or '@' or '$')
            {
                var skip = CSharpLexer.Skip(_text, _position);
                if (skip.Kind != TriviaKind.None)
                {
                    if (skip.Unterminated)
                        Report(CvwDiagnostics.Unterminated, SourceSpan.FromBounds(_position, skip.End), skip.Description);

                    if (skip.Kind == TriviaKind.Literal)
                        lastSignificant = '"';

                    _position = skip.End;
                    continue;
                }
            }

            // A markup comment where a statement may begin (C# never starts a statement with "<!--") is dropped
            if (c == '<' && IsStatementStart(lastSignificant) && StartsWith("<!--"))
            {
                AddCode(nodes, codeStart, _position);
                SkipComment("<!--", "-->", "markup comment");
                codeStart = _position;
                continue;
            }

            if (c == '<' && IsStatementStart(lastSignificant) && TryPeekTag(_position, out _, out var known) && known)
            {
                AddCode(nodes, codeStart, _position);

                var elementStart = _position;
                var element = ParseElement();
                if (CheckPlacement(container, element.Schema, elementStart))
                    nodes.Add(element);

                codeStart = _position;
                lastSignificant = ';';
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                if (depth == 0 && stopAtUnmatchedBrace)
                {
                    foundClosingBrace = true;
                    break;
                }

                depth--;
            }

            lastSignificant = c;
            _position++;
        }

        AddCode(nodes, codeStart, _position);
        return nodes;
    }

    /// <summary>Markup may only begin where a C# statement may begin.</summary>
    private static bool IsStatementStart(char lastSignificant) =>
        lastSignificant is '\0' or ';' or '{' or '}' or ':';

    private void AddCode(List<Node> nodes, int start, int end)
    {
        if (end > start)
            nodes.Add(new CodeNode(_text.Substring(start, end - start), SourceSpan.FromBounds(start, end)));
    }

    // ------------------------------------------------------------------ elements

    /// <summary>
    /// Parses an element starting at the <c>&lt;</c> under the cursor. The tag must be known.
    /// </summary>
    private ElementNode ParseElement()
    {
        _depth++;
        try
        {
            // Rules over several attributes and the children, now that both are known (ElementRules)
            var element = ParseElementCore();
            ElementRules.Check(element, _diagnostics);
            return element;
        }
        finally
        {
            _depth--;
        }
    }

    private ElementNode ParseElementCore()
    {
        var start = _position;

        if (_depth > MaxNestingDepth)
        {
            if (!_depthReported)
                Report(CvwDiagnostics.NestingTooDeep, new SourceSpan(start, 1), MaxNestingDepth);

            _depthReported = true;
            _position = _text.Length;
            _schema.TryGetElement(MarkupSchema.TextTag, out var placeholder);
            return new ElementNode(placeholder, new SourceSpan(start, 0), Array.Empty<AttributeNode>(), Array.Empty<Node>());
        }

        _position++; // '<'
        var tag = ReadName();
        _schema.TryGetElement(tag, out var schema);

        var attributes = ParseAttributes(schema, out var selfClosing, out var tagClosed);
        ValidateElement(schema, attributes, new SourceSpan(start, tag.Length + 1));

        if (!tagClosed || selfClosing || schema.Content == ContentModel.Void)
        {
            SkipVoidClosingTag(tag);
            return new ElementNode(schema, SourceSpan.FromBounds(start, _position), attributes, Array.Empty<Node>());
        }

        var children = ParseContent(schema);

        if (_position >= _text.Length)
        {
            Report(CvwDiagnostics.UnclosedElement, new SourceSpan(start, tag.Length + 1),
                $"<{tag}> is never closed. Add </{tag}>.");
            return new ElementNode(schema, SourceSpan.FromBounds(start, _position), attributes, children);
        }

        // At "</"
        var closeStart = _position;
        _position += 2;
        var closeName = ReadName();
        SkipWhitespace();

        if (Peek() == '>')
            _position++;
        else
            Report(CvwDiagnostics.MalformedTag, new SourceSpan(closeStart, _position - closeStart), $"expected '>' to end </{closeName}");

        if (!string.Equals(closeName, tag, StringComparison.Ordinal))
        {
            Report(CvwDiagnostics.UnclosedElement, SourceSpan.FromBounds(closeStart, _position),
                $"Expected </{tag}> to close the <{tag}> element, but found </{closeName}>.");
        }

        return new ElementNode(schema, SourceSpan.FromBounds(start, _position), attributes, children);
    }

    /// <summary>Accepts an optional redundant closing tag after a void element, e.g. <c>&lt;br&gt;&lt;/br&gt;</c>.</summary>
    private void SkipVoidClosingTag(string tag)
    {
        var closing = "</" + tag + ">";

        // <br></br> or <br>   </br>: an empty element written with a closing tag
        var next = _position;
        while (next < _text.Length && char.IsWhiteSpace(_text[next]))
            next++;

        if (string.CompareOrdinal(_text, next, closing, 0, closing.Length) == 0)
        {
            _position = next + closing.Length;
            return;
        }

        // <separator>text</separator> on one line: the content would otherwise be compiled as C#
        var lineEnd = _text.IndexOf('\n', _position);
        var close = _text.IndexOf(closing, _position, (lineEnd < 0 ? _text.Length : lineEnd) - _position, StringComparison.Ordinal);
        if (close < 0)
            return;

        Report(CvwDiagnostics.ElementNotAllowed, SourceSpan.FromBounds(_position, close + closing.Length),
            $"<{tag}> cannot have content; write <{tag} />");
        _position = close + closing.Length;
    }

    /// <summary>
    /// Parses attributes up to and including <c>&gt;</c> or <c>/&gt;</c>.
    /// </summary>
    private List<AttributeNode> ParseAttributes(ElementSchema schema, out bool selfClosing, out bool tagClosed)
    {
        var attributes = new List<AttributeNode>();
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        selfClosing = false;
        tagClosed = false;

        while (true)
        {
            SkipWhitespace();

            if (_position >= _text.Length)
            {
                Report(CvwDiagnostics.Unterminated, new SourceSpan(_position, 0), $"<{schema.Tag}> tag (expected '>')");
                return attributes;
            }

            if (Peek() == '/' && Peek(1) == '>')
            {
                _position += 2;
                selfClosing = true;
                tagClosed = true;
                return attributes;
            }

            if (Peek() == '>')
            {
                _position++;
                tagClosed = true;
                return attributes;
            }

            var attributeStart = _position;
            var name = ReadName();

            if (name.Length == 0)
            {
                Report(CvwDiagnostics.MalformedTag, new SourceSpan(_position, 1), $"unexpected character '{Peek()}' in <{schema.Tag}>");
                var end = _text.IndexOf('>', _position);
                _position = end < 0 ? _text.Length : end + 1;
                tagClosed = end >= 0;
                return attributes;
            }

            SkipWhitespace();
            List<AttributeValuePart>? parts = null;
            if (Peek() == '=')
            {
                _position++;
                SkipWhitespace();
                parts = ParseAttributeValue();
            }

            var span = SourceSpan.FromBounds(attributeStart, _position);
            var attribute = BindAttribute(schema, name, parts, span, new SourceSpan(attributeStart, name.Length), seenProperties);
            if (attribute is not null)
                attributes.Add(attribute);
        }
    }

    /// <summary>
    /// Validates an attribute against the element schema and translates literal values.
    /// Returns <see langword="null"/> (after reporting) when the attribute is unusable.
    /// </summary>
    private AttributeNode? BindAttribute(ElementSchema element, string name, List<AttributeValuePart>? parts,
        SourceSpan span, SourceSpan nameSpan, HashSet<string> seenProperties)
    {
        if (!element.TryGetAttribute(name, out var schema))
        {
            Report(CvwDiagnostics.UnknownAttribute, nameSpan, element.Tag, name, element.AttributeList);
            return null;
        }

        if (!seenProperties.Add(schema.Property))
        {
            Report(CvwDiagnostics.DuplicateAttribute, nameSpan, name, element.Tag);
            return null;
        }

        // Valueless flag, e.g. <text bold>
        if (parts is null)
        {
            if (schema.Type.AllowsFlag && schema.Type.TryTranslateLiteral(null, out var flagCode, out _))
                return new AttributeNode(schema, Array.Empty<AttributeValuePart>(), span, flagCode);

            Report(CvwDiagnostics.InvalidAttributeValue, nameSpan, "", name, "a value is required (" + schema.Type.Description + ")");
            return null;
        }

        if (parts.All(p => !p.IsExpression))
        {
            var literal = string.Concat(parts.Select(p => p.Text));
            if (schema.Type.TryTranslateLiteral(literal, out var code, out var error))
                return new AttributeNode(schema, parts, span, code);

            var valueSpan = parts.Count > 0 ? SourceSpan.FromBounds(parts[0].Span.Start, parts[parts.Count - 1].Span.End) : span;
            Report(CvwDiagnostics.InvalidAttributeValue, valueSpan, literal, name, error);
            return null;
        }

        if (parts.Count > 1 && !schema.Type.AllowsInterpolation)
        {
            Report(CvwDiagnostics.InvalidAttributeValue, span, _text.Substring(span.Start, span.Length), name,
                "text and expressions can only be mixed in text attributes; use a single @(expression)");
            return null;
        }

        return new AttributeNode(schema, parts, span, literalCode: null);
    }

    /// <summary>
    /// Parses an attribute value after <c>=</c>: a quoted string (which may contain <c>@</c> expressions),
    /// an unquoted <c>@expression</c>, or an unquoted literal.
    /// </summary>
    private List<AttributeValuePart> ParseAttributeValue()
    {
        var parts = new List<AttributeValuePart>();
        var c = Peek();

        if (c == '@' && (Peek(1) == '(' || IsIdentifierStart(Peek(1))))
        {
            var expression = ParseExpressionAfterAt();
            if (expression is not null)
                parts.Add(new AttributeValuePart(true, expression.Code, expression.Span));
            return parts;
        }

        if (c is not ('"' or '\''))
        {
            var start = _position;
            var value = new StringBuilder();

            while (_position < _text.Length && !char.IsWhiteSpace(Peek()) && Peek() != '>' && !(Peek() == '/' && Peek(1) == '>'))
            {
                if (Peek() == '&' && CharacterEntities.TryDecode(_text, _position, out var entity, out var entityLength))
                {
                    value.Append(entity);
                    _position += entityLength;
                }
                else
                {
                    value.Append(Peek());
                    _position++;
                }
            }

            parts.Add(new AttributeValuePart(false, value.ToString(), SourceSpan.FromBounds(start, _position)));
            return parts;
        }

        var quote = c;
        var valueStart = ++_position;
        var literal = new StringBuilder();
        var literalStart = _position;

        void FlushLiteral()
        {
            if (literal.Length > 0)
                parts.Add(new AttributeValuePart(false, literal.ToString(), SourceSpan.FromBounds(literalStart, _position)));
            literal.Clear();
        }

        while (_position < _text.Length && Peek() != quote)
        {
            if (Peek() == '@' && Peek(1) == '@')
            {
                literal.Append('@');
                _position += 2;
            }
            else if (Peek() == '@' && (Peek(1) == '(' || IsIdentifierStart(Peek(1))) && !IsEmailAt(_position))
            {
                FlushLiteral();
                var expression = ParseExpressionAfterAt();
                if (expression is not null)
                    parts.Add(new AttributeValuePart(true, expression.Code, expression.Span));
                literalStart = _position;
            }
            else if (Peek() == '&' && CharacterEntities.TryDecode(_text, _position, out var decoded, out var length))
            {
                literal.Append(decoded);
                _position += length;
            }
            else
            {
                literal.Append(Peek());
                _position++;
            }
        }

        FlushLiteral();

        if (_position >= _text.Length)
        {
            Report(CvwDiagnostics.Unterminated, SourceSpan.FromBounds(valueStart - 1, _position), "attribute value (missing closing quote)");
            return parts;
        }

        _position++; // closing quote

        if (parts.Count == 0)
            parts.Add(new AttributeValuePart(false, "", new SourceSpan(valueStart, 0)));

        return parts;
    }

    // ------------------------------------------------------------------ content

    /// <summary>
    /// Parses element content until <c>&lt;/</c> or the end of the text.
    /// </summary>
    private List<Node> ParseContent(ElementSchema element)
    {
        var nodes = new List<Node>();
        var text = new StringBuilder();
        var textStart = _position;

        void FlushText()
        {
            if (element.Content == ContentModel.Elements)
            {
                // Only child elements may appear here; whitespace between them is ignored
                if (text.ToString().Trim().Length > 0)
                    Report(CvwDiagnostics.TextNotAllowed, SourceSpan.FromBounds(textStart, _position), element.Tag, element.ChildList);
            }
            else if (text.Length > 0)
            {
                nodes.Add(new TextNode(text.ToString(), SourceSpan.FromBounds(textStart, _position)));
            }

            text.Clear();
        }

        while (_position < _text.Length)
        {
            var c = Peek();

            if (c == '<' && Peek(1) == '/')
                break;

            if (c == '<' && StartsWith("<!--"))
            {
                // Comments vanish without splitting the surrounding text, so whitespace collapses across them
                SkipComment("<!--", "-->", "markup comment");
                continue;
            }

            if (c == '<' && TryPeekTag(_position, out var tag, out var known))
            {
                FlushText();
                ParseChildElement(element, tag, known, nodes);
                textStart = _position;
                continue;
            }

            if (c == '@')
            {
                if (TryHandleAt(element, nodes, text, FlushText))
                    textStart = _position;
                continue;
            }

            if (c == '&' && CharacterEntities.TryDecode(_text, _position, out var decoded, out var length))
            {
                text.Append(decoded);
                _position += length;
                continue;
            }

            text.Append(c);
            _position++;
        }

        FlushText();
        return nodes;
    }

    private void ParseChildElement(ElementSchema parent, string tag, bool known, List<Node> nodes)
    {
        var start = _position;

        if (!known)
        {
            Report(CvwDiagnostics.UnknownElement, new SourceSpan(start, tag.Length + 1), tag, _schema.ElementList);

            // Skip the unknown element, including its content and closing tag, to avoid cascading errors
            var end = _text.IndexOf('>', _position);
            _position = end < 0 ? _text.Length : end + 1;

            if (end > 0 && _text[end - 1] != '/')
            {
                var closing = _text.IndexOf("</" + tag + ">", _position, StringComparison.Ordinal);
                if (closing >= 0)
                    _position = closing + tag.Length + 3;
            }

            return;
        }

        var child = ParseElement();
        if (CheckPlacement(parent, child.Schema, start))
            nodes.Add(child);
    }

    /// <summary>
    /// Handles <c>@</c> inside content. Literal results are appended to <paramref name="text"/>; anything else
    /// flushes the text and adds nodes. Returns <see langword="true"/> when the text buffer was flushed.
    /// </summary>
    private bool TryHandleAt(ElementSchema element, List<Node> nodes, StringBuilder text, Action flushText)
    {
        var next = Peek(1);

        // @@ → literal @
        if (next == '@')
        {
            text.Append('@');
            _position += 2;
            return false;
        }

        // @* comment *@ — vanishes without splitting the surrounding text
        if (next == '*')
        {
            SkipComment("@*", "*@", "@* comment *@");
            return false;
        }

        // name@example.com → literal @
        if (IsEmailAt(_position) || !(next == '(' || next == '{' || IsIdentifierStart(next)))
        {
            text.Append('@');
            _position++;
            return false;
        }

        flushText();

        if (next == '{')
        {
            if (element.Content == ContentModel.Inline)
                Report(CvwDiagnostics.CodeInText, new SourceSpan(_position, 2), "{", element.Tag);

            var start = _position;
            _position += 2;
            var code = ParseCode(element, stopAtUnmatchedBrace: true, out var closed);

            if (!closed)
                Report(CvwDiagnostics.Unterminated, new SourceSpan(start, 2), "@{ code block (missing '}')");
            else
                _position++; // '}'

            if (element.Content != ContentModel.Inline)
                nodes.AddRange(code);

            return true;
        }

        if (next != '(')
        {
            var keyword = PeekIdentifier(_position + 1);
            if (ControlKeywords.Contains(keyword))
            {
                if (element.Content == ContentModel.Inline)
                {
                    Report(CvwDiagnostics.CodeInText, new SourceSpan(_position, keyword.Length + 1), keyword, element.Tag);
                    ParseControlFlow(keyword, new List<Node>(), element);
                }
                else
                {
                    ParseControlFlow(keyword, nodes, element);
                }

                return true;
            }
        }

        var expressionStart = _position;
        var expression = ParseExpressionAfterAt();

        if (element.Content == ContentModel.Elements)
            Report(CvwDiagnostics.TextNotAllowed, SourceSpan.FromBounds(expressionStart, _position), element.Tag, element.ChildList);
        else if (expression is not null)
            nodes.Add(expression);

        return true;
    }

    /// <summary>
    /// Parses <c>@if (…) { … } else { … }</c> and similar statements. The header and closing braces become
    /// <see cref="CodeNode"/>s; block bodies are parsed in code mode so they may contain markup.
    /// </summary>
    private void ParseControlFlow(string keyword, List<Node> nodes, ElementSchema container)
    {
        _position++; // '@'
        var segmentStart = _position;

        while (true)
        {
            var openBrace = FindBlockStart(_position);
            if (openBrace < 0)
            {
                Report(CvwDiagnostics.Unterminated, new SourceSpan(segmentStart, keyword.Length), $"@{keyword} statement (expected '{{')");
                _position = _text.Length;
                return;
            }

            AddCode(nodes, segmentStart, openBrace + 1);
            _position = openBrace + 1;

            nodes.AddRange(ParseCode(container, stopAtUnmatchedBrace: true, out var closed));
            if (!closed)
            {
                Report(CvwDiagnostics.Unterminated, new SourceSpan(openBrace, 1), $"@{keyword} block (missing '}}')");
                return;
            }

            AddCode(nodes, _position, _position + 1);
            _position++; // '}'

            // Continuations: else / else if, catch / finally, do … while (…);
            var afterBlock = _position;
            SkipWhitespaceAndComments();
            var continuation = PeekIdentifier(_position);

            if ((keyword == "if" && continuation == "else")
                || (keyword == "try" && continuation is "catch" or "finally"))
            {
                segmentStart = afterBlock;
                continue;
            }

            if (keyword == "do" && continuation == "while")
            {
                var semicolon = FindStatementEnd(_position);
                if (semicolon < 0)
                {
                    Report(CvwDiagnostics.Unterminated, new SourceSpan(_position, 5), "do … while statement (missing ';')");
                    _position = _text.Length;
                    return;
                }

                AddCode(nodes, afterBlock, semicolon + 1);
                _position = semicolon + 1;
                return;
            }

            _position = afterBlock;
            return;
        }
    }

    /// <summary>
    /// Parses an expression after <c>@</c> at the cursor: explicit <c>@( … )</c> or implicit <c>@a.b(c)[d]</c>.
    /// </summary>
    private ExpressionNode? ParseExpressionAfterAt()
    {
        var at = _position;
        _position++; // '@'

        if (Peek() == '(')
        {
            var close = CSharpLexer.FindMatching(_text, _position, '(', ')');
            if (close < 0)
            {
                Report(CvwDiagnostics.Unterminated, new SourceSpan(at, 2), "@( expression (missing ')')");
                _position = _text.Length;
                return null;
            }

            var start = _position + 1;
            _position = close + 1;
            _lastExpressionEnd = _position;

            var code = _text.Substring(start, close - start);
            if (code.Trim().Length == 0)
            {
                Report(CvwDiagnostics.EmptyExpression, SourceSpan.FromBounds(at, _position));
                return null;
            }

            return new ExpressionNode(code, SourceSpan.FromBounds(start, close));
        }

        var expressionStart = _position;
        ScanImplicitExpression();
        _lastExpressionEnd = _position;
        return new ExpressionNode(_text.Substring(expressionStart, _position - expressionStart),
            SourceSpan.FromBounds(expressionStart, _position));
    }

    /// <summary>
    /// Scans an implicit expression: an identifier followed by member access (<c>.x</c>, <c>?.x</c>, <c>!.x</c>),
    /// calls <c>( … )</c> and indexers <c>[ … ]</c>. A trailing <c>.</c> (end of a sentence) is not included.
    /// </summary>
    private void ScanImplicitExpression()
    {
        ReadIdentifier();

        while (_position < _text.Length)
        {
            var c = Peek();

            if (c is '(' or '[')
            {
                var close = CSharpLexer.FindMatching(_text, _position, c, c == '(' ? ')' : ']');
                if (close < 0)
                {
                    Report(CvwDiagnostics.Unterminated, new SourceSpan(_position, 1), "expression (missing '" + (c == '(' ? ')' : ']') + "')");
                    _position = _text.Length;
                    return;
                }

                _position = close + 1;
            }
            else if (c == '.' && IsIdentifierStart(Peek(1)))
            {
                _position++;
                ReadIdentifier();
            }
            else if ((c is '?' or '!') && Peek(1) == '.' && IsIdentifierStart(Peek(2)))
            {
                _position += 2;
                ReadIdentifier();
            }
            else if (c == '?' && Peek(1) == '[')
            {
                _position++;
            }
            else
            {
                return;
            }
        }
    }

    // ------------------------------------------------------------------ validation

    /// <summary>
    /// Reports CVW103 when <paramref name="child"/> may not appear in <paramref name="container"/>
    /// (<see langword="null"/> meaning the top level of the view).
    /// </summary>
    /// <returns><see langword="true"/> when the placement is valid.</returns>
    private bool CheckPlacement(ElementSchema? container, ElementSchema child, int tagStart)
    {
        var error = container is null ? child.TopLevelPlacementError() : container.ChildPlacementError(child);
        if (error is null)
            return true;

        Report(CvwDiagnostics.ElementNotAllowed, new SourceSpan(tagStart, child.Tag.Length + 1), error);
        return false;
    }

    /// <summary>Reports missing required attributes (CVW111) and element-specific rules (CVW109).</summary>
    private void ValidateElement(ElementSchema schema, IReadOnlyList<AttributeNode> attributes, SourceSpan tagSpan)
    {
        foreach (var required in schema.RequiredAttributes)
        {
            if (!attributes.Any(a => a.Schema.Property == required.Property))
                Report(CvwDiagnostics.MissingAttribute, tagSpan, schema.Tag, required.Name);
        }

        var error = schema.Validate(attributes);
        if (error is not null)
            Report(CvwDiagnostics.InvalidElement, tagSpan, error);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Checks whether <c>&lt;name</c> starts at <paramref name="position"/>: a lower-case ASCII letter after
    /// <c>&lt;</c>, followed by whitespace, <c>&gt;</c> or <c>/</c> after the name.
    /// </summary>
    private bool TryPeekTag(int position, out string tag, out bool known)
    {
        tag = "";
        known = false;

        var first = position + 1 < _text.Length ? _text[position + 1] : '\0';
        if (first is < 'a' or > 'z')
            return false;

        var end = position + 1;
        while (end < _text.Length && IsNameCharacter(_text[end]))
            end++;

        var after = end < _text.Length ? _text[end] : '\0';
        if (!(char.IsWhiteSpace(after) || after == '>' || after == '/' || after == '\0'))
            return false;

        tag = _text.Substring(position + 1, end - position - 1);
        known = _schema.TryGetElement(tag, out _);
        return true;
    }

    /// <summary>Finds the <c>{</c> that opens a statement block, skipping parenthesised headers, strings and comments.</summary>
    private int FindBlockStart(int from)
    {
        var parentheses = 0;
        var i = from;

        while (i < _text.Length)
        {
            var skip = CSharpLexer.Skip(_text, i);
            if (skip.Kind != TriviaKind.None)
            {
                i = skip.End;
                continue;
            }

            var c = _text[i];
            if (c == '(')
                parentheses++;
            else if (c == ')')
                parentheses--;
            else if (c == '{' && parentheses == 0)
                return i;
            else if ((c == '}' || c == ';') && parentheses == 0)
                return -1;

            i++;
        }

        return -1;
    }

    /// <summary>Finds the <c>;</c> ending a statement, skipping parentheses, strings and comments.</summary>
    private int FindStatementEnd(int from)
    {
        var parentheses = 0;
        for (var i = from; i < _text.Length; i++)
        {
            var skip = CSharpLexer.Skip(_text, i);
            if (skip.Kind != TriviaKind.None)
            {
                i = skip.End - 1;
                continue;
            }

            var c = _text[i];
            if (c == '(') parentheses++;
            else if (c == ')') parentheses--;
            else if (c == ';' && parentheses == 0) return i;
        }

        return -1;
    }

    private void SkipComment(string open, string close, string description)
    {
        var end = _text.IndexOf(close, _position + open.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            Report(CvwDiagnostics.Unterminated, new SourceSpan(_position, open.Length), description);
            _position = _text.Length;
            return;
        }

        _position = end + close.Length;
    }

    private string ReadName()
    {
        var start = _position;
        while (_position < _text.Length && IsNameCharacter(_text[_position]))
            _position++;

        return _text.Substring(start, _position - start);
    }

    private void ReadIdentifier()
    {
        while (_position < _text.Length && IsIdentifierPart(_text[_position]))
            _position++;
    }

    private string PeekIdentifier(int position)
    {
        var end = position;
        while (end < _text.Length && IsIdentifierPart(_text[end]))
            end++;

        return _text.Substring(position, end - position);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the <c>@</c> at <paramref name="position"/> is part of an e-mail
    /// address (preceded by a letter or digit that is not the end of an expression).
    /// </summary>
    private bool IsEmailAt(int position) =>
        position > 0 && char.IsLetterOrDigit(_text[position - 1]) && position != _lastExpressionEnd;

    /// <summary>Returns <see langword="true"/> when only spaces or tabs precede <paramref name="position"/> on its line.</summary>
    private bool IsFirstOnLine(int position)
    {
        for (var i = position - 1; i >= 0; i--)
        {
            var c = _text[i];
            if (c == '\n')
                return true;
            if (c is not (' ' or '\t' or '\r'))
                return false;
        }

        return true;
    }

    /// <summary>Skips whitespace and C# comments (used between <c>}</c> and <c>else</c>, <c>catch</c>, …).</summary>
    private void SkipWhitespaceAndComments()
    {
        while (true)
        {
            SkipWhitespace();

            if (Peek() == '/' && Peek(1) is '/' or '*')
            {
                _position = CSharpLexer.Skip(_text, _position).End;
                continue;
            }

            return;
        }
    }

    private void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            _position++;
    }

    private bool StartsWith(string value) =>
        string.CompareOrdinal(_text, _position, value, 0, value.Length) == 0;

    private char Peek(int offset = 0)
    {
        var index = _position + offset;
        return index >= 0 && index < _text.Length ? _text[index] : '\0';
    }

    private void Report(Microsoft.CodeAnalysis.DiagnosticDescriptor descriptor, SourceSpan span, params object[] arguments) =>
        _diagnostics.Add(new ParseDiagnostic(descriptor, span, arguments));

    private static bool IsNameCharacter(char c) => char.IsLetterOrDigit(c) || c is '-' or '_';

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
