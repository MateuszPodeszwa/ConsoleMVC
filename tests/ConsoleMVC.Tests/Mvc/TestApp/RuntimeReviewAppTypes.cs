using ConsoleMVC.Mvc;

// Types for RuntimeReviewRegressionTests: disposal, arrays and retries.

namespace ConsoleMVC.Tests.Mvc.ReviewApp
{
    /// <summary>Everything the tests observe.</summary>
    public static class ReviewScript
    {
        public static List<string> Log { get; } = [];

        public static int Attempts { get; set; }

        public static void Reset()
        {
            Log.Clear();
            Attempts = 0;
        }
    }

    /// <summary>A singleton that can only be disposed asynchronously.</summary>
    public sealed class AsyncOnlyService : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            ReviewScript.Log.Add("service:disposeAsync");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A class middleware that is not registered as a service.</summary>
    public sealed class DisposableMiddleware : IActionMiddleware, IDisposable
    {
        public Task InvokeAsync(ActionContext context, ActionDelegate next) => next(context);

        public void Dispose() => ReviewScript.Log.Add("middleware:dispose");
    }
}

namespace ConsoleMVC.Tests.Mvc.ReviewApp.Controllers
{
    using ConsoleMVC.Tests.Mvc.ReviewApp;

    public sealed class ReviewController(AsyncOnlyService service) : Controller, IAsyncDisposable
    {
        private readonly int _attempt = ++ReviewScript.Attempts;

        public ActionResult Index() => View();

        public ActionResult Tags(string[] tags)
        {
            ReviewScript.Log.Add($"tags:{(tags is null ? "null" : tags.Length.ToString())}");
            return View("Index");
        }

        public ActionResult Flaky()
        {
            if (_attempt == 1)
                throw new IOException("first attempt fails");

            return View("Index");
        }

        public ValueTask DisposeAsync()
        {
            ReviewScript.Log.Add($"controller:disposeAsync#{_attempt}{(service is null ? "" : "")}");
            return ValueTask.CompletedTask;
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.ReviewApp.Views.Review
{
    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => NavigationResult.Quit();
    }
}
