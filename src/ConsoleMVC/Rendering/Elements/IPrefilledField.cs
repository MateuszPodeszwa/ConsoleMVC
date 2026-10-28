namespace ConsoleMVC.Rendering;

/// <summary>
/// A built-in form field that can start from the answer the user posted before, so a form shown again after
/// validation failed keeps what was typed, as ASP.NET Core forms do. A masked field (a password) never does.
/// </summary>
/// <example><code>if (field is IPrefilledField prefilled) prefilled.Prefill("Ada");</code></example>
internal interface IPrefilledField
{
    /// <summary>
    /// Starts the field from <paramref name="posted"/>, the value it posted last time, when that is a value the field
    /// accepts; otherwise leaves the field as it is.
    /// </summary>
    /// <param name="posted">The posted value, as the field posts it (<c>"true"</c>, <c>"2026-10-27"</c>, <c>"red,blue"</c>).</param>
    /// <example><code>number.Prefill("42");</code></example>
    void Prefill(string posted);
}
