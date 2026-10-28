#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ConsoleMVC.Generators.Diagnostics;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Schema;

/// <summary>
/// Writes the C-View Markup schema as JSON (<c>schema/cvw-schema.json</c>) for editors: every element with its
/// attributes, value kinds and allowed values, where it may appear and what it may contain, the directives, the C#
/// constructs and the diagnostics, each with a description. Editor plugins read this file instead of keeping their own
/// copy of the markup, so they stay in step with the framework.
/// </summary>
/// <remarks>
/// The generator tests compare the checked-in file with <see cref="ToJson"/>, and rewrite it when run with
/// <c>UPDATE_SCHEMA=1</c>. The format is described by <c>formatVersion</c>; fields are only ever added.
/// </remarks>
/// <example><code>File.WriteAllText("schema/cvw-schema.json", MarkupSchemaExport.ToJson(MarkupSchema.Default));</code></example>
internal static class MarkupSchemaExport
{
    /// <summary>The version of the JSON format, raised when a field changes meaning.</summary>
    public const int FormatVersion = 1;

    /// <summary>Writes <paramref name="schema"/> as indented JSON with a final line break.</summary>
    /// <param name="schema">The schema to write, usually <see cref="MarkupSchema.Default"/>.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an element or attribute has no description.</exception>
    public static string ToJson(MarkupSchema schema)
    {
        var json = new JsonWriter();
        json.BeginObject();
        json.Property("formatVersion", FormatVersion);
        json.Property("description", "The elements, attributes, directives and diagnostics of ConsoleMVC C-View Markup (.cvw), for editors.");

        json.Name("directives");
        json.BeginArray();
        foreach (var (name, syntax, description) in MarkupDocumentation.Directives)
        {
            json.BeginObject();
            json.Property("name", name);
            json.Property("syntax", syntax);
            json.Property("description", description);
            json.EndObject();
        }

        json.EndArray();

        json.Name("constructs");
        json.BeginArray();
        foreach (var (syntax, description) in MarkupDocumentation.Constructs)
        {
            json.BeginObject();
            json.Property("syntax", syntax);
            json.Property("description", description);
            json.EndObject();
        }

        json.EndArray();

        json.Name("elements");
        json.BeginArray();
        foreach (var element in schema.Elements)
            WriteElement(json, element);
        json.EndArray();

        json.Name("diagnostics");
        json.BeginArray();
        foreach (var descriptor in Diagnostics().OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            json.BeginObject();
            json.Property("id", descriptor.Id);
            json.Property("severity", descriptor.DefaultSeverity.ToString().ToLowerInvariant());
            json.Property("title", descriptor.Title.ToString(CultureInfo.InvariantCulture));
            json.EndObject();
        }

        json.EndArray();
        json.EndObject();
        return json.ToString();
    }

    private static void WriteElement(JsonWriter json, ElementSchema element)
    {
        if (!MarkupDocumentation.Elements.TryGetValue(element.Tag, out var description))
            throw new InvalidOperationException($"<{element.Tag}> has no description in MarkupDocumentation.Elements.");

        json.BeginObject();
        json.Property("name", element.Tag);
        json.Property("description", description);
        json.Property("content", element.Content switch
        {
            ContentModel.Void => "void",
            ContentModel.Inline => "inline",
            ContentModel.Block => "block",
            _ => "elements"
        });
        json.Property("topLevelOnly", element.TopLevelOnly);
        json.StringArrayProperty("parents", element.RequiredParents);
        json.StringArrayProperty("children", element.AllowedChildren);

        json.Name("attributes");
        json.BeginArray();
        foreach (var attribute in element.Attributes)
        {
            var attributeDescription = MarkupDocumentation.Describe(element.Tag, attribute)
                                       ?? throw new InvalidOperationException(
                                           $"<{element.Tag} {attribute.Name}> ({attribute.Property}) has no description in MarkupDocumentation.Attributes.");
            var type = attribute.Type;

            json.BeginObject();
            json.Property("name", attribute.Name);
            json.Property("description", attributeDescription);
            json.Property("required", attribute.Required);
            json.Property("kind", type.Kind);
            json.Property("accepts", type.Description);
            json.Property("flag", type.AllowsFlag);
            json.Property("interpolation", type.AllowsInterpolation);
            json.Property("expression", type.AllowsExpression);
            if (type.Values.Count > 0)
                json.StringArrayProperty("values", type.Values);
            if (type.Minimum is { } minimum)
                json.Property("minimum", minimum);
            if (type.Maximum is { } maximum)
                json.Property("maximum", maximum);
            json.EndObject();
        }

        json.EndArray();
        json.EndObject();
    }

    /// <summary>Every descriptor declared in <see cref="CvwDiagnostics"/>.</summary>
    private static IEnumerable<DiagnosticDescriptor> Diagnostics() =>
        typeof(CvwDiagnostics).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!);

    /// <summary>A minimal indented JSON writer, so the generator needs no JSON library.</summary>
    private sealed class JsonWriter
    {
        private readonly StringBuilder _text = new();
        private readonly Stack<bool> _hasItems = new();
        private bool _afterName;

        public void BeginObject() => Open('{');

        public void EndObject() => Close('}');

        public void BeginArray() => Open('[');

        public void EndArray() => Close(']');

        public void Name(string name)
        {
            Separate();
            WriteString(name);
            _text.Append(": ");
            _afterName = true;
        }

        public void Property(string name, string value)
        {
            Name(name);
            Value(value);
        }

        public void Property(string name, int value)
        {
            Name(name);
            Separate();
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Property(string name, bool value)
        {
            Name(name);
            Separate();
            _text.Append(value ? "true" : "false");
        }

        /// <summary>Writes an array of strings on one line, or <c>null</c>.</summary>
        public void StringArrayProperty(string name, IReadOnlyList<string>? values)
        {
            Name(name);
            Separate();
            if (values is null)
            {
                _text.Append("null");
                return;
            }

            _text.Append('[');
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    _text.Append(", ");
                WriteString(values[i]);
            }

            _text.Append(']');
        }

        private void Value(string value)
        {
            Separate();
            WriteString(value);
        }

        private void Open(char bracket)
        {
            Separate();
            _text.Append(bracket);
            _hasItems.Push(false);
        }

        private void Close(char bracket)
        {
            var hadItems = _hasItems.Pop();
            if (hadItems)
            {
                _text.Append('\n');
                Indent();
            }

            _text.Append(bracket);
            if (_hasItems.Count == 0)
                _text.Append('\n');
        }

        /// <summary>Starts the next item: a comma and a new line, unless it is the value after a name.</summary>
        private void Separate()
        {
            if (_afterName)
            {
                _afterName = false;
                return;
            }

            if (_hasItems.Count == 0)
                return;

            if (_hasItems.Peek())
                _text.Append(',');
            _hasItems.Pop();
            _hasItems.Push(true);
            _text.Append('\n');
            Indent();
        }

        private void Indent() => _text.Append(' ', _hasItems.Count * 2);

        private void WriteString(string value)
        {
            _text.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': _text.Append("\\\""); break;
                    case '\\': _text.Append("\\\\"); break;
                    case '\n': _text.Append("\\n"); break;
                    case '\r': _text.Append("\\r"); break;
                    case '\t': _text.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            _text.Append(c);
                        break;
                }
            }

            _text.Append('"');
        }

        public override string ToString() => _text.ToString();
    }
}
