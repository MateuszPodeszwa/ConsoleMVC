#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// A controller found in the compilation: its route name (the class name without <c>Controller</c>) and the names of
/// its actions. Value-equal, so the generator's cache survives edits that do not add, remove or rename actions.
/// </summary>
internal sealed class ControllerEntry : IEquatable<ControllerEntry>
{
    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <param name="name">The route name, e.g. <c>Orders</c> for <c>OrdersController</c>.</param>
    /// <param name="actions">The action names, in any order; duplicates (overloads) are removed.</param>
    public ControllerEntry(string name, IEnumerable<string> actions)
    {
        Name = name;
        Actions = actions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.Ordinal).ToImmutableArray();
    }

    /// <summary>Gets the route name.</summary>
    public string Name { get; }

    /// <summary>Gets the action names, sorted.</summary>
    public ImmutableArray<string> Actions { get; }

    /// <inheritdoc />
    public bool Equals(ControllerEntry? other) =>
        other is not null && Name == other.Name && Actions.SequenceEqual(other.Actions);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ControllerEntry);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name) ^ Actions.Length;
}

/// <summary>
/// The controllers and actions of the compilation, matched case-insensitively like the runtime router, so literal
/// <c>nav</c> targets can be checked at build time (CVW119).
/// </summary>
internal sealed class RouteCatalogue : IEquatable<RouteCatalogue>
{
    private readonly Dictionary<string, HashSet<string>> _controllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ImmutableArray<ControllerEntry> _entries;

    /// <summary>
    /// Creates a catalogue. Entries with the same name (the parts of a partial class) are merged.
    /// </summary>
    /// <param name="entries">The controllers found, in the compilation and in the ConsoleMVC modules it references.</param>
    /// <param name="isModule">
    /// Whether the compilation is a module: the application that adds it has controllers the module cannot see, so a
    /// controller it does not know is not reported.
    /// </param>
    public RouteCatalogue(IEnumerable<ControllerEntry> entries, bool isModule = false)
    {
        IsModule = isModule;
        var merged = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (!merged.TryGetValue(entry.Name, out var actions))
                merged[entry.Name] = actions = [];
            actions.AddRange(entry.Actions);
        }

        _entries = merged.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new ControllerEntry(p.Key, p.Value)).ToImmutableArray();
        foreach (var entry in _entries)
            _controllers[entry.Name] = new HashSet<string>(entry.Actions, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets whether the compilation is a module, whose application's controllers are not known.</summary>
    public bool IsModule { get; }

    /// <summary>Gets a value indicating whether the compilation has any controller; without one nothing is checked.</summary>
    public bool IsEmpty => _entries.Length == 0;

    /// <summary>Gets the controller names, sorted, for messages.</summary>
    public IEnumerable<string> ControllerNames => _entries.Select(e => e.Name);

    /// <summary>
    /// Looks up a controller.
    /// </summary>
    /// <param name="name">The route name, any case.</param>
    /// <param name="actions">Its actions when found.</param>
    /// <returns><see langword="true"/> when the controller exists.</returns>
    public bool TryGetController(string name, out IReadOnlyCollection<string> actions)
    {
        var found = _controllers.TryGetValue(name, out var set);
        actions = found ? set! : Array.Empty<string>();
        return found;
    }

    /// <summary>Gets the sorted action names of a controller that exists, for messages.</summary>
    /// <param name="name">The route name, any case.</param>
    public IEnumerable<string> ActionNamesOf(string name) =>
        _entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))?.Actions ?? ImmutableArray<string>.Empty;

    /// <inheritdoc />
    public bool Equals(RouteCatalogue? other) => other is not null && IsModule == other.IsModule && _entries.SequenceEqual(other._entries);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as RouteCatalogue);

    /// <inheritdoc />
    public override int GetHashCode() => _entries.Length;
}
