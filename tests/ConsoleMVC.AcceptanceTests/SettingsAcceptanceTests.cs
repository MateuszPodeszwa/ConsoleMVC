using ConsoleMVC.Testing;

namespace Acceptance;

/// <summary>The <c>ConsoleMVC:</c> settings, as docs/guides/docs/configuration.md lists them.</summary>
public class SettingsAcceptanceTests
{
    private static MvcTestHost WithSetting(string key, string value) =>
        MvcTestHost.For<Controllers.HomeController>(builder => builder.Configuration[key] = value);

    [Theory]
    [InlineData("ConsoleMVC:ScreenReader", "yes")]
    [InlineData("ConsoleMVC:Mouse", "1")]
    [InlineData("ConsoleMVC:HighContrast", "on")]
    [InlineData("ConsoleMVC:FullScreen", "enabled")]
    public void AnOnOffSettingThatIsNotTrueOrFalse_StopsTheApplication(string key, string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => WithSetting(key, value).Run("q"));

        Assert.Contains(key, error.Message);
        Assert.Contains($"'{value}'", error.Message);
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("true")]
    [InlineData(" True ")]
    public void TrueInAnyCasing_TurnsTheFeatureOn(string value)
    {
        var run = WithSetting("ConsoleMVC:ScreenReader", value).Run("q");

        Assert.Contains("You are here: Home", run.LastScreen);   // breadcrumbs as a screen reader hears them
    }

    [Fact]
    public void False_LeavesTheFeatureOff()
    {
        var run = WithSetting("ConsoleMVC:ScreenReader", "false").Run("q");

        Assert.DoesNotContain("You are here", run.LastScreen);
    }
}
