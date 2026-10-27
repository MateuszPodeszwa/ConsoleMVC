#nullable enable

using System.Collections.Generic;
using ConsoleMVC.Generators.Schema;

namespace ConsoleMVC.Generators.Syntax;

/// <summary>
/// Base class of every node in a parsed <c>.cvw</c> body.
/// </summary>
/// <remarks>
/// A body is a flat list of <see cref="CodeNode"/>s (verbatim C#) and <see cref="ElementNode"/>s
/// (markup). Element children may additionally contain <see cref="TextNode"/>s and
/// <see cref="ExpressionNode"/>s, plus <see cref="CodeNode"/>s for control flow such as
/// <c>@foreach</c>, whose bodies may in turn contain elements.
/// </remarks>
internal abstract class Node
{
    /// <summary>
    /// Creates a node.
    /// </summary>
    /// <param name="span">Where the node's content appears in the <c>.cvw</c> file.</param>
    protected Node(SourceSpan span)
    {
        Span = span;
    }

    /// <summary>Gets where the node's content appears in the <c>.cvw</c> file.</summary>
    public SourceSpan Span { get; }
}

/// <summary>
/// Verbatim C# code, emitted unchanged with a <c>#line</c> mapping back to <see cref="Node.Span"/>.
/// </summary>
/// <example><c>Console.Write("Name: ");</c></example>
internal sealed class CodeNode : Node
{
    /// <summary>
    /// Creates a code node.
    /// </summary>
    /// <param name="code">The C# code.</param>
    /// <param name="span">Location of the code in the source.</param>
    public CodeNode(string code, SourceSpan span) : base(span)
    {
        Code = code;
    }

    /// <summary>Gets the C# code.</summary>
    public string Code { get; }
}

/// <summary>
/// Literal text inside an element, with entities already decoded (whitespace not yet normalised).
/// </summary>
/// <example><c>Welcome, </c></example>
internal sealed class TextNode : Node
{
    /// <summary>
    /// Creates a text node.
    /// </summary>
    /// <param name="text">The decoded text.</param>
    /// <param name="span">Location of the raw text in the source.</param>
    public TextNode(string text, SourceSpan span) : base(span)
    {
        Text = text;
    }

    /// <summary>Gets the decoded text.</summary>
    public string Text { get; }
}

/// <summary>
/// A C# expression whose value is written into the surrounding text (<c>@Model.Name</c> or <c>@(a + b)</c>).
/// </summary>
internal sealed class ExpressionNode : Node
{
    /// <summary>
    /// Creates an expression node.
    /// </summary>
    /// <param name="code">The expression code without the leading <c>@</c> or surrounding parentheses.</param>
    /// <param name="span">Location of <paramref name="code"/> in the source.</param>
    public ExpressionNode(string code, SourceSpan span) : base(span)
    {
        Code = code;
    }

    /// <summary>Gets the expression code.</summary>
    public string Code { get; }
}

/// <summary>
/// A markup element such as <c>&lt;box title="Menu"&gt;…&lt;/box&gt;</c>.
/// </summary>
internal sealed class ElementNode : Node
{
    /// <summary>
    /// Creates an element node.
    /// </summary>
    /// <param name="schema">The schema describing this element.</param>
    /// <param name="span">Location of the whole element, from <c>&lt;</c> to the end of the closing tag.</param>
    /// <param name="attributes">The parsed attributes.</param>
    /// <param name="children">The child nodes (empty for void elements).</param>
    public ElementNode(ElementSchema schema, SourceSpan span, IReadOnlyList<AttributeNode> attributes, IReadOnlyList<Node> children)
        : base(span)
    {
        Schema = schema;
        Attributes = attributes;
        Children = children;
    }

    /// <summary>Gets the schema describing this element.</summary>
    public ElementSchema Schema { get; }

    /// <summary>Gets the parsed attributes in source order.</summary>
    public IReadOnlyList<AttributeNode> Attributes { get; }

    /// <summary>Gets the child nodes in source order.</summary>
    public IReadOnlyList<Node> Children { get; }
}

/// <summary>
/// One part of an attribute value: literal text or an embedded expression.
/// </summary>
internal sealed class AttributeValuePart
{
    /// <summary>
    /// Creates a value part.
    /// </summary>
    /// <param name="isExpression"><see langword="true"/> when <paramref name="text"/> is C# code.</param>
    /// <param name="text">The decoded literal or the expression code.</param>
    /// <param name="span">Location of the part in the source.</param>
    public AttributeValuePart(bool isExpression, string text, SourceSpan span)
    {
        IsExpression = isExpression;
        Text = text;
        Span = span;
    }

    /// <summary>Gets a value indicating whether <see cref="Text"/> is C# code.</summary>
    public bool IsExpression { get; }

    /// <summary>Gets the decoded literal or the expression code.</summary>
    public string Text { get; }

    /// <summary>Gets the location of the part in the source.</summary>
    public SourceSpan Span { get; }
}

/// <summary>
/// An attribute such as <c>width="40"</c>, <c>title=@Model.Title</c> or the flag <c>bold</c>.
/// </summary>
internal sealed class AttributeNode
{
    /// <summary>
    /// Creates an attribute node.
    /// </summary>
    /// <param name="schema">The attribute's schema.</param>
    /// <param name="parts">The value parts; empty for a valueless flag attribute.</param>
    /// <param name="span">Location of the whole attribute.</param>
    /// <param name="literalCode">The C# code for a literal value, already validated; otherwise <see langword="null"/>.</param>
    public AttributeNode(AttributeSchema schema, IReadOnlyList<AttributeValuePart> parts, SourceSpan span, string? literalCode)
    {
        Schema = schema;
        Parts = parts;
        Span = span;
        LiteralCode = literalCode;
    }

    /// <summary>Gets the attribute's schema.</summary>
    public AttributeSchema Schema { get; }

    /// <summary>Gets the value parts; empty for a valueless flag attribute.</summary>
    public IReadOnlyList<AttributeValuePart> Parts { get; }

    /// <summary>Gets the location of the whole attribute.</summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// Gets the translated C# code when the value is a pure literal (or a flag), e.g.
    /// <c>global::ConsoleMVC.Rendering.Length.Fixed(40)</c>; <see langword="null"/> when the value contains expressions.
    /// </summary>
    public string? LiteralCode { get; }
}
