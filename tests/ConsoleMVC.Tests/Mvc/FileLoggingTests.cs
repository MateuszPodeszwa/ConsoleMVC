using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.HostingApp;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for <see cref="FileLoggerProvider"/> and <c>builder.Logging.AddFile</c> (docs/design/file-logging.md).</summary>
[Collection(TestAppCollection.Name)]
public sealed class FileLoggingTests : IDisposable
{
    /// <summary>A renderer that does nothing.</summary>
    private sealed class NullRenderer : IMarkupRenderer
    {
        public int Width => 80;
        public void Render(Element element) { }
        public void WritePrompt(string text, TextStyle style) { }
        public void Clear() { }
    }

    /// <summary>A clock fixed at 27 October 2026, 21:04:05.123 UTC.</summary>
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 21, 4, 5, 123, TimeSpan.Zero);
    }

    private readonly string _folder = Directory.CreateTempSubdirectory("consolemvc-logs-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private FileLoggerProvider Provider(long maxSize = 0, int retained = 5) =>
        new(new FileLoggerOptions { Path = "logs/test.log", MaxFileSizeBytes = maxSize, RetainedFileCount = retained }, _folder, new FixedClock());

    private string Log(string name = "test.log") => File.ReadAllText(Path.Combine(_folder, "logs", name));

    [Fact]
    public void Entries_HaveTimeLevelCategoryAndMessage()
    {
        using (var provider = Provider())
        {
            var logger = provider.CreateLogger("Shop.Orders");
            logger.LogInformation("Order {Id} saved", 42);
            logger.LogWarning("Stock is low");
        }

        Assert.Equal("2026-10-27T21:04:05.123Z INFO  Shop.Orders: Order 42 saved\n2026-10-27T21:04:05.123Z WARN  Shop.Orders: Stock is low\n", Log());
    }

    [Fact]
    public void Exceptions_AndLineBreaks_AreIndented_SoTheyCannotForgeEntries()
    {
        using (var provider = Provider())
        {
            provider.CreateLogger("App").LogError(new InvalidOperationException("boom"),
                "Bad input: {Input}", "x\n2026-01-01T00:00:00.000Z INFO  App: forged\u001b[2J");
        }

        var lines = Log().TrimEnd('\n').Split('\n');
        Assert.StartsWith("2026-10-27T21:04:05.123Z ERROR App: Bad input: x", lines[0]);
        Assert.Equal("    2026-01-01T00:00:00.000Z INFO  App: forged[2J", lines[1]); // indented, escape removed
        Assert.StartsWith("    System.InvalidOperationException: boom", lines[2]);
        Assert.All(lines.Skip(1), line => Assert.StartsWith("    ", line));
    }

    [Fact]
    public void RollsOver_BySize_AndKeepsTheRetainedFiles()
    {
        using (var provider = Provider(maxSize: 60, retained: 2))
        {
            var logger = provider.CreateLogger("C");
            for (var i = 1; i <= 4; i++)
                logger.LogInformation("Entry number {I} with some padding", i);
        }

        var files = Directory.GetFiles(Path.Combine(_folder, "logs")).Select(f => Path.GetFileName(f)).Order().ToArray();
        Assert.Equal(["test.1.log", "test.2.log"], files); // the current file rolled after entry 4; entry 1 was removed
        Assert.Contains("Entry number 4", Log("test.1.log"));
        Assert.Contains("Entry number 3", Log("test.2.log"));
    }

    [Fact]
    public void InvalidOptions_Throw()
    {
        Assert.Throws<ArgumentException>(() => new FileLoggerProvider(new FileLoggerOptions { Path = " " }));
        Assert.Throws<ArgumentException>(() => new FileLoggerProvider(new FileLoggerOptions { RetainedFileCount = -1 }));
    }

    [Fact]
    public void AfterDispose_NothingIsLogged()
    {
        var provider = Provider();
        var logger = provider.CreateLogger("C");
        provider.Dispose();

        Assert.False(logger.IsEnabled(LogLevel.Critical));
        logger.LogCritical("ignored");
        provider.Dispose(); // idempotent
    }

    // ------------------------------------------------------------------ builder

    private MvcApplicationBuilder Builder(string appsettings)
    {
        File.WriteAllText(Path.Combine(_folder, "appsettings.json"), appsettings);
        var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions { ContentRootPath = _folder, EnvironmentName = "Production" });
        builder.ApplicationAssembly = typeof(FileLoggingTests).Assembly;
        builder.DefaultController = "Settings";
        builder.Renderer = new NullRenderer();
        builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop"));
        return builder;
    }

    [Fact]
    public void AddFile_WritesTheFrameworkAndApplicationLogs_UnderTheContentRoot()
    {
        HostingScript.Reset();
        var builder = Builder("""{ "Shop": { "Name": "Corner" } }""");
        builder.Logging.AddFile("logs/app.log");

        using (var app = builder.Build())
            app.Run();

        var log = Log("app.log");
        Assert.Contains("INFO  ConsoleMVC.Mvc.MvcApplication: Starting at Settings/Index (Production)", log);
        Assert.Contains("INFO  ConsoleMVC.Tests.Mvc.HostingApp.Controllers.SettingsController: Showing settings for Corner", log);
        Assert.Contains("ConsoleMVC.Mvc.MvcApplication: Stopped", log);
    }

    [Fact]
    public void AddFile_ReadsTheLoggingFileSection()
    {
        HostingScript.Reset();
        var builder = Builder("""
            { "Logging": { "File": { "Path": "logs/configured.log", "LogLevel": { "Default": "Debug" } } } }
            """);
        builder.Logging.AddFile();

        using (var app = builder.Build())
            app.Run();

        Assert.Contains("DEBUG ConsoleMVC.Mvc.MvcApplication: Running Settings/Index", Log("configured.log"));
    }

    [Fact]
    public void AddFile_Twice_AddsOneProvider()
    {
        var builder = Builder("{}");
        builder.Logging.AddFile().AddFile(o => o.RetainedFileCount = 1);

        using var app = builder.Build();

        Assert.Single(app.Services.GetServices<ILoggerProvider>());
    }
}
