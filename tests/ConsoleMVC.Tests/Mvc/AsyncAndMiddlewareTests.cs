using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.AsyncApp;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for asynchronous actions, <see cref="MvcApplication.RunAsync"/> and the action middleware pipeline
/// (docs/design/async-and-middleware.md).
/// </summary>
[Collection(TestAppCollection.Name)]
public class AsyncAndMiddlewareTests
{
    /// <summary>A renderer that does nothing.</summary>
    private sealed class NullRenderer : IMarkupRenderer
    {
        public int Width => 80;
        public void Render(Element element) { }
        public void WritePrompt(string text, TextStyle style) { }
        public void Clear() { }
    }

    private static MvcApplication CreateApp(string action = "Index", Action<IServiceCollection>? services = null)
    {
        AsyncScript.Reset();
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(AsyncAndMiddlewareTests).Assembly;
        builder.DefaultController = "Async";
        builder.DefaultAction = action;
        builder.Renderer = new NullRenderer();
        services?.Invoke(builder.Services);
        return builder.Build();
    }

    // --- async actions ---

    [Fact]
    public async Task RunAsync_AwaitsTaskActions()
    {
        using var app = CreateApp();

        await app.RunAsync();

        Assert.Equal(["action:Index", "view:Index"], AsyncScript.Log);
    }

    [Fact]
    public async Task RunAsync_AwaitsValueTaskActionsOfADerivedResult()
    {
        using var app = CreateApp("Value");

        await app.RunAsync();

        Assert.Equal(["action:Value", "view:Index"], AsyncScript.Log);
    }

    [Fact]
    public void Run_StillWorksWithAsyncActions()
    {
        using var app = CreateApp();

        app.Run();

        Assert.Equal(["action:Index", "view:Index"], AsyncScript.Log);
    }

    [Fact]
    public async Task RunAsync_PassesItsTokenToActions()
    {
        using var app = CreateApp("Cancellable");
        using var cancellation = new CancellationTokenSource();

        await app.RunAsync(cancellation.Token);

        Assert.True(AsyncScript.Token.CanBeCanceled);
        Assert.False(AsyncScript.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task TheActionsToken_IsCancelledWithTheCallers()
    {
        using var app = CreateApp("CancelDuring");
        using var cancellation = new CancellationTokenSource();
        AsyncScript.Caller = cancellation;

        try
        {
            await app.RunAsync(cancellation.Token);   // the view quits, or the next cycle sees the cancellation
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Contains("cancelled:True", AsyncScript.Log);
    }

    [Fact]
    public async Task RunAsync_StopsWhenCancelled()
    {
        using var app = CreateApp();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.RunAsync(new CancellationToken(canceled: true)));
        Assert.Empty(AsyncScript.Log);
    }

    [Fact]
    public async Task RunAsync_SurfacesTheActionsOwnException()
    {
        using var app = CreateApp("Fail");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => app.RunAsync());
        Assert.Equal("broken", error.Message);
    }

    [Fact]
    public void Router_TreatsTaskOfActionResultAsAnAction_ButNotPlainTask()
    {
        var router = new Router();
        router.DiscoverAll(typeof(AsyncAndMiddlewareTests).Assembly);
        var type = typeof(AsyncApp.Controllers.AsyncController);

        Assert.Equal("Value", router.ResolveAction(type, "value").Name);
        Assert.Throws<InvalidOperationException>(() => router.ResolveAction(type, "Background"));
    }

    // --- middleware ---

    [Fact]
    public async Task Middleware_RunsInRegistrationOrder_AroundTheAction()
    {
        using var app = CreateApp();
        app.Use(async (context, next) =>
        {
            AsyncScript.Log.Add("m1:before");
            await next(context);
            AsyncScript.Log.Add("m1:after");
        });
        app.Use(async (context, next) =>
        {
            AsyncScript.Log.Add($"m2:before {context.Route.Controller}/{context.Route.Action}");
            await next(context);
            AsyncScript.Log.Add($"m2:after {context.Result?.GetType().Name} {context.Controller?.GetType().Name}");
        });

        await app.RunAsync();

        Assert.Equal(
            ["m1:before", "m2:before Async/Index", "action:Index", "m2:after ViewResult AsyncController", "m1:after", "view:Index"],
            AsyncScript.Log);
    }

    [Fact]
    public async Task Middleware_CanShortCircuit_WithARedirect()
    {
        using var app = CreateApp("Fail");
        app.Use((context, next) =>
        {
            if (context.Route.Action == "Fail")
            {
                context.RedirectTo("Error");
                return Task.CompletedTask; // the Fail action never runs
            }

            return next(context);
        });

        await app.RunAsync();

        Assert.Equal(["action:Error", "view:Index"], AsyncScript.Log);
    }

    [Fact]
    public async Task Middleware_CanHandleExceptions()
    {
        using var app = CreateApp("Fail");
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (InvalidDataException e)
            {
                AsyncScript.Log.Add("caught:" + e.Message);
                context.RedirectTo("Error", "Async");
            }
        });

        await app.RunAsync();

        Assert.Equal(["caught:broken", "action:Error", "view:Index"], AsyncScript.Log);
    }

    [Fact]
    public async Task Middleware_SeesModelStateAfterTheAction()
    {
        AsyncScript.Reset();
        using var app = CreateApp();
        var invalid = false;
        app.Use(async (context, next) =>
        {
            if (context.Route.Action == "Index")
            {
                context.RedirectTo("Invalid");
                return;
            }

            context.Route = new RouteContext
            {
                Controller = context.Route.Controller,
                Action = context.Route.Action,
                FormData = new() { ["stars"] = "9" }
            };
            await next(context);
            invalid = !context.ModelState.IsValid;
        });

        await app.RunAsync();

        Assert.True(invalid);
    }

    [Fact]
    public async Task UseMiddleware_CreatesTheMiddlewarePerCycle_WithScopedServices()
    {
        using var app = CreateApp(services: s => s.AddScoped<RequestTag>());
        RequestTag? seen = null;
        app.UseMiddleware<TaggingMiddleware>();
        app.Use(async (context, next) =>
        {
            seen = (RequestTag)context.Items["tag"]!;
            await next(context);
        });

        await app.RunAsync();

        Assert.Equal("tag:True", AsyncScript.Log[0]);
        Assert.NotNull(seen);
    }

    [Fact]
    public async Task Middleware_ThatProducesNoResult_FailsWithAClearMessage()
    {
        using var app = CreateApp();
        app.Use((_, _) => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => app.RunAsync());

        Assert.Contains("middleware", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Use_AfterRunHasStarted_Throws()
    {
        using var app = CreateApp();
        app.Use((context, next) =>
        {
            Assert.Throws<InvalidOperationException>(() => app.Use((c, n) => n(c)));
            return next(context);
        });

        app.Run();
    }
}
