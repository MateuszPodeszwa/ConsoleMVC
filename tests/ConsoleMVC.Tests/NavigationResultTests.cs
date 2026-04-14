using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests;

public class NavigationResultTests
{
    [Fact]
    public void To_WithoutFormData_SetsControllerAndAction()
    {
        var result = NavigationResult.To("Home", "Index");

        Assert.Equal("Home", result.Controller);
        Assert.Equal("Index", result.Action);
        Assert.Null(result.FormData);
        Assert.False(result.Exit);
    }

    [Fact]
    public void To_WithFormData_SetsAllProperties()
    {
        var formData = new Dictionary<string, string> { ["Name"] = "Alice" };

        var result = NavigationResult.To("Greet", "Result", formData);

        Assert.Equal("Greet", result.Controller);
        Assert.Equal("Result", result.Action);
        Assert.Same(formData, result.FormData);
    }

    [Fact]
    public void ToAction_WithoutFormData_SetsActionOnly()
    {
        var result = NavigationResult.ToAction("About");

        Assert.Equal("About", result.Action);
        Assert.Null(result.Controller);
        Assert.Null(result.FormData);
    }

    [Fact]
    public void ToAction_WithFormData_SetsActionAndFormData()
    {
        var formData = new Dictionary<string, string> { ["Key"] = "Value" };

        var result = NavigationResult.ToAction("Process", formData);

        Assert.Equal("Process", result.Action);
        Assert.Null(result.Controller);
        Assert.Same(formData, result.FormData);
    }

    [Fact]
    public void Quit_SetsExitTrue()
    {
        var result = NavigationResult.Quit();

        Assert.True(result.Exit);
        Assert.Null(result.FormData);
    }
}
