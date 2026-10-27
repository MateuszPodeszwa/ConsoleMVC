using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.DiApp;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for dependency injection: <see cref="MvcApplicationBuilder.Services"/>, constructor injection into
/// controllers, <see cref="InjectAttribute"/> properties on views and one scope per navigation cycle
/// (docs/design/dependency-injection.md).
/// </summary>
[Collection(TestAppCollection.Name)]
public class DependencyInjectionTests
{
    /// <summary>A renderer that does nothing.</summary>
    private sealed class NullRenderer : IMarkupRenderer
    {
        public int Width => 80;
        public void Render(Element element) { }
        public void WritePrompt(string text, TextStyle style) { }
        public void Clear() { }
    }

    private static MvcApplicationBuilder CreateBuilder(string action = "Index", int screens = 1)
    {
        DiScript.Reset(screens);
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(DependencyInjectionTests).Assembly;
        builder.DefaultController = "Greeting";
        builder.DefaultAction = action;
        builder.Renderer = new NullRenderer();
        builder.Services.AddSingleton<IGreeter, Greeter>();
        builder.Services.AddScoped<UnitOfWork>();
        return builder;
    }

    [Fact]
    public void Run_InjectsServicesIntoControllerConstructorAndViewProperties()
    {
        using var app = CreateBuilder().Build();

        app.Run();

        Assert.Equal(["action:Hello, Ada", "view:Hello, Grace"], DiScript.Log);
    }

    [Fact]
    public void Run_SharesScopedServicesWithinOneCycle_AndCreatesNewOnesPerCycle()
    {
        using var app = CreateBuilder(screens: 2).Build();

        app.Run();

        // [controller, view] for the first screen, then the same for the second
        Assert.Equal(4, DiScript.Units.Count);
        Assert.Same(DiScript.Units[0], DiScript.Units[1]);
        Assert.Same(DiScript.Units[2], DiScript.Units[3]);
        Assert.NotSame(DiScript.Units[0], DiScript.Units[2]);
    }

    [Fact]
    public void Run_DisposesTheScopeAndTheController_AfterEachCycle()
    {
        using var app = CreateBuilder(screens: 2).Build();

        app.Run();

        Assert.All(DiScript.Units, unit => Assert.True(unit.Disposed));
        Assert.Equal(2, DiScript.Disposed.Count);
    }

    [Fact]
    public void Run_RedirectStartsANewCycle()
    {
        using var app = CreateBuilder("Bounce").Build();

        app.Run();

        Assert.Equal(["action:Hello, Ada", "view:Hello, Grace"], DiScript.Log);
        Assert.Equal(2, DiScript.Disposed.Count); // the Bounce controller and the Index controller
    }

    [Fact]
    public void Run_PartialViewsReceiveInjectedServices()
    {
        using var app = CreateBuilder("WithPartial").Build();

        app.Run();

        Assert.Equal(["partial:Hello, Ada"], DiScript.Log);
    }

    [Fact]
    public void PartialRenderedOutsideTheApplication_FailsClearlyWhenItNeedsServices()
    {
        var view = new DiApp.Views.Greeting.WithPartialView();

        var error = Assert.Throws<InvalidOperationException>(() => view.Render(new object()));

        Assert.Contains(nameof(IGreeter), error.Message);
    }

    [Fact]
    public void Run_ViewInjectingAnUnregisteredService_ThrowsWithAClearMessage()
    {
        using var app = CreateBuilder("Missing").Build();

        var error = Assert.Throws<InvalidOperationException>(app.Run);

        Assert.Contains("MissingView", error.Message);
        Assert.Contains(nameof(IMissingService), error.Message);
        Assert.Contains("Service", error.Message);
    }

    [Fact]
    public void Run_ControllerWithAnUnregisteredDependency_Throws()
    {
        var builder = CreateBuilder();
        builder.Services.Clear();
        builder.Services.AddSingleton<IGreeter, Greeter>(); // UnitOfWork missing
        using var app = builder.Build();

        var error = Assert.Throws<InvalidOperationException>(app.Run);

        Assert.Contains(nameof(UnitOfWork), error.Message);
    }

    [Fact]
    public void Build_ExposesTheProvider_WithTheFrameworkServices()
    {
        var builder = CreateBuilder();
        var renderer = builder.Renderer;
        using var app = builder.Build();

        Assert.Same(renderer, app.Services.GetRequiredService<IMarkupRenderer>());
        Assert.Same(ConsoleTextInput.Instance, app.Services.GetRequiredService<ITextInput>());
        Assert.IsType<Greeter>(app.Services.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void Build_UsesARendererRegisteredAsAService_WhenBuilderRendererIsNotSet()
    {
        var builder = CreateBuilder();
        builder.Renderer = null;
        var renderer = new NullRenderer();
        builder.Services.AddSingleton<IMarkupRenderer>(renderer);
        using var app = builder.Build();

        Assert.Same(renderer, app.Services.GetRequiredService<IMarkupRenderer>());
    }

    [Fact]
    public void Build_BuilderRendererWinsOverARegisteredService()
    {
        var builder = CreateBuilder();
        builder.Services.AddSingleton<IMarkupRenderer>(new NullRenderer());
        var configured = builder.Renderer;
        using var app = builder.Build();

        Assert.Same(configured, app.Services.GetRequiredService<IMarkupRenderer>());
    }

    [Fact]
    public void Build_DoesNotModifyTheBuilderServices()
    {
        var builder = CreateBuilder();
        var before = builder.Services.Count;

        using var first = builder.Build();
        using var second = builder.Build();

        Assert.Equal(before, builder.Services.Count);
    }

    [Fact]
    public void Build_ValidatesScopes()
    {
        var builder = CreateBuilder();
        using var app = builder.Build();

        // Resolving a scoped service from the root provider would create an application-lifetime instance
        Assert.Throws<InvalidOperationException>(() => app.Services.GetRequiredService<UnitOfWork>());
    }

    [Fact]
    public void Dispose_DisposesSingletonServices()
    {
        var builder = CreateBuilder();
        var unit = new UnitOfWork();
        builder.Services.AddSingleton(_ => new DisposableHolder(unit));
        var app = builder.Build();
        app.Services.GetRequiredService<DisposableHolder>();

        app.Dispose();

        Assert.True(unit.Disposed);
    }

    /// <summary>A singleton the container owns (registered by factory so the container disposes it).</summary>
    private sealed class DisposableHolder(UnitOfWork unit) : IDisposable
    {
        public void Dispose() => unit.Dispose();
    }

    [Fact]
    public void Router_ResolveController_UsesConstructorInjection()
    {
        var router = new Router();
        router.DiscoverAll(typeof(DependencyInjectionTests).Assembly);
        var services = new ServiceCollection()
            .AddSingleton<IGreeter, Greeter>()
            .AddTransient<UnitOfWork>()
            .BuildServiceProvider();

        var controller = router.ResolveController("Greeting", services);

        Assert.IsType<DiApp.Controllers.GreetingController>(controller);
    }

    [Fact]
    public void Router_ResolveView_SetsInjectedProperties()
    {
        var router = new Router();
        router.DiscoverAll(typeof(DependencyInjectionTests).Assembly);
        var unit = new UnitOfWork();
        var services = new ServiceCollection()
            .AddSingleton<IGreeter, Greeter>()
            .AddSingleton(unit)
            .BuildServiceProvider();

        var view = (DiApp.Views.Greeting.IndexView)router.ResolveView("Greeting", "Index", services);

        Assert.IsType<Greeter>(view.Greeter);
        Assert.Same(unit, view.Unit);
    }
}
