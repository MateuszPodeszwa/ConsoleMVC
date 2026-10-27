namespace ConsoleMVC.Mvc;

/// <summary>
/// A key-value dictionary for passing loosely-typed supplementary data from controllers
/// to views, mirroring ASP.NET Core's <c>ViewDataDictionary</c>.
/// </summary>
/// <remarks>
/// <para>
/// Use this when you need to pass small amounts of data to a view without adding
/// properties to the view model. The dictionary is populated in the controller and
/// made available in the view via <see cref="ConsoleView.ViewData"/>.
/// </para>
/// <para>
/// For strongly-typed data, prefer using a dedicated view model class instead.
/// </para>
/// </remarks>
public class ViewDataDictionary : Dictionary<string, object?>
{
}
