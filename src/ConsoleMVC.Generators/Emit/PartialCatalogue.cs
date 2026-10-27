#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// A partial view known to the generator: a <c>.cvw</c> file in <c>Views/Shared</c>.
/// </summary>
internal sealed class PartialEntry : IEquatable<PartialEntry>
{
    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <param name="name">The partial's name (its file name), used in <c>&lt;partial name="…"&gt;</c>.</param>
    /// <param name="fullClassName">The generated class, e.g. <c>MyApp.Views.Shared.Header</c>.</param>
    /// <param name="modelType">The type in its <c>@model</c> directive, or <see langword="null"/> when it has none.</param>
    /// <param name="path">The file path, which decides which of two clashing partials is kept (as for CVW004).</param>
    /// <param name="hasErrors">Whether the partial itself has errors, so no class is generated for it.</param>
    /// <param name="isLayout">Whether the file has a <c>&lt;body /&gt;</c> placeholder, making it a layout.</param>
    public PartialEntry(string name, string fullClassName, string? modelType, string path = "", bool hasErrors = false,
        bool isLayout = false)
    {
        IsLayout = isLayout;
        Name = name;
        FullClassName = fullClassName;
        ModelType = modelType;
        Path = path;
        HasErrors = hasErrors;
    }

    /// <summary>Gets a value indicating whether this is a layout rather than a partial.</summary>
    public bool IsLayout { get; }

    /// <summary>Gets the file path.</summary>
    public string Path { get; }

    /// <summary>Gets a value indicating whether the partial has errors and is therefore not generated.</summary>
    public bool HasErrors { get; }

    /// <summary>Gets the partial's name.</summary>
    public string Name { get; }

    /// <summary>Gets the generated class name, without <c>global::</c>.</summary>
    public string FullClassName { get; }

    /// <summary>Gets the declared model type, or <see langword="null"/> when the partial has no <c>@model</c>.</summary>
    public string? ModelType { get; }

    /// <inheritdoc />
    public bool Equals(PartialEntry? other) =>
        other is not null && Name == other.Name && FullClassName == other.FullClassName && ModelType == other.ModelType
        && Path == other.Path && HasErrors == other.HasErrors && IsLayout == other.IsLayout;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PartialEntry);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(FullClassName);
}

/// <summary>
/// Every partial view in the project, looked up by name (case-insensitively, like the router). It has value
/// equality, so editing the body of a partial does not regenerate the views that use it.
/// </summary>
/// <example>
/// <code>
/// if (catalogue.TryGet("Header", out var header))
///     Emit($"new global::{header.FullClassName}()");
/// </code>
/// </example>
internal sealed class PartialCatalogue : IEquatable<PartialCatalogue>
{
    private readonly Dictionary<string, PartialEntry> _entries;

    /// <summary>
    /// Creates a catalogue. When two partials differ only by case, the first is kept (the duplicate is reported as
    /// CVW004 separately).
    /// </summary>
    /// <param name="entries">The partial views.</param>
    public PartialCatalogue(IEnumerable<PartialEntry> entries)
    {
        _entries = new Dictionary<string, PartialEntry>(StringComparer.OrdinalIgnoreCase);
        // The same order as the duplicate check (CVW004), so the partial kept here is the one that is generated
        foreach (var entry in entries.OrderBy(e => e.Path, StringComparer.Ordinal))
        {
            if (!_entries.ContainsKey(entry.Name))
                _entries[entry.Name] = entry;
        }
    }

    /// <summary>Gets an empty catalogue.</summary>
    public static PartialCatalogue Empty { get; } = new([]);

    /// <summary>Gets the partial names (not layouts), sorted, for error messages.</summary>
    public IEnumerable<string> Names => _entries.Values.Where(e => !e.IsLayout).Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal);

    /// <summary>Gets the layout names, sorted, for error messages.</summary>
    public IEnumerable<string> LayoutNames => _entries.Values.Where(e => e.IsLayout).Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal);

    /// <summary>
    /// Looks up a partial by name.
    /// </summary>
    /// <param name="name">The name in the <c>&lt;partial&gt;</c> element.</param>
    /// <param name="entry">The partial when found.</param>
    /// <returns><see langword="true"/> when the partial exists.</returns>
    public bool TryGet(string name, out PartialEntry entry) => _entries.TryGetValue(name, out entry!);

    /// <inheritdoc />
    public bool Equals(PartialCatalogue? other) =>
        other is not null && _entries.Count == other._entries.Count
        && _entries.All(pair => other._entries.TryGetValue(pair.Key, out var entry) && entry.Equals(pair.Value));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PartialCatalogue);

    /// <inheritdoc />
    public override int GetHashCode() => _entries.Count;
}
