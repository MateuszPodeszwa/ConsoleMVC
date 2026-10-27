using ConsoleMVC.Mvc;

// Controllers, views and middleware for the async action and middleware tests (docs/design/async-and-middleware.md).

namespace ConsoleMVC.Tests.Mvc.AsyncApp
{
    /// <summary>Everything the async tests observe during a run.</summary>
    public static class AsyncScript
    {
        /// <summary>Ordered log of middleware, actions and views.</summary>
        public static List<string> Log { get; } = [];

        /// <summary>The token the last action received.</summary>
        public static CancellationToken Token { get; set; }

        public static void Reset()
        {
            Log.Clear();
            Token = default;
        }
    }

    /// <summary>A scoped service used by <see cref="TaggingMiddleware"/>.</summary>
    public sealed class RequestTag
    {
        private static int _counter;

        public int Id { get; } = Interlocked.Increment(ref _counter);
    }

    /// <summary>A class-based middleware with a scoped dependency.</summary>
    public sealed class TaggingMiddleware(RequestTag tag) : IActionMiddleware
    {
        public async Task InvokeAsync(ActionContext context, ActionDelegate next)
        {
            AsyncScript.Log.Add($"tag:{tag.Id > 0}");
            context.Items["tag"] = tag;
            await next(context);
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.AsyncApp.Controllers
{
    using ConsoleMVC.Tests.Mvc.AsyncApp;

    public sealed class AsyncController : Controller
    {
        public async Task<ActionResult> Index()
        {
            await Task.Yield();
            AsyncScript.Log.Add("action:Index");
            return View();
        }

        public async ValueTask<ViewResult> Value()
        {
            await Task.Delay(1);
            AsyncScript.Log.Add("action:Value");
            return View("Index");
        }

        public Task<ActionResult> Cancellable(CancellationToken cancellationToken)
        {
            AsyncScript.Token = cancellationToken;
            return Task.FromResult<ActionResult>(View("Index"));
        }

        public async Task<ActionResult> Fail()
        {
            await Task.Yield();
            throw new InvalidDataException("broken");
        }

        public ActionResult Error()
        {
            AsyncScript.Log.Add("action:Error");
            return View("Index");
        }

        public async Task<ActionResult> Invalid([System.ComponentModel.DataAnnotations.Range(1, 5)] int stars)
        {
            await Task.Yield();
            return View("Index");
        }

        /// <summary>Not an action: there is no result to render.</summary>
        public Task Background() => Task.CompletedTask;
    }
}

namespace ConsoleMVC.Tests.Mvc.AsyncApp.Views.Async
{
    using ConsoleMVC.Tests.Mvc.AsyncApp;

    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            AsyncScript.Log.Add("view:Index");
            return NavigationResult.Quit();
        }
    }
}
