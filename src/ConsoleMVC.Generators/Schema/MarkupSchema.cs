#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// The set of C-View Markup elements known to the generator: v0.1 <c>text</c>, <c>box</c>, <c>separator</c>,
/// <c>br</c>; v0.2 <c>table</c>/<c>row</c>/<c>cell</c>, <c>menu</c>/<c>option</c> and <c>input</c>; and
/// <c>errors</c> for validation messages; and <c>partial</c> to render a partial view.
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

    /// <summary>The tag name of the partial view element, which the emitter handles itself.</summary>
    public const string PartialTag = "partial";

    /// <summary>The tag name of the menu element, which partial views may not contain.</summary>
    public const string MenuTag = "menu";

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

    /// <summary>Gets the default schema.</summary>
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
        var text = new StringAttributeType();
        var padding = new IntegerAttributeType(0, 20);

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

        // Shared by every element with styled inline content: <text>, <cell>, <option>
        AttributeSchema[] InlineStyle() =>
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
        ];

        return new MarkupSchema(
        [
            new ElementSchema(TextTag, Rendering + "TextElement", ContentModel.Inline, InlineStyle()),
            new ElementSchema("box", Rendering + "BoxElement", ContentModel.Block,
            [
                new AttributeSchema("title", "Title", text),
                new AttributeSchema("width", "Width", length),
                new AttributeSchema("border", "Border", border),
                new AttributeSchema("padding", "Padding", padding),
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
            ]),

            // ---- v0.2: tables
            new ElementSchema("table", Rendering + "TableElement", ContentModel.Elements,
            [
                new AttributeSchema("border", "Border", border),
                new AttributeSchema("padding", "Padding", padding),
                new AttributeSchema("width", "Width", length),
                new AttributeSchema("color", "BorderColour", colour),
                new AttributeSchema("colour", "BorderColour", colour)
            ], allowedChildren: ["row"]),
            new ElementSchema("row", Rendering + "TableRowElement", ContentModel.Elements,
            [
                new AttributeSchema("header", "IsHeader", boolean)
            ], allowedChildren: ["cell"], requiredParent: "table"),
            new ElementSchema("cell", Rendering + "TableCellElement", ContentModel.Inline, InlineStyle(),
                requiredParent: "row"),

            // ---- v0.2: interaction
            new ElementSchema("menu", Rendering + "MenuElement", ContentModel.Elements,
            [
                new AttributeSchema("prompt", "Prompt", text),
                new AttributeSchema("error", "ErrorMessage", text),
                new AttributeSchema("key-color", "KeyColour", colour),
                new AttributeSchema("key-colour", "KeyColour", colour)
            ], allowedChildren: ["option"], topLevelOnly: true, topLevelStatement: "return this.RunMenu({0});"),
            new ElementSchema("option", Rendering + "MenuOptionElement", ContentModel.Inline,
            [
                ..InlineStyle(),
                new AttributeSchema("key", "Key", text, required: true),
                new AttributeSchema("nav", "Target", new RouteAttributeType()),
                new AttributeSchema("quit", "Quit", boolean),
                new AttributeSchema("post", "PostForm", boolean)
            ], requiredParent: "menu", validator: ValidateOption),
            new ElementSchema("input", Rendering + "InputElement", ContentModel.Void,
            [
                new AttributeSchema("name", "Name", text, required: true),
                new AttributeSchema("label", "Label", text),
                new AttributeSchema("default", "Default", text),
                new AttributeSchema("required", "Required", boolean),
                new AttributeSchema("masked", "Masked", boolean),
                new AttributeSchema("color", "LabelColour", colour),
                new AttributeSchema("colour", "LabelColour", colour)
            ], topLevelOnly: true, topLevelStatement: "this.ReadInput({0});"),

            // ---- validation
            new ElementSchema("errors", Rendering + "ValidationErrorsElement", ContentModel.Void,
            [
                new AttributeSchema("for", "For", text),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour)
            ], initialisers: ["Source = this.ModelState"]),

            // ---- composition: rendered by the emitter as a call to the partial's Render(model)
            new ElementSchema(PartialTag, "", ContentModel.Void,
            [
                new AttributeSchema("name", "Name", text, required: true),
                new AttributeSchema("model", "Model", new ModelAttributeType())
            ], topLevelOnly: true)
        ]);
    }

    /// <summary>An option must either navigate (<c>nav</c>) or quit (<c>quit</c>), not both and not neither.</summary>
    private static string? ValidateOption(IReadOnlyList<Syntax.AttributeNode> attributes)
    {
        var navigates = attributes.Any(a => a.Schema.Property == "Target");
        // quit="false" is the same as leaving quit out
        var quits = attributes.Any(a => a.Schema.Property == "Quit" && a.LiteralCode != "false");

        if (navigates == quits)
            return "<option> needs exactly one of 'nav' (e.g. nav=\"Home/Index\") or 'quit'";

        return null;
    }
}
