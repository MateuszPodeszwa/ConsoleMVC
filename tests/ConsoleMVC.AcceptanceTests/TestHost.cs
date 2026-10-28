using Acceptance.Controllers;
using ConsoleMVC.Testing;

namespace Acceptance;

/// <summary>The acceptance application, as a test host. Example: <c>App.Host().Run("1", "Ada", "q")</c>.</summary>
internal static class App
{
    /// <summary>A fresh host for the application in this assembly.</summary>
    public static MvcTestHost Host() => MvcTestHost.For<HomeController>();
}
