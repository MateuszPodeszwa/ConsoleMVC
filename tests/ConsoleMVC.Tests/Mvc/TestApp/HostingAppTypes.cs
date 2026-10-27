using ConsoleMVC.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Controllers, views and options for the hosting integration tests (docs/design/hosting-integration.md).

namespace ConsoleMVC.Tests.Mvc.HostingApp
{
    /// <summary>Options bound from the "Shop" configuration section.</summary>
    public sealed class ShopOptions
    {
        public string Name { get; set; } = "";
        public int PageSize { get; set; }
    }

    /// <summary>Everything the hosting tests observe during a run.</summary>
    public static class HostingScript
    {
        /// <summary>Ordered log of what the controller and view saw.</summary>
        public static List<string> Log { get; } = [];

        public static void Reset() => Log.Clear();
    }

    /// <summary>A logger provider that keeps every message, for assertions.</summary>
    public sealed class ListLoggerProvider : ILoggerProvider
    {
        /// <summary>Gets the messages logged, as "Level Category: message".</summary>
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new ListLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class ListLogger(ListLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (provider.Messages)
                    provider.Messages.Add($"{logLevel} {category}: {formatter(state, exception)}");
            }
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.HostingApp.Controllers
{
    using ConsoleMVC.Tests.Mvc.HostingApp;

    /// <summary>A controller that receives configuration, options and a logger through its constructor.</summary>
    public sealed class SettingsController(IConfiguration configuration, IOptions<ShopOptions> options,
        ILogger<SettingsController> logger) : Controller
    {
        public ActionResult Index()
        {
            logger.LogInformation("Showing settings for {Shop}", options.Value.Name);
            HostingScript.Log.Add($"config:{configuration["Shop:Name"]}");
            HostingScript.Log.Add($"options:{options.Value.Name}/{options.Value.PageSize}");
            return View();
        }

        public ActionResult Fail() => throw new InvalidOperationException("The shop is closed.");
    }
}

namespace ConsoleMVC.Tests.Mvc.HostingApp.Views.Settings
{
    using ConsoleMVC.Tests.Mvc.HostingApp;

    /// <summary>A view injecting configuration the way <c>@inject IConfiguration Config</c> generates it.</summary>
    public sealed class IndexView : ConsoleView<object>
    {
        [Inject]
        public IConfiguration Config { get; private set; } = default!;

        public override NavigationResult Render(object Model)
        {
            HostingScript.Log.Add($"view:{Config["Shop:Name"]}");
            return NavigationResult.Quit();
        }
    }
}
