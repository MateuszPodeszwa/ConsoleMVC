using System.Text.Json;
using ConsoleMVC.Generators.Schema;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Tests for <see cref="MarkupSchemaExport"/>: the schema editors read (<c>schema/cvw-schema.json</c>) matches the
/// generator's own schema. Run with <c>UPDATE_SCHEMA=1</c> to rewrite the checked-in file after changing the markup.
/// </summary>
public class SchemaExportTests
{
    private static readonly string SchemaPath = Path.Combine(RepositoryRoot(), "schema", "cvw-schema.json");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ConsoleMVC.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (ConsoleMVC.sln) was not found.");
    }

    private static JsonDocument Exported() => JsonDocument.Parse(MarkupSchemaExport.ToJson(MarkupSchema.Default));

    [Fact]
    public void TheCheckedInSchema_IsUpToDate()
    {
        var current = MarkupSchemaExport.ToJson(MarkupSchema.Default);
        if (Environment.GetEnvironmentVariable("UPDATE_SCHEMA") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SchemaPath)!);
            File.WriteAllText(SchemaPath, current);
        }

        Assert.True(File.Exists(SchemaPath), $"{SchemaPath} is missing; run the tests with UPDATE_SCHEMA=1 to create it.");

        // Line endings may be converted on checkout
        var checkedIn = File.ReadAllText(SchemaPath).Replace("\r\n", "\n");
        Assert.True(checkedIn == current,
            "schema/cvw-schema.json is out of date with MarkupSchema; run the generator tests with UPDATE_SCHEMA=1 and commit the file.");
    }

    [Fact]
    public void TheSchema_IsValidJson_AndDescribesEveryElementAndAttribute()
    {
        using var json = Exported();
        var root = json.RootElement;

        Assert.Equal(MarkupSchemaExport.FormatVersion, root.GetProperty("formatVersion").GetInt32());
        var elements = root.GetProperty("elements").EnumerateArray().ToList();
        Assert.Equal(MarkupSchema.Default.Elements.Count, elements.Count);

        foreach (var element in elements)
        {
            Assert.False(string.IsNullOrWhiteSpace(element.GetProperty("description").GetString()));
            foreach (var attribute in element.GetProperty("attributes").EnumerateArray())
                Assert.False(string.IsNullOrWhiteSpace(attribute.GetProperty("description").GetString()));
        }
    }

    [Fact]
    public void Elements_CarryTheirRules()
    {
        using var json = Exported();
        var elements = json.RootElement.GetProperty("elements").EnumerateArray()
            .ToDictionary(e => e.GetProperty("name").GetString()!);

        var menu = elements["menu"];
        Assert.True(menu.GetProperty("topLevelOnly").GetBoolean());
        Assert.Equal("elements", menu.GetProperty("content").GetString());
        Assert.Equal(["option"], menu.GetProperty("children").EnumerateArray().Select(c => c.GetString()));

        var option = elements["option"];
        Assert.Equal(["menu"], option.GetProperty("parents").EnumerateArray().Select(p => p.GetString()));
        var key = option.GetProperty("attributes").EnumerateArray().Single(a => a.GetProperty("name").GetString() == "key");
        Assert.True(key.GetProperty("required").GetBoolean());

        Assert.Equal(JsonValueKind.Null, elements["box"].GetProperty("parents").ValueKind);
        Assert.Equal("block", elements["box"].GetProperty("content").GetString());
    }

    [Fact]
    public void Attributes_CarryTheirValues()
    {
        using var json = Exported();
        var box = json.RootElement.GetProperty("elements").EnumerateArray().Single(e => e.GetProperty("name").GetString() == "box");
        var attributes = box.GetProperty("attributes").EnumerateArray().ToDictionary(a => a.GetProperty("name").GetString()!);

        Assert.Equal("choice", attributes["border"].GetProperty("kind").GetString());
        Assert.Contains("rounded", attributes["border"].GetProperty("values").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("integer", attributes["padding"].GetProperty("kind").GetString());
        Assert.Equal(0, attributes["padding"].GetProperty("minimum").GetInt32());
        Assert.Equal(20, attributes["padding"].GetProperty("maximum").GetInt32());

        var colours = attributes["colour"].GetProperty("values").EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.Contains("cyan", colours);
        Assert.Contains("darkgrey", colours);
        Assert.Contains("default", colours);
        Assert.True(attributes["title"].GetProperty("interpolation").GetBoolean());
    }

    [Fact]
    public void EveryValueTheSchemaOffers_IsAcceptedByTheGenerator()
    {
        // An editor completes these values; each must also pass the generator's own check
        foreach (var element in MarkupSchema.Default.Elements)
        {
            foreach (var attribute in element.Attributes)
            {
                foreach (var value in attribute.Type.Values)
                    Assert.True(attribute.Type.TryTranslateLiteral(value, out _, out _), $"<{element.Tag} {attribute.Name}=\"{value}\">");
            }
        }
    }

    [Fact]
    public void DirectivesAndDiagnostics_AreListed()
    {
        using var json = Exported();
        var root = json.RootElement;

        Assert.Equal(["@model", "@using", "@inject", "@layout", "@refresh"],
            root.GetProperty("directives").EnumerateArray().Select(d => d.GetProperty("name").GetString()));

        var diagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("id").GetString()).ToList();
        Assert.Contains("CVW001", diagnostics);
        Assert.Contains("CVW121", diagnostics);
        Assert.Equal(diagnostics.OrderBy(d => d, StringComparer.Ordinal), diagnostics);
    }
}
