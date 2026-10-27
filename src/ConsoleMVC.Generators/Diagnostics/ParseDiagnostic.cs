#nullable enable

using ConsoleMVC.Generators.Syntax;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Diagnostics;

/// <summary>
/// A diagnostic found while parsing, located by offset. Converted to a Roslyn <see cref="Diagnostic"/>
/// (with file, line and column) by the generator.
/// </summary>
/// <example><code>new ParseDiagnostic(CvwDiagnostics.UnknownAttribute, span, "box", "colur", "title, width");</code></example>
internal sealed class ParseDiagnostic
{
    /// <summary>
    /// Creates a parse diagnostic.
    /// </summary>
    /// <param name="descriptor">The diagnostic descriptor.</param>
    /// <param name="span">The offending source range.</param>
    /// <param name="arguments">Arguments for the descriptor's message format.</param>
    public ParseDiagnostic(DiagnosticDescriptor descriptor, SourceSpan span, params object[] arguments)
    {
        Descriptor = descriptor;
        Span = span;
        Arguments = arguments;
    }

    /// <summary>Gets the diagnostic descriptor.</summary>
    public DiagnosticDescriptor Descriptor { get; }

    /// <summary>Gets the offending source range.</summary>
    public SourceSpan Span { get; }

    /// <summary>Gets the message format arguments.</summary>
    public object[] Arguments { get; }

    /// <summary>Gets a value indicating whether this diagnostic prevents code generation.</summary>
    public bool IsError => Descriptor.DefaultSeverity == DiagnosticSeverity.Error;

    /// <summary>Gets the formatted message, for tests and logging.</summary>
    public string Message => string.Format(Descriptor.MessageFormat.ToString(), Arguments);

    /// <inheritdoc />
    public override string ToString() => $"{Descriptor.Id} {Span}: {Message}";
}
