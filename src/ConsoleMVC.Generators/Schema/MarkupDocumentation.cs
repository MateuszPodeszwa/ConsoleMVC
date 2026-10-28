#nullable enable

using System;
using System.Collections.Generic;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// Short descriptions of every element, attribute and directive, written into the exported schema
/// (<c>schema/cvw-schema.json</c>) so editors can show them in completion lists and hover documentation.
/// </summary>
/// <remarks>
/// An attribute is described by its runtime property, so aliases such as <c>color</c> and <c>colour</c> share one text;
/// a <c>tag.Property</c> entry overrides it for one element. The schema export tests fail when an element or attribute
/// has no description, so adding an element to <see cref="MarkupSchema"/> means describing it here too.
/// </remarks>
internal static class MarkupDocumentation
{
    /// <summary>One description per element tag.</summary>
    public static readonly IReadOnlyDictionary<string, string> Elements = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["text"] = "Styled text that wraps to the available width. May contain @expressions and <br />.",
        ["box"] = "A bordered panel with an optional title. Holds other elements; plain text inside becomes a <text>.",
        ["separator"] = "A horizontal rule across the available width.",
        ["br"] = "A line break; inside text it ends the line, elsewhere it leaves blank lines.",
        ["table"] = "A table whose columns size themselves to the content. Holds <row> elements, usually from @foreach.",
        ["row"] = "One row of a <table>; 'header' makes it the header row.",
        ["cell"] = "One cell of a <row>, with styled text.",
        ["menu"] = "Shows keyed options, waits for the user's choice and navigates. The view returns its result, so it comes last.",
        ["option"] = "One menu option: typing its key chooses it. Needs exactly one of 'nav', 'quit' or 'back'.",
        ["input"] = "Reads a line of text into the view's Form, under 'name'.",
        ["select"] = "A list of <choice> elements answered with the arrow keys (or typed); 'multiple' allows several answers.",
        ["radio"] = "One answer from a small set of <choice> elements.",
        ["choice"] = "One choice of a <select> or <radio>: its text is shown and its 'value' (or text) is posted.",
        ["checkbox"] = "A yes or no field, posted as true or false.",
        ["number"] = "A number field, checked against 'min', 'max', 'step' and 'decimals'.",
        ["date"] = "A date field; 'min', 'max' and 'default' accept yyyy-MM-dd, today, tomorrow, yesterday, today+N or today-N.",
        ["errors"] = "The validation messages of the view's ModelState: every field's, or one field's with 'for'.",
        ["breadcrumbs"] = "The navigation history as a trail, such as Home › Orders › Details.",
        ["shortcuts"] = "The application's keyboard shortcuts with their descriptions, such as 'Esc Back  Ctrl+X Quit'.",
        ["progress"] = "A progress bar of 'value' out of 'max'; redrawn in place while an action works (ILiveDisplay).",
        ["spinner"] = "A spinner that turns while an action works, then shows a tick when it is 'done'.",
        ["bar-chart"] = "Horizontal bars that compare values. Holds <bar> elements.",
        ["bar"] = "One bar of a <bar-chart>.",
        ["sparkline"] = "A series of values drawn on one line, such as ▁▂▅▇▅▃.",
        ["scroll"] = "Shows long content a page at a time; the user scrolls with the arrow keys and continues with Enter.",
        ["markdown"] = "Draws Markdown: headings, lists, emphasis, code, quotes and links.",
        ["columns"] = "Places <column> elements side by side, and stacks them when the terminal is narrow.",
        ["column"] = "One column of <columns>; 'width' sets its share.",
        ["partial"] = "Renders a partial view from Views/Shared, optionally with a model.",
        ["body"] = "In a layout view (Views/Shared), where each page's own content is drawn."
    };

    /// <summary>One description per attribute, by runtime property; <c>tag.Property</c> keys override for one element.</summary>
    public static readonly IReadOnlyDictionary<string, string> Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ElementSchema.ThemeClassProperty] = "Named styles from the theme, separated by spaces, such as \"title\" or \"panel muted\".",
        ["Foreground"] = "The text colour.",
        ["Background"] = "The background colour.",
        ["Bold"] = "Bold text.",
        ["Dim"] = "Dim (faint) text.",
        ["Italic"] = "Italic text.",
        ["Underline"] = "Underlined text.",
        ["Align"] = "Horizontal alignment: left, centre or right.",
        ["Wrap"] = "Whether long text wraps onto more lines (the default) or is cut off.",
        ["Title"] = "The title shown in the top border.",
        ["Width"] = "The width: auto, a number of columns, a percentage such as 50%, or * for the rest.",
        ["MinWidth"] = "The narrowest the element may be, in columns.",
        ["MaxWidth"] = "The widest the element may be, in columns.",
        ["Border"] = "The border style.",
        ["Padding"] = "Blank columns between the border and the content.",
        ["BorderColour"] = "The border colour.",
        ["TitleColour"] = "The title colour.",
        ["Style"] = "The line style.",
        ["Colour"] = "The colour.",
        ["Count"] = "How many line breaks.",
        ["IsHeader"] = "Makes this the header row, drawn bold and separated from the rows below.",
        ["Prompt"] = "The prompt written before the user types a choice.",
        ["ErrorMessage"] = "The message shown when the user types a key that no option has.",
        ["KeyColour"] = "The colour of the keys, such as [1].",
        ["Key"] = "What the user types to choose this option, such as 1 or q; no two options may share one, ignoring case.",
        ["Target"] = "Where the option goes: Controller/Action or Action, optionally with ?key=value route values.",
        ["Quit"] = "Ends the application.",
        ["Back"] = "Goes back to the previous screen.",
        ["PostForm"] = "Posts the values the view collected (its Form) to the target action.",
        ["Name"] = "The name the value is posted under: an action parameter or model property.",
        ["Label"] = "The label shown before the field.",
        ["Default"] = "The value used when the user enters nothing.",
        ["Required"] = "Asks again until the user gives a value.",
        ["Masked"] = "Hides what is typed, as for a password.",
        ["LabelColour"] = "The label colour.",
        ["HighlightColour"] = "The colour of the highlighted choice.",
        ["Multiple"] = "Allows several choices; Space selects and Enter confirms.",
        ["Filter"] = "Lets the user type to narrow the list.",
        ["Size"] = "How many choices are shown at once; the list scrolls through the rest.",
        ["Value"] = "The value posted when this is chosen; the choice's text when left out.",
        ["Checked"] = "Starts ticked.",
        ["Min"] = "The smallest value accepted.",
        ["Max"] = "The largest value accepted.",
        ["Step"] = "The amount the arrow keys add or take away; more than 0.",
        ["Decimals"] = "How many decimal places are accepted.",
        ["For"] = "Shows only this field's messages (case-insensitive).",
        ["Separator"] = "The text between items.",
        ["BarWidth"] = "The width of the bar, in columns.",
        ["ShowPercent"] = "Shows the percentage after the bar.",
        ["EmptyColour"] = "The colour of the unfilled part.",
        ["Frame"] = "The frame to draw, for a spinner animated by @refresh.",
        ["Done"] = "Shows the finished symbol instead of turning.",
        ["DoneSymbol"] = "The symbol shown when done; ✓ by default.",
        ["ShowValues"] = "Shows each bar's value after it.",
        ["Format"] = "A .NET format string for the values, such as N0 or 0.0.",
        ["Values"] = "The values: numbers separated by commas or spaces, or an expression such as @Model.Load.",
        ["ShowLast"] = "Shows the last value after the line.",
        ["Height"] = "How many lines a page shows; the terminal's height when left out.",
        ["StatusColour"] = "The colour of the status line (Lines 1-12 of 40).",
        ["Text"] = "The Markdown text, usually an expression such as @Model.Notes.",
        ["ShowLinkUrls"] = "Shows each link's address after its text.",
        ["HeadingColour"] = "The colour of headings.",
        ["CodeColour"] = "The colour of code.",
        ["LinkColour"] = "The colour of links.",
        ["QuoteColour"] = "The colour of quotes.",
        ["Gap"] = "Blank columns between the columns.",
        ["StackBelow"] = "Stacks the columns when the terminal is narrower than this many columns.",
        ["Model"] = "The model passed to the partial view, such as @Model.User.",

        // Where a property means something else for one element
        ["separator.Width"] = "The width of the rule: auto (the full width), a number of columns or a percentage.",
        ["progress.Style"] = "The bar style.",
        ["spinner.Style"] = "The animation.",
        ["bar-chart.Style"] = "The bar style.",
        ["bar-chart.Max"] = "The value of a full-width bar; the largest value when left out.",
        ["progress.Value"] = "How much is done, out of 'max'.",
        ["progress.Max"] = "The value of a full bar; 100 when left out.",
        ["progress.Label"] = "The text before the bar.",
        ["spinner.Label"] = "The text after the spinner.",
        ["bar.Value"] = "The bar's value.",
        ["bar.Label"] = "The label before the bar.",
        ["sparkline.Label"] = "The text before the line.",
        ["sparkline.Min"] = "The value drawn as the lowest block; the smallest value when left out.",
        ["sparkline.Max"] = "The value drawn as the highest block; the largest value when left out.",
        ["column.Width"] = "The column's width: auto, a number of columns, a percentage or * for the rest.",
        ["partial.Name"] = "The partial view's name: Views/Shared/{name}.cvw.",
        ["breadcrumbs.Separator"] = "The text between screens; › by default.",
        ["number.Default"] = "The answer to an empty line and the starting value; a number the field accepts.",
        ["select.Default"] = "The value chosen for an empty answer and highlighted first; for 'multiple', values separated by commas.",
        ["radio.Default"] = "The value chosen for an empty answer and highlighted first; it must be a choice's value.",
        ["shortcuts.Separator"] = "The text between shortcuts; two spaces by default."
    };

    /// <summary>The directives at the top of a view, with their syntax and description.</summary>
    public static readonly IReadOnlyList<(string Name, string Syntax, string Description)> Directives =
    [
        ("@model", "@model Namespace.Type", "The type of the view's Model, passed by the action's View(model)."),
        ("@using", "@using Namespace", "Imports a namespace for the view's code."),
        ("@inject", "@inject IService Name", "Declares a property filled from the application's services before the view is drawn."),
        ("@layout", "@layout Name", "Draws the view inside a layout from Views/Shared; @layout none opts out of the default layout."),
        ("@refresh", "@refresh 5s", "Draws the view again every interval (ms, s or m) until the user starts typing.")
    ];

    /// <summary>The markup's C# constructs, for completion and documentation.</summary>
    public static readonly IReadOnlyList<(string Syntax, string Description)> Constructs =
    [
        ("@Model.Name", "An expression, written into the markup as text."),
        ("@(a + b)", "An explicit expression, for anything more than a name and member accesses."),
        ("@{ ... }", "A C# code block."),
        ("@if (...) { ... } else { ... }", "Conditional markup."),
        ("@foreach (var item in items) { ... }", "Repeated markup; also @for, @while, @do and @switch."),
        ("@* ... *@", "A comment, left out of the generated code."),
        ("@@", "A literal @."),
        ("&lt;", "A literal <; also &gt; and &amp;.")
    ];

    /// <summary>Returns the description of an element's attribute.</summary>
    /// <param name="tag">The element's tag.</param>
    /// <param name="attribute">The attribute.</param>
    /// <returns>The description, or <see langword="null"/> when there is none.</returns>
    public static string? Describe(string tag, AttributeSchema attribute) =>
        Attributes.TryGetValue(tag + "." + attribute.Property, out var specific)
            ? specific
            : Attributes.TryGetValue(attribute.Property, out var general) ? general : null;
}
