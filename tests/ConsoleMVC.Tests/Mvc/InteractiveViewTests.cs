using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Rendering;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for <see cref="ConsoleView.ReadInput"/>, <see cref="ConsoleView.RunMenu"/> and <see cref="ConsoleView.Form"/>.
/// </summary>
public class InteractiveViewTests
{
    /// <summary>A view whose body is supplied by each test.</summary>
    private sealed class LambdaView(Func<LambdaView, NavigationResult> body) : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => body(this);

        public string CallReadInput(InputElement input) => ReadInput(input);

        public NavigationResult CallRunMenu(MenuElement menu) => RunMenu(menu);
    }

    private static LambdaView View(ITextInput input, Func<LambdaView, NavigationResult>? body = null)
    {
        var view = new LambdaView(body ?? (_ => NavigationResult.Quit()))
        {
            Renderer = new MarkupRenderer(new PlainTextRenderTarget(TextWriter.Null), () => 40),
            Input = input
        };
        return view;
    }

    private static MenuElement Menu(params MenuOptionElement[] options)
    {
        var menu = new MenuElement();
        foreach (var option in options)
        {
            option.AppendText(option.Key);
            menu.Add(option);
        }

        return menu;
    }

    [Fact]
    public void ReadInput_StoresValueInForm()
    {
        var view = View(new ScriptedInput("Ada"));

        Assert.Equal("Ada", view.CallReadInput(new InputElement { Name = "Name" }));
        Assert.Equal("Ada", view.Form["name"]); // case-insensitive
    }

    [Fact]
    public void ReadInput_EndOfInput_EndsTheRunWithoutStoringAValue()
    {
        var view = View(new ScriptedInput());

        Assert.Throws<InputEndedException>(() => view.CallReadInput(new InputElement { Name = "Name" }));
        Assert.False(view.Form.ContainsKey("Name"));
    }

    [Theory]
    [InlineData("Home/About", "Home", "About")]
    [InlineData("About", null, "About")]
    public void RunMenu_Target_MapsToNavigation(string target, string? controller, string action)
    {
        var view = View(new ScriptedInput("1"));

        var navigation = view.CallRunMenu(Menu(new MenuOptionElement { Key = "1", Target = target }));

        Assert.Equal(controller, navigation.Controller);
        Assert.Equal(action, navigation.Action);
        Assert.Null(navigation.FormData);
    }

    [Fact]
    public void RunMenu_Quit_Quits()
    {
        var view = View(new ScriptedInput("q"));

        Assert.True(view.CallRunMenu(Menu(new MenuOptionElement { Key = "q", Quit = true })).Exit);
    }

    [Fact]
    public void RunMenu_EndOfInput_Quits()
    {
        var view = View(new ScriptedInput());

        Assert.True(view.CallRunMenu(Menu(new MenuOptionElement { Key = "1", Target = "A" })).Exit);
    }

    [Fact]
    public void RunMenu_PostForm_PostsACopyOfTheForm()
    {
        var view = View(new ScriptedInput("Ada", "s"));
        view.CallReadInput(new InputElement { Name = "Name" });

        var navigation = view.CallRunMenu(Menu(new MenuOptionElement { Key = "s", Target = "Greet/Result", PostForm = true }));
        view.Form["Name"] = "changed later";

        Assert.Equal("Ada", navigation.FormData!["Name"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b/c/d")]
    [InlineData("/About")]
    public void RunMenu_InvalidTarget_ThrowsDescriptiveError(string target)
    {
        var view = View(new ScriptedInput("1"));

        var error = Assert.Throws<InvalidOperationException>(() =>
            view.CallRunMenu(Menu(new MenuOptionElement { Key = "1", Target = target })));

        Assert.Contains("Controller/Action", error.Message);
    }

    [Fact]
    public void Application_GivesViewsTheConfiguredInput()
    {
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(InteractiveViewTests).Assembly;
        builder.DefaultController = "Shop";
        builder.DefaultAction = "Ask";
        builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(TextWriter.Null), () => 40);
        builder.Input = new ScriptedInput("Ada");

        TestApp.Views.Shop.AskView.Answer = null;
        builder.Build().Run();

        Assert.Equal("Ada", TestApp.Views.Shop.AskView.Answer);
    }
}
