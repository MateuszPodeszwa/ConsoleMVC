using ConsoleMVC.Generators.Parsing;
using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Syntax;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Unit tests for <see cref="CvwDocumentParser"/> and <see cref="MarkupParser"/>: deciding what is C# and what
/// is markup.
/// </summary>
public class ParserTests
{
    private static CvwDocument Parse(string text) => CvwDocumentParser.Parse(text, MarkupSchema.Default);

    private static IEnumerable<ElementNode> Elements(CvwDocument document) => document.Body.OfType<ElementNode>();

    // --- directives ---

    [Fact]
    public void Directives_AreParsed_WithExactSpans()
    {
        const string text = "@model  MyApp.Models.Home \n@using System.Text\n\nConsole.WriteLine();";

        var document = Parse(text);

        Assert.Equal("MyApp.Models.Home", document.Model!.Value);
        Assert.Equal("MyApp.Models.Home", text.Substring(document.Model.Span.Start, document.Model.Span.Length));
        Assert.Equal("System.Text", Assert.Single(document.Usings).Value);
    }

    [Fact]
    public void MissingModel_DefaultsToObject()
    {
        Assert.Equal("object", Parse("return NavigationResult.Quit();").ModelType);
    }

    // --- plain C# stays C# ---

    [Fact]
    public void PlainCSharpBody_IsOneCodeNode_Verbatim()
    {
        const string body = "var x = 1;\nif (x < 2) { Console.WriteLine(\"<text>\"); }\nreturn NavigationResult.Quit();";

        var document = Parse("@model object\n" + body);

        var code = Assert.IsType<CodeNode>(Assert.Single(document.Body));
        Assert.Equal(body, code.Code);
        Assert.Empty(document.Diagnostics);
    }

    [Theory]
    [InlineData("var ok = a <text;")]                             // comparison, not statement start
    [InlineData("var list = new List<text>();")]                  // generic argument
    [InlineData("Console.WriteLine(\"<text>hi</text>\");")]       // string
    [InlineData("Console.WriteLine(@\"<text>\"\"hi\"\"</text>\");")] // verbatim string
    [InlineData("Console.WriteLine($\"{x}<text>{(y ? \"}\" : \"<box>\")}\");")] // interpolation holes
    [InlineData("Console.WriteLine(\"\"\"<text>\"\"\");")]        // raw string
    [InlineData("// <text>commented</text>")]                    // line comment
    [InlineData("/* <box> */ var a = 1;")]                       // block comment
    [InlineData("var c = '<';")]                                 // char literal
    [InlineData("<Text>x</Text>")]                               // tags are case-sensitive
    [InlineData("<textual>")]                                    // not a known tag name
    public void LessThan_OutsideStatementStart_IsNotMarkup(string body)
    {
        Assert.Empty(Elements(Parse(body)));
    }

    [Theory]
    [InlineData("<text>a</text>")]
    [InlineData("Console.Write(1);<text>a</text>")]
    [InlineData("if (x) { <text>a</text> }")]
    [InlineData("switch (x) { case 1: <text>a</text> break; }")]
    [InlineData("foo();\n   <text>a</text>")]
    public void LessThan_AtStatementStart_IsMarkup(string body)
    {
        var element = Assert.Single(Elements(Parse(body)));
        Assert.Equal("text", element.Schema.Tag);
    }

    // --- element structure ---

    [Fact]
    public void Element_ParsesAttributesAndNestedContent()
    {
        var document = Parse("<box title=\"Menu\" width=40>x</box>");

        var box = Assert.Single(Elements(document));
        Assert.Equal(2, box.Attributes.Count);
        Assert.Equal("Title", box.Attributes[0].Schema.Property);
        Assert.Equal("global::ConsoleMVC.Rendering.Length.Fixed(40)", box.Attributes[1].LiteralCode);
        Assert.IsType<TextNode>(Assert.Single(box.Children));
    }

    [Fact]
    public void ControlFlow_InsideBox_ProducesCodeAndElements()
    {
        var document = Parse("<box>@foreach (var i in items) { <text>@i</text> }</box>");

        var box = Assert.Single(Elements(document));
        Assert.Collection(box.Children,
            n => Assert.Equal("foreach (var i in items) {", Assert.IsType<CodeNode>(n).Code),
            n => Assert.IsType<CodeNode>(n),   // whitespace before <text>
            n => Assert.IsType<ElementNode>(n),
            n => Assert.IsType<CodeNode>(n),   // whitespace after </text>
            n => Assert.Equal("}", Assert.IsType<CodeNode>(n).Code));
    }

    [Fact]
    public void IfElseChain_IsParsedAsOneStatement()
    {
        var document = Parse("<box>@if (a) { <text>1</text> } else if (b) { <text>2</text> } else { <text>3</text> }</box>");

        var box = Assert.Single(Elements(document));
        Assert.Equal(3, box.Children.OfType<ElementNode>().Count());
        Assert.Empty(document.Diagnostics);
    }

    [Theory]
    [InlineData("<text>@Model.Name.</text>", "Model.Name")]                 // trailing period excluded
    [InlineData("<text>@user?.Address!.City</text>", "user?.Address!.City")]
    [InlineData("<text>@items[0].ToString(\"x)\")</text>", "items[0].ToString(\"x)\")")]
    [InlineData("<text>@(a + b)</text>", "a + b")]
    public void Expressions_AreScannedCorrectly(string body, string expected)
    {
        var text = Assert.Single(Elements(Parse(body)));

        Assert.Equal(expected, text.Children.OfType<ExpressionNode>().Single().Code);
    }

    [Theory]
    [InlineData("<text>mail@example.com</text>", "mail@example.com")]
    [InlineData("<text>@@handle</text>", "@handle")]
    [InlineData("<text>a &lt; b &amp;&amp; c &#64; d</text>", "a < b && c @ d")]
    [InlineData("<text>a <!-- hidden --> b @* hidden *@</text>", "a  b ")]
    public void Text_LiteralsEntitiesAndComments(string body, string expected)
    {
        var text = Assert.Single(Elements(Parse(body)));

        Assert.Equal(expected, string.Concat(text.Children.OfType<TextNode>().Select(t => t.Text)));
        Assert.Empty(text.Children.OfType<ExpressionNode>());
    }
}
