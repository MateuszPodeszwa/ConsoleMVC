namespace ConsoleMVC.Mvc;

/// <summary>
/// A key-value data bag for passing loose data from controllers to views,
/// mirroring ASP.NET Core's ViewDataDictionary.
/// </summary>
public class ViewDataDictionary : Dictionary<string, object?>
{
}
