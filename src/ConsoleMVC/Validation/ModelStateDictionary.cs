using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The validation errors for a request, keyed by form field name (case-insensitive), mirroring ASP.NET Core's
/// <c>ModelStateDictionary</c>. The framework fills it before an action runs: values that could not be converted
/// and failed DataAnnotations attributes such as <c>[Required]</c> or <c>[Range]</c>.
/// </summary>
/// <remarks>
/// The same instance is passed to the view returned by the action, so the view can show the errors with
/// <c>&lt;errors /&gt;</c> or read them from <see cref="ConsoleViewBase.ModelState"/>.
/// </remarks>
/// <example>
/// <code>
/// public ActionResult Save(SignupModel model)
/// {
///     if (!ModelState.IsValid)
///         return View("Index", model);
///
///     if (users.Exists(model.Name))
///     {
///         ModelState.AddModelError("Name", "That name is taken.");
///         return View("Index", model);
///     }
///
///     return RedirectToAction("Done");
/// }
/// </code>
/// </example>
public sealed class ModelStateDictionary : IReadOnlyDictionary<string, IReadOnlyList<string>>
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _bindingErrors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether there are no errors.</summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>Gets the total number of error messages across all fields.</summary>
    public int ErrorCount => _errors.Values.Sum(list => list.Count);

    /// <summary>Gets the number of fields that have errors.</summary>
    public int Count => _errors.Count;

    /// <summary>Gets the names of the fields that have errors.</summary>
    public IEnumerable<string> Keys => _errors.Keys;

    /// <summary>Gets the error lists, one per field with errors.</summary>
    public IEnumerable<IReadOnlyList<string>> Values => _errors.Values;

    /// <summary>
    /// Gets the errors for a field.
    /// </summary>
    /// <param name="key">The field name (case-insensitive).</param>
    /// <exception cref="KeyNotFoundException">Thrown when the field has no errors; use <see cref="ErrorsFor"/> to avoid this.</exception>
    public IReadOnlyList<string> this[string key] => _errors[key];

    /// <summary>
    /// Records an error for a field. Use an empty key for errors that do not belong to a single field.
    /// </summary>
    /// <param name="key">The field name, e.g. <c>"Email"</c>, or <c>""</c> for a general error.</param>
    /// <param name="message">The message shown to the user.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public void AddModelError(string key, string message)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(message);

        if (!_errors.TryGetValue(key, out var list))
            _errors[key] = list = [];

        list.Add(message);
    }

    /// <summary>
    /// Returns the errors for a field, or an empty list when it has none.
    /// </summary>
    /// <param name="key">The field name (case-insensitive).</param>
    /// <returns>The error messages in the order they were added.</returns>
    public IReadOnlyList<string> ErrorsFor(string key) =>
        _errors.TryGetValue(key, out var list) ? list : [];

    /// <summary>Removes all errors.</summary>
    public void Clear()
    {
        _errors.Clear();
        _bindingErrors.Clear();
    }

    /// <summary>
    /// Records that a posted value for <paramref name="key"/> could not be converted. Such fields are not
    /// validated again, so the user sees one clear message per field.
    /// </summary>
    internal void AddBindingError(string key, string message)
    {
        AddModelError(key, message);
        _bindingErrors.Add(key);
    }

    /// <summary>Returns whether a conversion error was recorded for <paramref name="key"/>.</summary>
    internal bool HasBindingError(string key) => _bindingErrors.Contains(key);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _errors.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out IReadOnlyList<string> value)
    {
        var found = _errors.TryGetValue(key, out var list);
        value = list;
        return found;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator() =>
        _errors.Select(pair => new KeyValuePair<string, IReadOnlyList<string>>(pair.Key, pair.Value)).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
