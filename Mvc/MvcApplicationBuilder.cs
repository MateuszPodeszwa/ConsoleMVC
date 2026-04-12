namespace ConsoleMVC.Mvc;

/// <summary>
/// Builder for configuring and creating an MvcApplication instance.
/// Mirrors ASP.NET Core's WebApplicationBuilder pattern.
/// </summary>
public class MvcApplicationBuilder
{
    public string DefaultController { get; set; } = "Home";
    public string DefaultAction { get; set; } = "Index";

    public MvcApplication Build()
    {
        return new MvcApplication(this);
    }
}
