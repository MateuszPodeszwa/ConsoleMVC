using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.HostingApp;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for <see cref="MvcApplicationBuilder.Configuration"/>, <see cref="MvcApplicationBuilder.Environment"/> and
/// <see cref="MvcApplicationBuilder.Logging"/> (docs/design/hosting-integration.md).
/// </summary>
[Collection(TestAppCollection.Name)]
public sealed class HostingIntegrationTests : IDisposable
{
    /// <summary>A renderer that does nothing.</summary>
    private sealed class NullRenderer : IMarkupRenderer
    {
        public int Width => 80;
        public void Render(Element element) { }
        public void WritePrompt(string text, TextStyle style) { }
        public void Clear() { }
    }

    private readonly string _contentRoot = Directory.CreateTempSubdirectory("consolemvc-hosting-").FullName;

    public HostingIntegrationTests()
    {
        HostingScript.Reset();
        File.WriteAllText(Path.Combine(_contentRoot, "appsettings.json"), """
            {
              "Shop": { "Name": "Json shop", "PageSize": 10 },
              "Logging": { "LogLevel": { "Default": "Information" } }
            }
            """);
        File.WriteAllText(Path.Combine(_contentRoot, "appsettings.Development.json"), """
            { "Shop": { "PageSize": 25 }, "Logging": { "LogLevel": { "Default": "Debug" } } }
            """);
    }

    public void Dispose() => Directory.Delete(_contentRoot, recursive: true);

    private MvcApplicationBuilder CreateBuilder(string environment = "Production", params string[] args)
    {
        var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions
        {
            Args = args, EnvironmentName = environment, ContentRootPath = _contentRoot
        });
        builder.ApplicationAssembly = typeof(HostingIntegrationTests).Assembly;
        builder.DefaultController = "Settings";
        builder.Renderer = new NullRenderer();
        builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop"));
        return builder;
    }

    // ------------------------------------------------------------------ configuration

    [Fact]
    public void Configuration_ReadsAppSettingsJson()
    {
        var builder = CreateBuilder();

        Assert.Equal("Json shop", builder.Configuration["Shop:Name"]);
        Assert.Equal(10, builder.Configuration.GetValue<int>("Shop:PageSize"));
    }

    [Fact]
    public void Configuration_ReadsTheEnvironmentsFile_OverTheBaseOne()
    {
        var builder = CreateBuilder("Development");

        Assert.Equal("Development", builder.Environment.EnvironmentName);
        Assert.True(builder.Environment.IsDevelopment());
        Assert.Equal(25, builder.Configuration.GetValue<int>("Shop:PageSize"));
        Assert.Equal("Json shop", builder.Configuration["Shop:Name"]);
    }

    [Fact]
    public void Configuration_CommandLineWinsOverFiles()
    {
        var builder = CreateBuilder("Production", "--Shop:Name=Args shop");

        Assert.Equal("Args shop", builder.Configuration["Shop:Name"]);
    }

    [Fact]
    public void Configuration_ReadsEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("CONSOLEMVC_HOSTING_TEST__VALUE", "from env");
        try
        {
            Assert.Equal("from env", CreateBuilder().Configuration["CONSOLEMVC_HOSTING_TEST:VALUE"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONSOLEMVC_HOSTING_TEST__VALUE", null);
        }
    }

    [Fact]
    public void Environment_DefaultsToProduction_AndContentRootToTheAppDirectory()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var builder = MvcApplication.CreateBuilder([]);

        Assert.Equal(string.IsNullOrEmpty(environment) ? "Production" : environment, builder.Environment.EnvironmentName);
        Assert.Equal(AppContext.BaseDirectory, builder.Environment.ContentRootPath);
    }

    [Fact]
    public void Run_InjectsConfigurationOptionsAndLoggers()
    {
        var builder = CreateBuilder();
        var logs = new ListLoggerProvider();
        builder.Logging.AddProvider(logs);
        using var app = builder.Build();

        app.Run();

        Assert.Equal(["config:Json shop", "options:Json shop/10", "view:Json shop"], HostingScript.Log);
        Assert.Contains("Information ConsoleMVC.Tests.Mvc.HostingApp.Controllers.SettingsController: Showing settings for Json shop",
            logs.Messages);
        Assert.Same(builder.Configuration, app.Configuration);
        Assert.Same(app.Configuration, app.Services.GetRequiredService<IConfiguration>());
    }

    // ------------------------------------------------------------------ logging

    [Fact]
    public void Logging_HasNoProvidersByDefault_SoNothingIsWrittenOverTheScreen()
    {
        using var app = CreateBuilder().Build();

        Assert.Empty(app.Services.GetServices<ILoggerProvider>());
        Assert.NotNull(app.Services.GetRequiredService<ILogger<HostingIntegrationTests>>());
    }

    [Fact]
    public void Logging_FollowsTheLoggingSection_AndTheFrameworkLogsNavigation()
    {
        var logs = new ListLoggerProvider();

        var production = CreateBuilder();
        production.Logging.AddProvider(logs);
        using (var app = production.Build())
            app.Run();

        Assert.DoesNotContain(logs.Messages, m => m.StartsWith("Debug ConsoleMVC", StringComparison.Ordinal));

        var development = CreateBuilder("Development");
        development.Logging.AddProvider(logs);
        using (var app = development.Build())
            app.Run();

        Assert.Contains("Debug ConsoleMVC.Mvc.MvcApplication: Running Settings/Index", logs.Messages);
    }

    [Fact]
    public void Logging_RecordsFailures_AndTheExceptionStillPropagates()
    {
        var builder = CreateBuilder();
        builder.DefaultAction = "Fail";
        var logs = new ListLoggerProvider();
        builder.Logging.AddProvider(logs);
        using var app = builder.Build();

        var error = Assert.Throws<InvalidOperationException>(app.Run);

        Assert.Equal("The shop is closed.", error.Message);
        Assert.Contains("Error ConsoleMVC.Mvc.MvcApplication: Unhandled exception on Settings/Fail", logs.Messages);
    }

    [Fact]
    public void Options_ValidateOnStart_FailsTheBuild()
    {
        var builder = CreateBuilder();
        builder.Services.AddOptions<ShopOptions>().Validate(o => o.PageSize > 50, "PageSize must be over 50.").ValidateOnStart();

        var error = Assert.Throws<OptionsValidationException>(() => builder.Build().Run());

        Assert.Contains("PageSize must be over 50.", error.Message);
    }
}
