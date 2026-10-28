#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// What an element may contain.
/// </summary>
internal enum ContentModel
{
    /// <summary>Nothing — a void element such as <c>&lt;br /&gt;</c> or <c>&lt;input /&gt;</c>.</summary>
    Void,

    /// <summary>Text, expressions and <c>&lt;br /&gt;</c> — e.g. <c>&lt;text&gt;</c>, <c>&lt;cell&gt;</c>.</summary>
    Inline,

    /// <summary>Block elements, text (wrapped in an implicit <c>&lt;text&gt;</c>), code blocks and control flow — e.g. <c>&lt;box&gt;</c>.</summary>
    Block,

    /// <summary>Only specific child elements, plus code blocks and control flow — e.g. <c>&lt;table&gt;</c>, <c>&lt;menu&gt;</c>.</summary>
    Elements
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
    /// <param name="required">Whether the element is invalid without this attribute (CVW111).</param>
    public AttributeSchema(string name, string property, AttributeType type, bool required = false)
    {
        Name = name;
        Property = property;
        Type = type;
        Required = required;
    }

    /// <summary>Gets the markup attribute name.</summary>
    public string Name { get; }

    /// <summary>Gets the runtime property assigned by the attribute.</summary>
    public string Property { get; }

    /// <summary>Gets the value type.</summary>
    public AttributeType Type { get; }

    /// <summary>Gets a value indicating whether the attribute must be present.</summary>
    public bool Required { get; }
}

/// <summary>
/// Describes a markup element: its tag, the runtime class it creates, what it may contain, where it may appear,
/// its attributes and the statement generated when it appears at the top level of a view.
/// </summary>
internal sealed class ElementSchema
{
    /// <summary>
    /// The pseudo-property of the <c>class</c> attribute. The emitter turns it into <c>this.Theme.Apply(element, …)</c>
    /// instead of an assignment.
    /// </summary>
    public const string ThemeClassProperty = "@Theme";

    /// <summary>The default top-level statement: render the element immediately.</summary>
    public const string RenderStatement = "this.Renderer.Render({0});";

    private readonly Dictionary<string, AttributeSchema> _attributes;
    private readonly AttributeSchema[] _attributeList;
    private readonly HashSet<string>? _allowedChildren;
    private readonly string[]? _allowedChildList;
    private readonly Func<IReadOnlyList<AttributeNode>, string?>? _validator;
    private readonly string[] _initialisers;
    private readonly string _parentList;

    /// <summary>
    /// Creates an element schema.
    /// </summary>
    /// <param name="tag">The tag name (lower case).</param>
    /// <param name="runtimeType">The fully-qualified runtime type, prefixed with <c>global::</c>.</param>
    /// <param name="content">What the element may contain.</param>
    /// <param name="attributes">The element's attributes. Several names may map to the same property (aliases).</param>
    /// <param name="allowedChildren">For <see cref="ContentModel.Elements"/>: the tags allowed as children.</param>
    /// <param name="requiredParent">The only parent tag the element may appear in, or <see langword="null"/>.</param>
    /// <param name="requiredParents">Several parent tags the element may appear in (instead of <paramref name="requiredParent"/>).</param>
    /// <param name="topLevelOnly">Whether the element may only appear at the top level of a view (interactive elements).</param>
    /// <param name="topLevelStatement">
    /// The statement emitted for the element at the top level; <c>{0}</c> is the element variable.
    /// Defaults to <see cref="RenderStatement"/>.
    /// </param>
    /// <param name="validator">An optional rule over the element's attributes, returning an error message or <see langword="null"/> (CVW109).</param>
    /// <param name="initialisers">
    /// Property assignments emitted right after the element is created, before its attributes, e.g.
    /// <c>"Source = this.ModelState"</c> to connect <c>&lt;errors&gt;</c> to the view's model state.
    /// </param>
    public ElementSchema(string tag, string runtimeType, ContentModel content, IEnumerable<AttributeSchema> attributes,
        IEnumerable<string>? allowedChildren = null, string? requiredParent = null, bool topLevelOnly = false,
        string topLevelStatement = RenderStatement, Func<IReadOnlyList<AttributeNode>, string?>? validator = null,
        IEnumerable<string>? initialisers = null, IEnumerable<string>? requiredParents = null)
    {
        Tag = tag;
        RuntimeType = runtimeType;
        Content = content;
        _attributeList = attributes.ToArray();
        _attributes = _attributeList.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        _allowedChildList = allowedChildren?.ToArray();
        _allowedChildren = _allowedChildList is null ? null : new HashSet<string>(_allowedChildList, StringComparer.Ordinal);
        RequiredParents = requiredParents?.ToArray() ?? (requiredParent is null ? null : [requiredParent]);
        _parentList = RequiredParents is null ? "" : string.Join(" or ", RequiredParents.Select(t => "<" + t + ">"));
        TopLevelOnly = topLevelOnly;
        TopLevelStatement = topLevelStatement;
        _validator = validator;
        _initialisers = initialisers?.ToArray() ?? [];

        AttributeList = string.Join(", ", _attributes.Keys);
        ChildList = _allowedChildren is null ? "" : string.Join(", ", _allowedChildren.Select(t => "<" + t + ">"));
        RequiredAttributes = _attributes.Values.Where(a => a.Required).GroupBy(a => a.Property).Select(g => g.First()).ToArray();
    }

    /// <summary>Gets the tag name.</summary>
    public string Tag { get; }

    /// <summary>Gets the fully-qualified runtime type.</summary>
    public string RuntimeType { get; }

    /// <summary>Gets what the element may contain.</summary>
    public ContentModel Content { get; }

    /// <summary>Gets the parent tags this element may appear in, or <see langword="null"/> when it may appear anywhere.</summary>
    public IReadOnlyList<string>? RequiredParents { get; }

    /// <summary>Gets a value indicating whether the element may only appear at the top level of a view.</summary>
    public bool TopLevelOnly { get; }

    /// <summary>Gets the statement emitted at the top level; <c>{0}</c> is the element variable.</summary>
    public string TopLevelStatement { get; }

    /// <summary>Gets a comma-separated list of valid attribute names, for error messages.</summary>
    public string AttributeList { get; }

    /// <summary>Gets a comma-separated list of allowed child tags, for error messages.</summary>
    public string ChildList { get; }

    /// <summary>Gets the property assignments emitted when the element is created (see the constructor).</summary>
    public IReadOnlyList<string> Initialisers => _initialisers;

    /// <summary>Gets every attribute, in the order they were declared, aliases included.</summary>
    public IReadOnlyList<AttributeSchema> Attributes => _attributeList;

    /// <summary>
    /// Gets the tags allowed as children of a <see cref="ContentModel.Elements"/> element, in declared order, or
    /// <see langword="null"/> when the content model decides.
    /// </summary>
    public IReadOnlyList<string>? AllowedChildren => _allowedChildList;

    /// <summary>Gets the attributes that must be present (one per property).</summary>
    public IReadOnlyList<AttributeSchema> RequiredAttributes { get; }

    /// <summary>
    /// Looks up an attribute by name (case-insensitive).
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="attribute">The attribute schema when found.</param>
    /// <returns><see langword="true"/> when the element has the attribute.</returns>
    public bool TryGetAttribute(string name, out AttributeSchema attribute) =>
        _attributes.TryGetValue(name, out attribute!);

    /// <summary>
    /// Runs the element-specific rule, if any.
    /// </summary>
    /// <param name="attributes">The parsed attributes.</param>
    /// <returns>An error message, or <see langword="null"/> when the element is valid.</returns>
    public string? Validate(IReadOnlyList<AttributeNode> attributes) => _validator?.Invoke(attributes);

    /// <summary>
    /// Explains why this element may not appear at the top level of a view.
    /// </summary>
    /// <returns>An error message, or <see langword="null"/> when the placement is valid.</returns>
    public string? TopLevelPlacementError() =>
        RequiredParents is null ? null : $"<{Tag}> is only allowed inside {_parentList}";

    /// <summary>
    /// Explains why <paramref name="child"/> may not appear directly inside this element.
    /// </summary>
    /// <param name="child">The candidate child element.</param>
    /// <returns>An error message, or <see langword="null"/> when the child is allowed.</returns>
    public string? ChildPlacementError(ElementSchema child)
    {
        if (child.RequiredParents is not null && !child.RequiredParents.Contains(Tag))
            return $"<{child.Tag}> is only allowed inside {child._parentList}";

        if (child.TopLevelOnly)
            return $"<{child.Tag}> must be written at the top level of the view, not inside <{Tag}>";

        var allowed = Content switch
        {
            ContentModel.Block => true,
            ContentModel.Inline => child.Tag == MarkupSchema.LineBreakTag,
            ContentModel.Elements => _allowedChildren?.Contains(child.Tag) == true,
            _ => false
        };

        if (allowed)
            return null;

        return Content == ContentModel.Elements
            ? $"<{Tag}> may only contain {ChildList}, not <{child.Tag}>"
            : $"<{child.Tag}> is not allowed inside <{Tag}>";
    }
}
