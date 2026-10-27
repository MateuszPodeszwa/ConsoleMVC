#nullable enable

using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Diagnostics;

/// <summary>
/// Descriptors for every diagnostic the ConsoleMVC view generator can report. Each one points at the
/// offending location in the <c>.cvw</c> file.
/// </summary>
/// <remarks>See <c>docs/design/c-view-markup.md</c> §7 for the full table.</remarks>
internal static class CvwDiagnostics
{
    private const string Category = "ConsoleMVC.Views";

    /// <summary>CVW001: the file is not at <c>Views/{Controller}/{Name}.cvw</c>.</summary>
    public static readonly DiagnosticDescriptor MisplacedView = new(
        "CVW001", "View file is not in a controller folder",
        "View file '{0}' was skipped: views must be placed at Views/{{Controller}}/{{Action}}View.cvw",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <summary>CVW002: a file or folder name is not a valid C# identifier.</summary>
    public static readonly DiagnosticDescriptor InvalidIdentifier = new(
        "CVW002", "Invalid view or controller name",
        "'{0}' is not a valid C# identifier, so it cannot be used as a {1} name",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW003: the view's file name does not end with <c>View</c>, so the router cannot find it.</summary>
    public static readonly DiagnosticDescriptor ViewNameSuffix = new(
        "CVW003", "View name should end with 'View'",
        "View '{0}' will not be discovered by the router: rename the file to '{0}View.cvw'",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <summary>CVW100: unknown attribute on an element.</summary>
    public static readonly DiagnosticDescriptor UnknownAttribute = new(
        "CVW100", "Unknown attribute",
        "<{0}> has no attribute '{1}'. Valid attributes: {2}.",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW101: an attribute value is not valid for its type.</summary>
    public static readonly DiagnosticDescriptor InvalidAttributeValue = new(
        "CVW101", "Invalid attribute value",
        "Invalid value '{0}' for attribute '{1}': {2}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW102: missing or mismatched closing tag.</summary>
    public static readonly DiagnosticDescriptor UnclosedElement = new(
        "CVW102", "Element is not closed correctly",
        "{0}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW103: element not allowed in its parent.</summary>
    public static readonly DiagnosticDescriptor ElementNotAllowed = new(
        "CVW103", "Element not allowed here",
        "{0}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW104: unterminated expression, code block, string, comment or tag.</summary>
    public static readonly DiagnosticDescriptor Unterminated = new(
        "CVW104", "Unterminated construct",
        "Unterminated {0}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW105: unknown element inside markup.</summary>
    public static readonly DiagnosticDescriptor UnknownElement = new(
        "CVW105", "Unknown element",
        "Unknown element <{0}>. Known elements: {1}. To show a literal '<', write &lt; instead.",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW106: control flow or a code block inside text content.</summary>
    public static readonly DiagnosticDescriptor CodeInText = new(
        "CVW106", "Code not allowed inside text",
        "'@{0}' is not allowed inside <{1}>. Use an expression such as @(condition ? \"a\" : \"b\"), or move the logic outside the element.",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW107: an attribute is specified more than once.</summary>
    public static readonly DiagnosticDescriptor DuplicateAttribute = new(
        "CVW107", "Duplicate attribute",
        "Attribute '{0}' is specified more than once on <{1}>",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW109: an element-specific rule is broken (e.g. an option needs nav or quit).</summary>
    public static readonly DiagnosticDescriptor InvalidElement = new(
        "CVW109", "Invalid element",
        "{0}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW110: text or an expression inside an element that only accepts child elements.</summary>
    public static readonly DiagnosticDescriptor TextNotAllowed = new(
        "CVW110", "Text not allowed here",
        "Text and expressions are not allowed directly inside <{0}>; it may only contain {1}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW111: a required attribute is missing.</summary>
    public static readonly DiagnosticDescriptor MissingAttribute = new(
        "CVW111", "Required attribute missing",
        "<{0}> requires the '{1}' attribute",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>CVW108: malformed tag syntax.</summary>
    public static readonly DiagnosticDescriptor MalformedTag = new(
        "CVW108", "Malformed tag",
        "Malformed tag: {0}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
}
