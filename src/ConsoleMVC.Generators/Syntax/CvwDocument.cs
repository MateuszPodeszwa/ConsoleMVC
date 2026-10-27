#nullable enable

using System.Collections.Generic;
using System.Linq;
using ConsoleMVC.Generators.Diagnostics;

namespace ConsoleMVC.Generators.Syntax;

/// <summary>
/// A directive argument (the type after <c>@model</c> or the namespace after <c>@using</c>) and its location.
/// </summary>
internal sealed class DirectiveValue
{
    /// <summary>
    /// Creates a directive value.
    /// </summary>
    /// <param name="value">The trimmed argument text.</param>
    /// <param name="span">Location of the argument in the source.</param>
    public DirectiveValue(string value, SourceSpan span)
    {
        Value = value;
        Span = span;
    }

    /// <summary>Gets the trimmed argument text.</summary>
    public string Value { get; }

    /// <summary>Gets the location of the argument in the source.</summary>
    public SourceSpan Span { get; }
}

/// <summary>
/// An <c>@inject Type Name</c> directive: a service the framework assigns to a generated view property.
/// </summary>
/// <example><code>@inject IGreetingService Greetings</code></example>
internal sealed class InjectDirective
{
    /// <summary>
    /// Creates an inject directive.
    /// </summary>
    /// <param name="type">The service type, as written.</param>
    /// <param name="name">The property name.</param>
    public InjectDirective(DirectiveValue type, DirectiveValue name)
    {
        Type = type;
        Name = name;
    }

    /// <summary>Gets the service type and its location.</summary>
    public DirectiveValue Type { get; }

    /// <summary>Gets the property name and its location.</summary>
    public DirectiveValue Name { get; }
}

/// <summary>
/// The result of parsing a <c>.cvw</c> file: its directives, body and any diagnostics.
/// </summary>
internal sealed class CvwDocument
{
    /// <summary>The model type used when a view has no <c>@model</c> directive.</summary>
    public const string DefaultModelType = "object";

    /// <summary>
    /// Creates a document.
    /// </summary>
    /// <param name="text">The full source text.</param>
    /// <param name="model">The <c>@model</c> directive, or <see langword="null"/> if absent.</param>
    /// <param name="usings">The <c>@using</c> directives in order.</param>
    /// <param name="injects">The valid <c>@inject</c> directives in order.</param>
    /// <param name="body">The parsed body nodes.</param>
    /// <param name="diagnostics">Problems found while parsing.</param>
    public CvwDocument(string text, DirectiveValue? model, IReadOnlyList<DirectiveValue> usings,
        IReadOnlyList<InjectDirective> injects, IReadOnlyList<Node> body, IReadOnlyList<ParseDiagnostic> diagnostics)
    {
        Text = text;
        Model = model;
        Usings = usings;
        Injects = injects;
        Body = body;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the full source text.</summary>
    public string Text { get; }

    /// <summary>Gets the <c>@model</c> directive, or <see langword="null"/> when the view declares none.</summary>
    public DirectiveValue? Model { get; }

    /// <summary>Gets the model type name, defaulting to <see cref="DefaultModelType"/>.</summary>
    public string ModelType => Model?.Value ?? DefaultModelType;

    /// <summary>Gets the <c>@using</c> directives in source order.</summary>
    public IReadOnlyList<DirectiveValue> Usings { get; }

    /// <summary>Gets the <c>@inject</c> directives in source order.</summary>
    public IReadOnlyList<InjectDirective> Injects { get; }

    /// <summary>Gets the parsed body.</summary>
    public IReadOnlyList<Node> Body { get; }

    /// <summary>Gets the problems found while parsing.</summary>
    public IReadOnlyList<ParseDiagnostic> Diagnostics { get; }

    /// <summary>Gets a value indicating whether any diagnostic is an error.</summary>
    public bool HasErrors => Diagnostics.Any(d => d.IsError);
}
