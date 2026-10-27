using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.ReviewApp;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Regression tests for the runtime bugs found in the 1.9 review.
/// </summary>
[Collection(TestAppCollection.Name)]
public class RuntimeReviewRegressionTests
{
    private sealed class NullRenderer : IMarkupRenderer
    {
        public int Width => 80;
        public void Render(Element element) { }
        public void WritePrompt(string text, TextStyle style) { }
        public void Clear() { }
    }

    private static MvcApplication CreateApp(string action = "Index")
    {
        ReviewScript.Reset();
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(RuntimeReviewRegressionTests).Assembly;
        builder.DefaultController = "Review";
        builder.DefaultAction = action;
        builder.Renderer = new NullRenderer();
        builder.Services.AddSingleton<AsyncOnlyService>();
        return builder.Build();
    }

    [Fact]
    public void Dispose_HandlesServicesThatAreOnlyAsyncDisposable()
    {
        var app = CreateApp();
        app.Run();

        app.Dispose();

        Assert.Contains("service:disposeAsync", ReviewScript.Log);
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheServices()
    {
        var app = CreateApp();
        await app.RunAsync();

        await app.DisposeAsync();

        Assert.Contains("service:disposeAsync", ReviewScript.Log);
    }

    [Fact]
    public void ControllersThatAreOnlyAsyncDisposable_AreDisposed()
    {
        using var app = CreateApp();

        app.Run();

        Assert.Contains("controller:disposeAsync#1", ReviewScript.Log);
    }

    [Fact]
    public void ArrayParameters_BindNull_InsteadOfThrowing()
    {
        using var app = CreateApp("Tags");

        app.Run();

        Assert.Contains("tags:null", ReviewScript.Log);
    }

    [Fact]
    public async Task RunAsync_CanRunAgain_AfterFinishing_OrBeingCancelledBeforeStarting()
    {
        using var app = CreateApp();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.RunAsync(new CancellationToken(canceled: true)));
        await app.RunAsync();
        await app.RunAsync();
    }

    [Fact]
    public async Task RunAsync_RefusesToRunTwiceAtTheSameTime()
    {
        using var app = CreateApp();
        var gate = new TaskCompletionSource();
        app.Use(async (context, next) =>
        {
            await gate.Task;
            await next(context);
        });

        var first = app.RunAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.RunAsync());
        gate.SetResult();
        await first;
    }

    [Fact]
    public void UnregisteredClassMiddleware_IsDisposedAfterEachCycle()
    {
        using var app = CreateApp();
        app.UseMiddleware<DisposableMiddleware>();

        app.Run();

        Assert.Contains("middleware:dispose", ReviewScript.Log);
    }

    [Fact]
    public void RetryingMiddleware_DisposesEveryControllerItCreated()
    {
        using var app = CreateApp("Flaky");
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (IOException)
            {
                await next(context);
            }
        });

        app.Run();

        Assert.Contains("controller:disposeAsync#1", ReviewScript.Log);
        Assert.Contains("controller:disposeAsync#2", ReviewScript.Log);
    }

    [Fact]
    public void ThemeEmpty_IsNotSharedBetweenCallers()
    {
        Theme.Empty.Set("leak", new ThemeStyle { Bold = true });

        Assert.Empty(Theme.Empty.Names);
    }

    [Fact]
    public void HotReload_ClearsTheInjectedPropertyCache()
    {
        var view = new DiApp.Views.Greeting.IndexView();
        var services = new ServiceCollection()
            .AddSingleton<DiApp.IGreeter, DiApp.Greeter>()
            .AddSingleton(new DiApp.UnitOfWork())
            .BuildServiceProvider();
        ServiceInjector.InjectProperties(view, services);
        Assert.True(ServiceInjector.IsCached(view.GetType()));

        HotReloadHandler.ClearCache([view.GetType()]);

        Assert.False(ServiceInjector.IsCached(view.GetType()));
    }

    [Fact]
    public void ColumnsInsideAnAutoWidthBox_DoNotStackOnAWideTerminal()
    {
        static TextElement Text(string content)
        {
            var text = new TextElement();
            text.AppendText(content);
            return text;
        }

        var columns = new ColumnsElement { StackBelow = 60 };
        var menu = new ColumnElement();
        menu.Add(Text("Menu"));
        var details = new ColumnElement();
        details.Add(Text("Details"));
        columns.Add(menu);
        columns.Add(details);
        var box = new BoxElement { Title = "Panel" };
        box.Add(columns);

        var lines = box.Layout(100).Select(l => l.ToPlainText()).ToArray();

        Assert.Contains("│ Menu  Details │", lines);
        Assert.Equal(3, lines.Length);
        Assert.Equal(2, columns.Layout(20).Count); // still stacks on a narrow terminal
    }
}
