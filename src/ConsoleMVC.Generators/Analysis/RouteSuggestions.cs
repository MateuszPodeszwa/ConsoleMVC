#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleMVC.Generators.Analysis;

/// <summary>
/// Lists the controllers or actions a navigation could have meant, the closest spelling first, for the CVW119 and
/// CVW120 messages: <c>Actions: Details, Delete, Index and 6 more.</c>
/// </summary>
internal static class RouteSuggestions
{
    /// <summary>The most names listed in a message.</summary>
    public const int MaxListed = 8;

    /// <summary>
    /// Lists <paramref name="names"/>, the ones closest to <paramref name="wanted"/> first (then alphabetically), at most
    /// <see cref="MaxListed"/> of them, saying how many more there are.
    /// </summary>
    /// <param name="names">The existing names.</param>
    /// <param name="wanted">The name that was written, e.g. <c>Detials</c>.</param>
    /// <returns>The list, e.g. <c>Details, Index and 9 more</c>.</returns>
    /// <example><code>RouteSuggestions.List(["Index", "Details"], "Detials"); // "Details, Index"</code></example>
    public static string List(IEnumerable<string> names, string wanted)
    {
        var all = names.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => Distance(name, wanted))
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();
        var shown = string.Join(", ", all.Take(MaxListed));
        return all.Count > MaxListed ? $"{shown} and {all.Count - MaxListed} more" : shown;
    }

    /// <summary>
    /// The name that <paramref name="wanted"/> is most likely a misspelling of (at most a third of its letters
    /// different), or <see langword="null"/>.
    /// </summary>
    /// <param name="names">The existing names.</param>
    /// <param name="wanted">The name that was written.</param>
    /// <returns>The closest name, or <see langword="null"/> when none is close.</returns>
    /// <example><code>RouteSuggestions.Closest(["Index", "Details"], "Detials"); // "Details"</code></example>
    public static string? Closest(IEnumerable<string> names, string wanted)
    {
        var limit = Math.Max(1, wanted.Length / 3);
        return names.Select(name => (Name: name, Distance: Distance(name, wanted)))
            .Where(candidate => candidate.Distance <= limit)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .Select(candidate => candidate.Name)
            .FirstOrDefault();
    }

    /// <summary>
    /// The text that ends a message: <c> Did you mean 'Details'?</c> when a name is close, else nothing.
    /// </summary>
    /// <example><code>RouteSuggestions.DidYouMean(["Details"], "Detials"); // " Did you mean 'Details'?"</code></example>
    public static string DidYouMean(IEnumerable<string> names, string wanted) =>
        Closest(names, wanted) is { } closest ? $" Did you mean '{closest}'?" : "";

    /// <summary>
    /// The optimal string alignment distance between two names, ignoring case: how many letters to insert, delete,
    /// change or swap with a neighbour to turn one into the other (<c>Detials</c> to <c>Details</c> is 1).
    /// </summary>
    private static int Distance(string a, string b)
    {
        a = a.ToUpperInvariant();
        b = b.ToUpperInvariant();
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
            d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++)
            d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }

        return d[a.Length, b.Length];
    }
}
