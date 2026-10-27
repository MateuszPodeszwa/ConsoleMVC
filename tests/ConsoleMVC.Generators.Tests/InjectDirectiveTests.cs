using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Tests for the <c>@inject Type Name</c> directive (docs/design/dependency-injection.md).
/// </summary>
public class InjectDirectiveTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private const string Services = """
        namespace TestApp
        {
            public interface IGreeter { string Greet(string name); }
            public sealed class Greeter : IGreeter { public string Greet(string name) => "Hello, " + name; }
        }
        """;

    private static void AssertNoErrors(GenerationResult result) =>
        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());

    [Fact]
    public void Inject_GeneratesAnInjectableProperty()
    {
        var result = ViewHarness.Generate("@using TestApp\n@inject IGreeter Greeter" + Quit, extraSource: Services);

        AssertNoErrors(result);
        var property = result.Assembly!.GetType("TestApp.Views.Home.IndexView")!.GetProperty("Greeter")!;
        Assert.Equal("TestApp.IGreeter", property.PropertyType.FullName);
        Assert.True(property.IsDefined(typeof(InjectAttribute), inherit: false));
    }

    [Fact]
    public void Inject_PropertyIsUsableInTheView()
    {
        var services = new ServiceCollection();
        var output = ViewHarness.RenderWithServices(
            "@using TestApp\n@inject IGreeter Greeter\n<text>@Greeter.Greet(\"Ada\")</text>" + Quit,
            assembly => services.AddSingleton(
                assembly.GetType("TestApp.IGreeter")!, assembly.GetType("TestApp.Greeter")!).BuildServiceProvider(),
            Services);

        Assert.Equal("Hello, Ada\n", output);
    }

    [Fact]
    public void Inject_AcceptsGenericTypesWithSpaces_AndATrailingSemicolon()
    {
        var result = ViewHarness.Generate(
            "@inject Dictionary<string, int> Counts;\n<text>@Counts.Count</text>" + Quit);

        AssertNoErrors(result);
        Assert.NotNull(result.Assembly!.GetType("TestApp.Views.Home.IndexView")!.GetProperty("Counts"));
    }

    [Fact]
    public void Inject_WorksAlongsideModelAndUsingInAnyOrder()
    {
        var result = ViewHarness.Generate(
            "@inject TestApp.IGreeter Greeter\n@model string\n@using System.Text\n<text>@Greeter.Greet(Model)</text>" + Quit,
            extraSource: Services);

        AssertNoErrors(result);
    }

    [Fact]
    public void Inject_UnknownType_IsReportedAtTheDirective()
    {
        var result = ViewHarness.Generate("@model string\n@inject   INope Thing" + Quit);

        var error = Assert.Single(result.CompilationDiagnostics, d => d.Id == "CS0246");
        var position = error.Location.GetMappedLineSpan().StartLinePosition;
        Assert.Equal((1, 10), (position.Line, position.Character));
        Assert.EndsWith("IndexView.cvw", error.Location.GetMappedLineSpan().Path);
    }

    [Theory]
    [InlineData("@inject IGreeter")]                 // no property name
    [InlineData("@inject IGreeter 1Greeter")]        // not an identifier
    [InlineData("@inject IGreeter class")]           // keyword
    [InlineData("@inject IGreeter Model")]           // the Render parameter
    [InlineData("@inject IGreeter Renderer")]        // a ConsoleView member
    [InlineData("@inject IGreeter __cvw_e1")]        // reserved prefix
    public void InvalidInject_ReportsCVW007(string directive)
    {
        var result = ViewHarness.Generate("@using TestApp\n" + directive + Quit, extraSource: Services);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW007");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(1, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void DuplicateInjectName_ReportsCVW008()
    {
        var result = ViewHarness.Generate(
            "@using TestApp\n@inject IGreeter Greeter\n@inject IGreeter Greeter" + Quit, extraSource: Services);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW008");
        Assert.Equal(2, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }
}
