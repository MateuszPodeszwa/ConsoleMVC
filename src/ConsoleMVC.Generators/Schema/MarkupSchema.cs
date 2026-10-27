#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// The set of C-View Markup elements known to the generator: v0.1 <c>text</c>, <c>box</c>, <c>separator</c>,
/// <c>br</c>; v0.2 <c>table</c>/<c>row</c>/<c>cell</c>, <c>menu</c>/<c>option</c> and <c>input</c>; and
/// <c>errors</c> for validation messages; <c>partial</c> to render a partial view; and the choice inputs
/// <c>select</c>, <c>radio</c>, <c>choice</c> and <c>checkbox</c>.
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

    /// <summary>The attribute that applies named styles from the view's theme.</summary>
    public const string ClassAttribute = "class";

    /// <summary>The tag name of the partial view element, which the emitter handles itself.</summary>
    public const string PartialTag = "partial";

    /// <summary>The tag name of a layout's placeholder for the view, which the emitter handles itself.</summary>
    public const string BodyTag = "body";

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

        // Named styles from the view's Theme; applied before the element's other attributes (see ViewEmitter)
        AttributeSchema ThemeClass() => new(ClassAttribute, ElementSchema.ThemeClassProperty, text);
        var widthLimit = new IntegerAttributeType(0, 1000);

        // Shared by every element with styled inline content: <text>, <cell>, <option>
        AttributeSchema[] InlineStyle() =>
        [
            ThemeClass(),
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

        var number = new DecimalAttributeType();
        var amount = new DecimalAttributeType(asDouble: true);

        var progressStyle = new ChoiceAttributeType(new Dictionary<string, string>
        {
            ["block"] = Rendering + "ProgressStyle.Block",
            ["line"] = Rendering + "ProgressStyle.Line",
            ["ascii"] = Rendering + "ProgressStyle.Ascii"
        });

        var barStyle = new ChoiceAttributeType(new Dictionary<string, string>
        {
            ["block"] = Rendering + "BarChartStyle.Block",
            ["shade"] = Rendering + "BarChartStyle.Shade",
            ["ascii"] = Rendering + "BarChartStyle.Ascii"
        });

        var spinnerStyle = new ChoiceAttributeType(new Dictionary<string, string>
        {
            ["dots"] = Rendering + "SpinnerStyle.Dots",
            ["line"] = Rendering + "SpinnerStyle.Line",
            ["circle"] = Rendering + "SpinnerStyle.Circle"
        });
        var date = new DateAttributeType();

        // Shared by <number> and <date>
        AttributeSchema[] ValueField(bool dateDefault = false) =>
        [
            new AttributeSchema("name", "Name", text, required: true),
            new AttributeSchema("label", "Label", text),
            new AttributeSchema("default", "Default", dateDefault ? date : text),
            new AttributeSchema("required", "Required", boolean),
            new AttributeSchema("color", "LabelColour", colour),
            new AttributeSchema("colour", "LabelColour", colour),
            new AttributeSchema("highlight-color", "HighlightColour", colour),
            new AttributeSchema("highlight-colour", "HighlightColour", colour)
        ];

        // Shared by <select> and <radio>
        AttributeSchema[] ChoiceField() =>
        [
            new AttributeSchema("name", "Name", text, required: true),
            new AttributeSchema("label", "Label", text),
            new AttributeSchema("default", "Default", text),
            new AttributeSchema("required", "Required", boolean),
            new AttributeSchema("color", "LabelColour", colour),
            new AttributeSchema("colour", "LabelColour", colour),
            new AttributeSchema("highlight-color", "HighlightColour", colour),
            new AttributeSchema("highlight-colour", "HighlightColour", colour)
        ];

        return new MarkupSchema(
        [
            new ElementSchema(TextTag, Rendering + "TextElement", ContentModel.Inline, InlineStyle()),
            new ElementSchema("box", Rendering + "BoxElement", ContentModel.Block,
            [
                ThemeClass(),
                new AttributeSchema("title", "Title", text),
                new AttributeSchema("width", "Width", length),
                new AttributeSchema("min-width", "MinWidth", widthLimit),
                new AttributeSchema("max-width", "MaxWidth", widthLimit),
                new AttributeSchema("border", "Border", border),
                new AttributeSchema("padding", "Padding", padding),
                new AttributeSchema("color", "BorderColour", colour),
                new AttributeSchema("colour", "BorderColour", colour),
                new AttributeSchema("title-color", "TitleColour", colour),
                new AttributeSchema("title-colour", "TitleColour", colour)
            ]),
            new ElementSchema("separator", Rendering + "SeparatorElement", ContentModel.Void,
            [
                ThemeClass(),
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
                ThemeClass(),
                new AttributeSchema("border", "Border", border),
                new AttributeSchema("padding", "Padding", padding),
                new AttributeSchema("width", "Width", length),
                new AttributeSchema("min-width", "MinWidth", widthLimit),
                new AttributeSchema("max-width", "MaxWidth", widthLimit),
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
                new AttributeSchema("back", "Back", boolean),
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

            // ---- choice inputs
            new ElementSchema("select", Rendering + "SelectElement", ContentModel.Elements,
            [
                ..ChoiceField(),
                new AttributeSchema("multiple", "Multiple", boolean),
                new AttributeSchema("filter", "Filter", boolean),
                new AttributeSchema("size", "Size", new IntegerAttributeType(0, 1000))
            ], allowedChildren: ["choice"], topLevelOnly: true, topLevelStatement: "this.ReadInput({0});"),
            new ElementSchema("radio", Rendering + "RadioElement", ContentModel.Elements, ChoiceField(),
                allowedChildren: ["choice"], topLevelOnly: true, topLevelStatement: "this.ReadInput({0});"),
            new ElementSchema("choice", Rendering + "ChoiceElement", ContentModel.Inline,
            [
                ..InlineStyle(),
                new AttributeSchema("value", "Value", text)
            ], requiredParents: ["select", "radio"]),
            new ElementSchema("checkbox", Rendering + "CheckboxElement", ContentModel.Void,
            [
                new AttributeSchema("name", "Name", text, required: true),
                new AttributeSchema("label", "Label", text),
                new AttributeSchema("checked", "Checked", boolean),
                new AttributeSchema("required", "Required", boolean),
                new AttributeSchema("color", "LabelColour", colour),
                new AttributeSchema("colour", "LabelColour", colour)
            ], topLevelOnly: true, topLevelStatement: "this.ReadInput({0});"),

            new ElementSchema("number", Rendering + "NumberElement", ContentModel.Void,
            [
                ..ValueField(),
                new AttributeSchema("min", "Min", number),
                new AttributeSchema("max", "Max", number),
                new AttributeSchema("step", "Step", number),
                new AttributeSchema("decimals", "Decimals", new IntegerAttributeType(0, 28))
            ], topLevelOnly: true, topLevelStatement: "this.ReadInput({0});"),
            new ElementSchema("date", Rendering + "DateElement", ContentModel.Void,
            [
                ..ValueField(dateDefault: true),
                new AttributeSchema("min", "Min", date),
                new AttributeSchema("max", "Max", date)
            ], topLevelOnly: true, topLevelStatement: "this.ReadInput({0});"),

            // ---- validation
            new ElementSchema("errors", Rendering + "ValidationErrorsElement", ContentModel.Void,
            [
                ThemeClass(),
                new AttributeSchema("for", "For", text),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour)
            ], initialisers: ["Source = this.ModelState"]),

            // ---- navigation
            new ElementSchema("breadcrumbs", Rendering + "BreadcrumbsElement", ContentModel.Void,
            [
                ThemeClass(),
                new AttributeSchema("separator", "Separator", text),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour)
            ], initialisers: ["Items = this.Breadcrumbs"]),
            new ElementSchema("shortcuts", Rendering + "ShortcutsElement", ContentModel.Void,
            [
                ThemeClass(),
                new AttributeSchema("separator", "Separator", text),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour),
                new AttributeSchema("key-color", "KeyColour", colour),
                new AttributeSchema("key-colour", "KeyColour", colour)
            ], initialisers: ["Items = this.ShortcutHints"]),

            // ---- 1.24: live elements
            new ElementSchema("progress", Rendering + "ProgressElement", ContentModel.Void,
            [
                ThemeClass(),
                new AttributeSchema("value", "Value", amount),
                new AttributeSchema("max", "Max", amount),
                new AttributeSchema("label", "Label", text),
                new AttributeSchema("bar-width", "BarWidth", new IntegerAttributeType(1, 1000)),
                new AttributeSchema("percent", "ShowPercent", boolean),
                new AttributeSchema("style", "Style", progressStyle),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour),
                new AttributeSchema("empty-color", "EmptyColour", colour),
                new AttributeSchema("empty-colour", "EmptyColour", colour),
                new AttributeSchema("label-color", "LabelColour", colour),
                new AttributeSchema("label-colour", "LabelColour", colour)
            ]),
            new ElementSchema("spinner", Rendering + "SpinnerElement", ContentModel.Void,
            [
                ThemeClass(),
                new AttributeSchema("label", "Label", text),
                new AttributeSchema("style", "Style", spinnerStyle),
                new AttributeSchema("frame", "Frame", new IntegerAttributeType(0, int.MaxValue)),
                new AttributeSchema("done", "Done", boolean),
                new AttributeSchema("done-symbol", "DoneSymbol", text),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour),
                new AttributeSchema("label-color", "LabelColour", colour),
                new AttributeSchema("label-colour", "LabelColour", colour)
            ]),

            // ---- 1.25: charts
            new ElementSchema("bar-chart", Rendering + "BarChartElement", ContentModel.Elements,
            [
                ThemeClass(),
                new AttributeSchema("max", "Max", amount),
                new AttributeSchema("values", "ShowValues", boolean),
                new AttributeSchema("format", "Format", text),
                new AttributeSchema("style", "Style", barStyle),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour),
                new AttributeSchema("label-color", "LabelColour", colour),
                new AttributeSchema("label-colour", "LabelColour", colour)
            ], allowedChildren: ["bar"]),
            new ElementSchema("bar", Rendering + "BarElement", ContentModel.Void,
            [
                new AttributeSchema("label", "Label", text),
                new AttributeSchema("value", "Value", amount, required: true),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour)
            ], requiredParent: "bar-chart"),
            new ElementSchema("sparkline", Rendering + "SparklineElement", ContentModel.Void,
            [
                ThemeClass(),
                new AttributeSchema("values", "Values", new NumberListAttributeType(), required: true),
                new AttributeSchema("min", "Min", amount),
                new AttributeSchema("max", "Max", amount),
                new AttributeSchema("label", "Label", text),
                new AttributeSchema("show-last", "ShowLast", boolean),
                new AttributeSchema("format", "Format", text),
                new AttributeSchema("color", "Colour", colour),
                new AttributeSchema("colour", "Colour", colour),
                new AttributeSchema("label-color", "LabelColour", colour),
                new AttributeSchema("label-colour", "LabelColour", colour)
            ]),

            // ---- 1.27: scrolling (a pager: waits for the user, so only at the top level of a view)
            new ElementSchema("scroll", Rendering + "ScrollElement", ContentModel.Block,
            [
                ThemeClass(),
                new AttributeSchema("height", "Height", new IntegerAttributeType(1, 1000)),
                new AttributeSchema("color", "StatusColour", colour),
                new AttributeSchema("colour", "StatusColour", colour)
            ], topLevelOnly: true, topLevelStatement: "this.ShowScroll({0});"),

            // ---- 1.26: Markdown
            new ElementSchema("markdown", Rendering + "MarkdownElement", ContentModel.Void,
            [
                ThemeClass(),
                new AttributeSchema("text", "Text", text, required: true),
                new AttributeSchema("links", "ShowLinkUrls", boolean),
                new AttributeSchema("heading-color", "HeadingColour", colour),
                new AttributeSchema("heading-colour", "HeadingColour", colour),
                new AttributeSchema("code-color", "CodeColour", colour),
                new AttributeSchema("code-colour", "CodeColour", colour),
                new AttributeSchema("link-color", "LinkColour", colour),
                new AttributeSchema("link-colour", "LinkColour", colour),
                new AttributeSchema("quote-color", "QuoteColour", colour),
                new AttributeSchema("quote-colour", "QuoteColour", colour)
            ]),

            // ---- 1.0: layout
            new ElementSchema("columns", Rendering + "ColumnsElement", ContentModel.Elements,
            [
                new AttributeSchema("gap", "Gap", new IntegerAttributeType(0, 20)),
                new AttributeSchema("stack-below", "StackBelow", widthLimit)
            ], allowedChildren: ["column"]),
            new ElementSchema("column", Rendering + "ColumnElement", ContentModel.Block,
            [
                new AttributeSchema("width", "Width", length)
            ], requiredParent: "columns"),

            // ---- composition: rendered by the emitter as a call to the partial's Render(model)
            new ElementSchema(PartialTag, "", ContentModel.Void,
            [
                new AttributeSchema("name", "Name", text, required: true),
                new AttributeSchema("model", "Model", new ModelAttributeType())
            ], topLevelOnly: true),
            new ElementSchema(BodyTag, "", ContentModel.Void, [], topLevelOnly: true)
        ]);
    }

    /// <summary>An option must either navigate (<c>nav</c>) or quit (<c>quit</c>), not both and not neither.</summary>
    private static string? ValidateOption(IReadOnlyList<Syntax.AttributeNode> attributes)
    {
        var navigates = attributes.Any(a => a.Schema.Property == "Target");
        // quit="false" is the same as leaving quit out, and so is back="false"
        var quits = attributes.Any(a => a.Schema.Property == "Quit" && a.LiteralCode != "false");
        var goesBack = attributes.Any(a => a.Schema.Property == "Back" && a.LiteralCode != "false");

        if ((navigates ? 1 : 0) + (quits ? 1 : 0) + (goesBack ? 1 : 0) != 1)
            return "<option> needs exactly one of 'nav' (e.g. nav=\"Home/Index\"), 'quit' or 'back'";

        return null;
    }
}
