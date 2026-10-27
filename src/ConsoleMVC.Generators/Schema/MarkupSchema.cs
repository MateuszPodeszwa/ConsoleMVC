#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// The set of C-View Markup elements known to the generator (v0.1: <c>text</c>, <c>box</c>,
/// <c>separator</c>, <c>br</c>).
/// </summary>
/// <remarks>
/// To add an element: create its runtime class in <c>ConsoleMVC.Rendering</c> and register an
/// <see cref="ElementSchema"/> here. Attribute values are validated and translated by their
/// <see cref="AttributeType"/>.
/// </remarks>
internal sealed class MarkupSchema
{
    /// <summary>The tag name of the line break element, which is also allowed inside text.</summary>
    public const string LineBreakTag = "br";

    /// <summary>The tag name of the text element, used for implicit text inside containers.</summary>
    public const string TextTag = "text";

    private const string Rendering = "global::ConsoleMVC.Rendering.";

    private readonly Dictionary<string, ElementSchema> _elements;

    /// <summary>
    /// Creates a schema from a set of element definitions.
    /// </summary>
    /// <param name="elements">The element definitions.</param>
    public MarkupSchema(IEnumerable<ElementSchema> elements)
    {
        _elements = elements.ToDictionary(e => e.Tag, StringComparer.Ordinal);
        ElementList = string.Join(", ", _elements.Keys.Select(k => "<" + k + ">"));
    }

    /// <summary>Gets the default v0.1 schema.</summary>
    public static MarkupSchema Default { get; } = CreateDefault();

    /// <summary>Gets a comma-separated list of known elements, for error messages.</summary>
    public string ElementList { get; }

    /// <summary>Gets the schema of the <c>&lt;text&gt;</c> element.</summary>
    public ElementSchema Text => _elements[TextTag];

    /// <summary>
    /// Looks up an element by tag name. Tag names are case-sensitive and lower case, like HTML in XHTML mode,
    /// so that C# identifiers such as <c>Text</c> are never mistaken for markup.
    /// </summary>
    /// <param name="tag">The tag name.</param>
    /// <param name="element">The element schema when found.</param>
    /// <returns><see langword="true"/> when the element exists.</returns>
    public bool TryGetElement(string tag, out ElementSchema element) => _elements.TryGetValue(tag, out element!);

    private static MarkupSchema CreateDefault()
    {
        var colour = new ColourAttributeType();
        var boolean = new BooleanAttributeType();
        var length = new LengthAttributeType();

        var alignment = new ChoiceAttributeType(new Dictionary<string, string>
        {
            ["left"] = Rendering + "Alignment.Left",
            ["centre"] = Rendering + "Alignment.Centre",
            ["center"] = Rendering + "Alignment.Centre",
            ["right"] = Rendering + "Alignment.Right"
        });

        var border = new ChoiceAttributeType(new Dictionary<string, string>
        {
            ["single"] = Rendering + "BorderStyle.Single",
            ["double"] = Rendering + "BorderStyle.Double",
            ["rounded"] = Rendering + "BorderStyle.Rounded",
            ["heavy"] = Rendering + "BorderStyle.Heavy",
            ["ascii"] = Rendering + "BorderStyle.Ascii",
            ["none"] = Rendering + "BorderStyle.None"
        });

        var lineStyle = new ChoiceAttributeType(new Dictionary<string, string>
        {
            ["single"] = Rendering + "LineStyle.Single",
            ["double"] = Rendering + "LineStyle.Double",
            ["heavy"] = Rendering + "LineStyle.Heavy",
            ["dashed"] = Rendering + "LineStyle.Dashed",
            ["ascii"] = Rendering + "LineStyle.Ascii"
        });

        return new MarkupSchema(
        [
            new ElementSchema(TextTag, Rendering + "TextElement", ContentModel.Inline,
            [
                new AttributeSchema("color", "Foreground", colour),
                new AttributeSchema("colour", "Foreground", colour),
                new AttributeSchema("background", "Background", colour),
                new AttributeSchema("bold", "Bold", boolean),
                new AttributeSchema("dim", "Dim", boolean),
                new AttributeSchema("italic", "Italic", boolean),
                new AttributeSchema("underline", "Underline", boolean),
                new AttributeSchema("align", "Align", alignment),
                new AttributeSchema("wrap", "Wrap", boolean)
            ]),
            new ElementSchema("box", Rendering + "BoxElement", ContentModel.Block,
            [
                new AttributeSchema("title", "Title", new StringAttributeType()),
                new AttributeSchema("width", "Width", length),
                new AttributeSchema("border", "Border", border),
                new AttributeSchema("padding", "Padding", new IntegerAttributeType(0, 20)),
                new AttributeSchema("color", "BorderColour", colour),
                new AttributeSchema("colour", "BorderColour", colour),
                new AttributeSchema("title-color", "TitleColour", colour),
                new AttributeSchema("title-colour", "TitleColour", colour)
            ]),
            new ElementSchema("separator", Rendering + "SeparatorElement", ContentModel.Void,
            [
                new AttributeSchema("style", "Style", lineStyle),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour),
                new AttributeSchema("width", "Width", length)
            ]),
            new ElementSchema(LineBreakTag, Rendering + "LineBreakElement", ContentModel.Void,
            [
                new AttributeSchema("count", "Count", new IntegerAttributeType(0, 100))
            ])
        ]);
    }
}
