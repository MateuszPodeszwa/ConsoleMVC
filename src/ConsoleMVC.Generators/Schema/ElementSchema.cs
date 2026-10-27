#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// What an element may contain.
/// </summary>
internal enum ContentModel
{
    /// <summary>Nothing — a void element such as <c>&lt;br /&gt;</c>.</summary>
    Void,

    /// <summary>Text, expressions and <c>&lt;br /&gt;</c> — e.g. <c>&lt;text&gt;</c>.</summary>
    Inline,

    /// <summary>Block elements, text (wrapped in an implicit <c>&lt;text&gt;</c>), code blocks and control flow — e.g. <c>&lt;box&gt;</c>.</summary>
    Block
}

/// <summary>
/// One attribute of an element: its markup name, the runtime property it sets and its value type.
/// </summary>
internal sealed class AttributeSchema
{
    /// <summary>
    /// Creates an attribute schema.
    /// </summary>
    /// <param name="name">The markup attribute name (lower case).</param>
    /// <param name="property">The runtime property assigned.</param>
    /// <param name="type">The value type.</param>
    public AttributeSchema(string name, string property, AttributeType type)
    {
        Name = name;
        Property = property;
        Type = type;
    }

    /// <summary>Gets the markup attribute name.</summary>
    public string Name { get; }

    /// <summary>Gets the runtime property assigned by the attribute.</summary>
    public string Property { get; }

    /// <summary>Gets the value type.</summary>
    public AttributeType Type { get; }
}

/// <summary>
/// Describes a markup element: its tag, the runtime class it creates, what it may contain and its attributes.
/// </summary>
internal sealed class ElementSchema
{
    private readonly Dictionary<string, AttributeSchema> _attributes;

    /// <summary>
    /// Creates an element schema.
    /// </summary>
    /// <param name="tag">The tag name (lower case).</param>
    /// <param name="runtimeType">The fully-qualified runtime type, prefixed with <c>global::</c>.</param>
    /// <param name="content">What the element may contain.</param>
    /// <param name="attributes">The element's attributes. Several names may map to the same property (aliases).</param>
    public ElementSchema(string tag, string runtimeType, ContentModel content, IEnumerable<AttributeSchema> attributes)
    {
        Tag = tag;
        RuntimeType = runtimeType;
        Content = content;
        _attributes = attributes.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        AttributeList = string.Join(", ", _attributes.Keys);
    }

    /// <summary>Gets the tag name.</summary>
    public string Tag { get; }

    /// <summary>Gets the fully-qualified runtime type.</summary>
    public string RuntimeType { get; }

    /// <summary>Gets what the element may contain.</summary>
    public ContentModel Content { get; }

    /// <summary>Gets a comma-separated list of valid attribute names, for error messages.</summary>
    public string AttributeList { get; }

    /// <summary>
    /// Looks up an attribute by name (case-insensitive).
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="attribute">The attribute schema when found.</param>
    /// <returns><see langword="true"/> when the element has the attribute.</returns>
    public bool TryGetAttribute(string name, out AttributeSchema attribute) =>
        _attributes.TryGetValue(name, out attribute!);

    /// <summary>
    /// Returns whether <paramref name="child"/> may appear directly inside this element.
    /// </summary>
    /// <param name="child">The candidate child element.</param>
    /// <returns><see langword="true"/> when the child is allowed.</returns>
    public bool Allows(ElementSchema child) => Content switch
    {
        ContentModel.Block => true,
        ContentModel.Inline => child.Tag == MarkupSchema.LineBreakTag,
        _ => false
    };
}
