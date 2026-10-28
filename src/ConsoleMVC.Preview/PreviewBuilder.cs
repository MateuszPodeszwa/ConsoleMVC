using System.Reflection;
using ConsoleMVC.Generators.Emit;
using ConsoleMVC.Generators.Parsing;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Preview;

/// <summary>
/// Builds the runtime elements of a parsed view, as its generated code would, but without running any C#: code blocks
/// and control flow are skipped, so every element is built once; expressions become placeholders; partial views are
/// built in place and the view is put inside its layout. See docs/design/editor-support.md, section 3.2.
/// </summary>
/// <param name="theme">The theme that <c>class="…"</c> attributes apply.</param>
/// <param name="files">Where partial views and layouts are read from.</param>
/// <param name="problems">Collects what the preview could not show, such as an unknown partial.</param>
/// <example>
/// <code>
/// var builder = new PreviewBuilder(Theme.CreateDefault(), ProjectViewFiles.ForView(path), problems);
/// foreach (var element in builder.BuildView(document)) renderer.Render(element);
/// </code>
/// </example>
internal sealed class PreviewBuilder(Theme theme, IViewFiles files, List<string> problems)
{
    /// <summary>The deepest nesting of partial views followed; deeper ones are shown as a note.</summary>
    public const int MaxPartialDepth = 8;

    private static readonly Assembly RenderingAssembly = typeof(Element).Assembly;

    /// <summary>The runtime property that holds the number an element shows, such as a progress bar's.</summary>
    private const string ValueProperty = "Value";

    private readonly Stack<string> _partials = new();

    private int _samples;

    /// <summary>Builds a view's top-level elements, inside its layout unless it is a shared view or uses <c>@layout none</c>.</summary>
    /// <param name="document">The parsed view.</param>
    /// <returns>The elements to draw, in order.</returns>
    public IReadOnlyList<Element> BuildView(CvwDocument document)
    {
        var page = new List<Element>();
        BuildTopLevel(document.Body, page, body: null);

        if (files.IsShared)
            return page;

        var layoutName = document.Layout?.Value ?? PartialChecker.DefaultLayout;
        if (layoutName == PartialChecker.NoLayout || LoadShared(layoutName) is not { } layout || !layout.HasBodyPlaceholder)
        {
            if (document.Layout is not null && layoutName != PartialChecker.NoLayout)
                problems.Add($"The layout '{layoutName}' was not found in Views/Shared, so the view is shown without it.");
            return page;
        }

        var framed = new List<Element>();
        BuildTopLevel(layout.Body, framed, body: page);
        return framed;
    }

    /// <summary>Builds top-level nodes: elements, partials and the layout's body; C# is skipped.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="output">Receives the elements.</param>
    /// <param name="body">The page drawn at a layout's <c>&lt;body /&gt;</c>, or <see langword="null"/>.</param>
    private void BuildTopLevel(IReadOnlyList<Node> nodes, List<Element> output, IReadOnlyList<Element>? body)
    {
        foreach (var node in nodes.OfType<ElementNode>())
        {
            switch (node.Schema.Tag)
            {
                case MarkupSchema.BodyTag:
                    output.AddRange(body ?? [Note("the page's content")]);
                    break;

                case MarkupSchema.PartialTag:
                    BuildPartial(node, output);
                    break;

                default:
                    if (BuildElement(node) is Element element)
                    {
                        output.Add(element);

                        // A menu waits for a choice: show its prompt, as the user would see it
                        if (element is MenuElement menu)
                            output.Add(Text(menu.Prompt));
                    }

                    break;
            }
        }
    }

    private void BuildPartial(ElementNode node, List<Element> output)
    {
        var name = node.Attributes.FirstOrDefault(a => a.Schema.Property == "Name") is { } attribute
                   && attribute.Parts.All(p => !p.IsExpression)
            ? string.Concat(attribute.Parts.Select(p => p.Text)).Trim()
            : null;

        if (name is null || !ProjectViewFiles.IsPlainName(name))
        {
            output.Add(Note("a partial view"));
            return;
        }

        if (_partials.Count >= MaxPartialDepth || _partials.Contains(name))
        {
            output.Add(Note($"partial view {name}"));
            return;
        }

        if (LoadShared(name) is not { } partial)
        {
            problems.Add($"The partial view '{name}' was not found in Views/Shared.");
            output.Add(Note($"partial view {name}"));
            return;
        }

        _partials.Push(name);
        BuildTopLevel(partial.Body, output, body: null);
        _partials.Pop();
    }

    private CvwDocument? LoadShared(string name) =>
        ProjectViewFiles.IsPlainName(name) && files.ReadShared(name) is { } text ? CvwDocumentParser.Parse(text, MarkupSchema.Default) : null;

    /// <summary>
    /// Creates the runtime object of an element node with its attributes and content: usually an <see cref="Element"/>,
    /// but parts of an element, such as a table's <c>&lt;row&gt;</c> or a chart's <c>&lt;bar&gt;</c>, are plain objects.
    /// </summary>
    private object? BuildElement(ElementNode node)
    {
        var schema = node.Schema;
        var typeName = schema.RuntimeType.StartsWith("global::", StringComparison.Ordinal) ? schema.RuntimeType[8..] : schema.RuntimeType;
        if (RenderingAssembly.GetType(typeName) is not { } type || Activator.CreateInstance(type) is not { } element)
            return null;

        AddSamples(element);

        // Theme classes first, so attributes written on the element override them, as in the generated code
        foreach (var attribute in node.Attributes.Where(a => a.Schema.Property == ElementSchema.ThemeClassProperty))
        {
            if (element is Element themed && attribute.Parts.All(p => !p.IsExpression))
                theme.Apply(themed, string.Concat(attribute.Parts.Select(p => p.Text)));
        }

        foreach (var attribute in node.Attributes.Where(a => a.Schema.Property != ElementSchema.ThemeClassProperty))
        {
            var property = type.GetProperty(attribute.Schema.Property, BindingFlags.Public | BindingFlags.Instance);
            if (property is not { CanWrite: true })
                continue;

            if (AttributeValues.TryConvert(attribute, property.PropertyType, out var value))
                SetProperty(element, property, attribute, value);
            else if (IsComputedValue(attribute, property))
                SetProperty(element, property, attribute, NextSample());
        }

        switch (schema.Content)
        {
            case ContentModel.Inline when element is InlineElement inline:
                AppendInline(node.Children, inline);
                break;

            case ContentModel.Block or ContentModel.Elements:
                BuildChildren(node.Children, element, textAllowed: schema.Content == ContentModel.Block);
                break;
        }

        if (element is ValidationErrorsElement errors)
            errors.Source = SampleErrors(errors.For);

        return element;
    }

    /// <summary>Builds children: elements, and runs of text in a block container as an implicit <c>&lt;text&gt;</c>.</summary>
    private void BuildChildren(IReadOnlyList<Node> nodes, object parent, bool textAllowed)
    {
        var index = 0;
        while (index < nodes.Count)
        {
            switch (nodes[index])
            {
                case ElementNode child:
                    if (BuildElement(child) is { } element)
                        Add(parent, element);
                    index++;
                    break;

                case TextNode or ExpressionNode:
                    var run = new List<Node>();
                    while (index < nodes.Count && nodes[index] is TextNode or ExpressionNode)
                        run.Add(nodes[index++]);

                    var items = ViewEmitter.NormaliseInline(run);
                    if (items.Count > 0 && textAllowed)
                    {
                        var text = new TextElement();
                        AppendItems(items, text);
                        Add(parent, text);
                    }

                    break;

                default:
                    index++; // C# runs nowhere in a preview
                    break;
            }
        }
    }

    private static void AppendInline(IReadOnlyList<Node> children, InlineElement element) =>
        AppendItems(ViewEmitter.NormaliseInline(children), element);

    private static void AppendItems(IEnumerable<Node> items, InlineElement element)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case TextNode text:
                    element.AppendText(text.Text);
                    break;

                case ExpressionNode expression:
                    element.AppendText(AttributeValues.Placeholder(expression.Code));
                    break;

                case ElementNode { Schema.Tag: MarkupSchema.LineBreakTag } lineBreak:
                    var count = lineBreak.Attributes.FirstOrDefault() is { } attribute
                                && AttributeValues.TryConvert(attribute, typeof(int), out var value) ? (int)value! : 1;
                    for (var i = 0; i < Math.Clamp(count, 0, 100); i++)
                        element.AppendLineBreak();
                    break;
            }
        }
    }

    /// <summary>Sets a property; a value its setter refuses, such as a negative width, is reported and left out.</summary>
    private void SetProperty(object element, PropertyInfo property, AttributeNode attribute, object? value)
    {
        try
        {
            property.SetValue(element, value);
        }
        catch (TargetInvocationException e) when (e.InnerException is ArgumentException argument)
        {
            problems.Add($"{attribute.Schema.Name}: {argument.Message}");
        }
    }

    /// <summary>
    /// Whether the attribute is a computed <c>value="@…"</c> of a number, such as a progress bar's or a chart bar's, which
    /// is shown with a sample so the element is not empty.
    /// </summary>
    private static bool IsComputedValue(AttributeNode attribute, PropertyInfo property) =>
        property.Name == ValueProperty && property.PropertyType == typeof(double) && attribute.Parts.Any(p => p.IsExpression);

    /// <summary>The next sample number, so that bars built from one <c>@foreach</c> differ: 30, 50, 20, 60…</summary>
    private double NextSample() => AttributeValues.SampleSeries[_samples++ % AttributeValues.SampleSeries.Count];

    /// <summary>Calls the parent's <c>Add</c> that takes the child, as the generated <c>parent.Add(child)</c> does.</summary>
    private void Add(object parent, object child)
    {
        var add = parent.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Add" && m.GetParameters() is [{ } parameter] && parameter.ParameterType.IsInstanceOfType(child));
        if (add is null)
            return;

        try
        {
            add.Invoke(parent, [child]);
        }
        catch (TargetInvocationException e) when (e.InnerException is ArgumentException argument)
        {
            // Such as two menu options with the same key
            problems.Add(argument.Message);
        }
    }

    /// <summary>Sample data for elements that show the application's state.</summary>
    private static void AddSamples(object element)
    {
        switch (element)
        {
            case BreadcrumbsElement breadcrumbs:
                breadcrumbs.Items = ["Home", "Page"];
                break;
            case ShortcutsElement shortcuts:
                shortcuts.Items = [new ShortcutHint("Esc", "Back"), new ShortcutHint("Ctrl+X", "Quit")];
                break;
        }
    }

    private static Dictionary<string, IReadOnlyList<string>> SampleErrors(string? field) =>
        new(StringComparer.OrdinalIgnoreCase) { [field ?? "Field"] = ["{validation message}"] };

    /// <summary>A dim note standing for something the preview cannot draw.</summary>
    private static TextElement Note(string what)
    {
        var note = new TextElement { Dim = true };
        note.AppendText("[" + what + "]");
        return note;
    }

    private static TextElement Text(string text)
    {
        var element = new TextElement();
        element.AppendText(text);
        return element;
    }
}
