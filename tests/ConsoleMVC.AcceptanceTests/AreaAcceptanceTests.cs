namespace Acceptance;

/// <summary>Areas, as docs/guides/docs/areas-and-modules.md describes them.</summary>
public class AreaAcceptanceTests
{
    [Fact]
    public void InsideAnArea_AControllerNamedAlone_IsTheAreas()
    {
        var run = App.Host().Run("9", "a", "q");

        Assert.Equal(["Home/Index", "Admin/Home/Index", "Admin/Home/Index"], run.Routes);
    }

    [Fact]
    public void ALeadingSlash_NamesTheApplicationsController()
    {
        var run = App.Host().Run("9", "h", "q");

        Assert.Equal(["Home/Index", "Admin/Home/Index", "Home/Index"], run.Routes);
        Assert.Contains("Acceptance", run.LastScreen);
    }

    [Fact]
    public void ARouteTypedInAnyCase_ReadsAsDeclared()
    {
        var run = App.Host().Run("0", "q");

        Assert.Contains("Area: Admin, controller: Home", run.LastScreen);
    }
}
