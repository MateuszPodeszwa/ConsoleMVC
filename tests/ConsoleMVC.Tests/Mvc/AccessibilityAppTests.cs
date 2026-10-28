using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;
using ConsoleMVC.Tests.Mvc.LiveApp;
using ConsoleMVC.Tests.Mvc.LiveApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for <see cref="MvcApplicationBuilder.ScreenReader"/> and <see cref="MvcApplicationBuilder.HighContrast"/>.</summary>
[Collection(TestAppCollection.Name)]
public class AccessibilityAppTests
{
    private static MvcTestHost Host(Action<MvcApplicationBuilder> accessibility) =>
        MvcTestHost.For<DashboardController>(builder =>
        {
            builder.DefaultController = "Dashboard";
            builder.Services.AddSingleton<VisitCounter>();
            accessibility(builder);
        });

    [Fact]
    public void ScreenReader_DescribesLiveElements_AndNeverRefreshesInPlace()
    {
        var run = Host(builder => builder.ScreenReader = true).Run("i");

        Assert.Contains("Import: 100%", run.Output);
        Assert.DoesNotContain("█", run.Output);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("yes", false)]
    public void ScreenReader_CanBeChosenInTheSettings(string value, bool expected)
    {
        var run = Host(builder => builder.Configuration[MvcApplicationBuilder.ScreenReaderSetting] = value).Run("i");

        Assert.Equal(expected, run.Output.Contains("Import: 100%"));
    }

    [Fact]
    public void HighContrast_ReplacesTheTheme()
    {
        var builder = MvcApplication.CreateBuilder([]);
        builder.Configuration[MvcApplicationBuilder.HighContrastSetting] = "true";
        using var app = builder.Build();

        var muted = new TextElement();
        app.Services.GetRequiredService<Theme>().Apply(muted, "muted");

        Assert.False(muted.Dim);
    }
}
