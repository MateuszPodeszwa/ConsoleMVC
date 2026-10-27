using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// End-to-end tests for C-View Markup v0.2: tables, menus and inputs.
/// </summary>
public class InteractiveMarkupTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private const string Models = """
        using System.Collections.Generic;

        namespace TestApp
        {
            public record User(string Name, int Age);

            public class Users
            {
                public List<User> All { get; } = [new("Ada", 36), new("Linus", 54)];
            }
        }
        """;

    [Fact]
    public void Table_WithHeaderAndLoop()
    {
        const string view = """
            @model TestApp.Users
            <table>
                <row header>
                    <cell>Name</cell>
                    <cell align="right">Age</cell>
                </row>
                @foreach (var user in Model.All)
                {
                    <row>
                        <cell>@user.Name</cell>
                        <cell align="right">@user.Age</cell>
                    </row>
                }
            </table>
            """;

        var output = ViewHarness.Render(view + Quit, "new TestApp.Users()", Models);

        Assert.Equal(
            """
            ┌───────┬─────┐
            │ Name  │ Age │
            ├───────┼─────┤
            │ Ada   │  36 │
            │ Linus │  54 │
            └───────┴─────┘

            """.Replace("\r\n", "\n"), output);
    }

    [Fact]
    public void Table_InsideBox()
    {
        const string view = """
            <box title="T" padding="0" border="ascii">
                <table border="ascii" padding="0"><row><cell>x</cell><cell>y</cell></row></table>
            </box>
            """;

        Assert.Equal("+- T -+\n|+-+-+|\n||x|y||\n|+-+-+|\n+-----+\n", ViewHarness.Render(view + Quit));
    }

    [Theory]
    [InlineData("1", "Home", "About", false)]
    [InlineData("2", null, "Search", true)]
    public void Menu_ReturnsNavigationForChosenOption(string choice, string? controller, string action, bool posted)
    {
        const string view = """
            <menu prompt="> ">
                <option key="1" nav="Home/About">About</option>
                <option key="2" nav="Search" post>Search</option>
                <option key="q" quit>Quit</option>
            </menu>
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view, input: choice);

        Assert.Equal("[1] About\n[2] Search\n[q] Quit\n> ", output);
        Assert.Equal(controller, navigation.Controller);
        Assert.Equal(action, navigation.Action);
        Assert.Equal(posted, navigation.FormData is not null);
    }

    [Fact]
    public void Menu_QuitOption_Quits()
    {
        var (_, navigation) = ViewHarness.RenderWithNavigation(
            "<menu><option key=\"q\" quit>Quit</option></menu>", input: "Q");

        Assert.True(navigation.Exit);
    }

    [Fact]
    public void Menu_GeneratedFromModel()
    {
        const string view = """
            @model TestApp.Users
            <menu>
                @for (var i = 0; i < Model.All.Count; i++)
                {
                    <option key="@(i + 1)" nav="Users/Details">@Model.All[i].Name</option>
                }
                <option key="b" nav="Home/Index">Back</option>
            </menu>
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view, "new TestApp.Users()", Models, input: "2");

        Assert.StartsWith("[1] Ada\n[2] Linus\n[b] Back\n", output);
        Assert.Equal("Details", navigation.Action);
    }

    [Fact]
    public void Inputs_CollectForm_AndPostViaMenu()
    {
        const string view = """
            <input name="Name" label="Name: " required />
            <input name="Colour" label="Colour: " default="blue" />
            <menu prompt="> ">
                <option key="s" nav="Greet/Result" post>Submit</option>
            </menu>
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view, input: ["", "Ada", "", "s"]);

        Assert.Equal("Name: This field is required.\nName: Colour: [s] Submit\n> ", output);
        Assert.Equal("Ada", navigation.FormData!["Name"]);
        Assert.Equal("blue", navigation.FormData["Colour"]);
    }

    [Fact]
    public void Inputs_FormUsableFromCode()
    {
        const string view = """
            <input name="a" />
            <input name="b" />
            var total = int.Parse(Form["a"]) + int.Parse(Form["b"]);
            <text>Total: @total</text>
            return NavigationResult.ToAction("Result", Form);
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view, input: ["2", "3"]);

        Assert.Equal("Total: 5\n", output);
        Assert.Equal("3", navigation.FormData!["b"]);
    }

    [Fact]
    public void Input_InsideTopLevelIf_IsAllowed()
    {
        const string view = """
            if (true)
            {
                <input name="x" label="x: " />
            }
            """;

        Assert.Equal("x: ", ViewHarness.Render(view + Quit, input: "1"));
    }
}
