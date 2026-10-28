using ConsoleMVC.Mvc;
using ConsoleMVC.Testing;
using ConsoleMVC.Tests.Mvc.AreaApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for areas and modules (docs/design/areas-and-modules.md).</summary>
[Collection(TestAppCollection.Name)]
public class AreaTests
{
    private static MvcTestHost Host() => MvcTestHost.For<PortalController>(builder => builder.DefaultController = "Portal");

    [Fact]
    public void AreaRoutes_HaveThreeParts_AndFindTheAreasViews()
    {
        var run = Host().Run("1", "q");

        Assert.Equal(["Portal/Index", "Admin/Users/Index"], run.Routes);
        Assert.Contains("Admin users Portal > Users", run.LastScreen); // breadcrumbs show the controller, not the area
    }

    [Fact]
    public void TwoPartTargets_InsideAnArea_PreferTheArea()
    {
        var run = Host().Run("1", "p");

        Assert.Equal("Admin/Portal/Index", run.Routes[^1]);
        Assert.Contains("Admin portal", run.LastScreen);
    }

    [Fact]
    public void Redirects_InsideAnArea_PreferTheArea()
    {
        var run = Host().Run("1", "l");

        Assert.Equal(["Portal/Index", "Admin/Users/Index", "Admin/Users/Leave", "Admin/Portal/Index"], run.Routes);
    }

    [Fact]
    public void TheAreaAttribute_PutsAControllerInAnArea()
    {
        var run = Host().Run("2");

        Assert.Equal("Reports/Sales/Index", run.Routes[^1]);
        Assert.Contains("Sales report in Reports", run.LastScreen);
    }

    [Fact]
    public void AreaControllers_AreOnlyReachedThroughTheirArea()
    {
        var host = MvcTestHost.For<PortalController>(builder => builder.DefaultController = "Users");

        var error = Assert.Throws<InvalidOperationException>(() => host.Run("q"));

        Assert.Contains("Users", error.Message);
        Assert.Contains("Admin/Users", error.Message); // the message names the area route
    }

    [Theory]
    [InlineData("Admin/Users/Index", "Admin/Users", "Index")]
    [InlineData("Admin/Users/Details?id=4", "Admin/Users", "Details")]
    [InlineData("Users/Index", "Users", "Index")]
    [InlineData("Index", null, "Index")]
    public void ParseTarget_AcceptsAnArea(string target, string? controller, string action)
    {
        var (parsedController, parsedAction, _) = RouteValues.ParseTarget(target);

        Assert.Equal(controller, parsedController);
        Assert.Equal(action, parsedAction);
    }

    [Fact]
    public void RouteContext_KnowsItsArea()
    {
        Assert.Equal("Admin", new RouteContext { Controller = "Admin/Users", Action = "Index" }.Area);
        Assert.Null(new RouteContext { Controller = "Users", Action = "Index" }.Area);
        Assert.Equal("Users", new RouteContext { Controller = "Admin/Users", Action = "Index" }.ControllerName);
    }

    // ------------------------------------------------------------------ modules

    /// <summary>A module that registers a service; its assembly (here the test assembly) brings controllers and views.</summary>
    public sealed class PortalModule : IMvcModule
    {
        public static int Configured;

        public void Configure(MvcApplicationBuilder builder)
        {
            Configured++;
            builder.Services.AddSingleton(new ModuleMarker());
        }
    }

    public sealed class ModuleMarker;

    [Fact]
    public void Modules_AddTheirAssembly_AndConfigureTheBuilder()
    {
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(MvcTestHost).Assembly; // the testing package: no controllers of its own
        builder.DefaultController = "Portal";
        builder.Input = new Rendering.ScriptedInput("q");
        builder.Renderer = new ConsoleMVC.Rendering.MarkupRenderer(new ConsoleMVC.Rendering.PlainTextRenderTarget(new StringWriter()), () => 40);

        builder.AddModule<PortalModule>();
        builder.AddModule<PortalModule>(); // once is enough

        Assert.Equal(1, PortalModule.Configured);
        Assert.Equal([typeof(PortalModule).Assembly], builder.Modules);

        using var app = builder.Build();
        Assert.NotNull(app.Services.GetService<ModuleMarker>());
        app.Run(); // Portal is found in the module
    }
}
