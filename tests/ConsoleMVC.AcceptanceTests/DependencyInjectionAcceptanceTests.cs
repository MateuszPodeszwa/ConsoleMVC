namespace Acceptance;

/// <summary>Dependency injection into views, as docs/guides/docs/dependency-injection.md describes it.</summary>
public class DependencyInjectionAcceptanceTests
{
    [Fact]
    public void AMissingService_IsNamedAsItIsWrittenInCSharp()
    {
        var error = Assert.Throws<InvalidOperationException>(() => App.Host().Run("6", "q"));

        Assert.Contains("injects 'IList<String> Names'", error.Message);
        Assert.Contains("builder.Services.AddSingleton<IList<String>, ...>();", error.Message);
        Assert.DoesNotContain("`1", error.Message);
    }
}
