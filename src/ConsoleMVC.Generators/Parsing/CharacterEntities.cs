#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConsoleMVC.Generators.Parsing;

/// <summary>
/// Decodes HTML-style character entities in markup text: <c>&amp;lt;</c>, <c>&amp;gt;</c>, <c>&amp;amp;</c>,
/// <c>&amp;quot;</c>, <c>&amp;apos;</c>, <c>&amp;nbsp;</c>, <c>&amp;at;</c> and numeric forms such as
/// <c>&amp;#64;</c> or <c>&amp;#x40;</c>.
/// </summary>
/// <example>
/// <code>
/// CharacterEntities.TryDecode("&amp;lt;b", 0, out var value, out var length); // value "&lt;", length 4
/// </code>
/// </example>
internal static class CharacterEntities
{
    private const int MaxEntityLength = 10;

    private static readonly Dictionary<string, string> Named = new(StringComparer.Ordinal)
    {
        ["lt"] = "<",
        ["gt"] = ">",
        ["amp"] = "&",
        ["quot"] = "\"",
        ["apos"] = "'",
        ["nbsp"] = " ",
        ["at"] = "@"
    };

    /// <summary>
    /// Decodes the entity starting with <c>&amp;</c> at <paramref name="position"/>.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">Offset of the <c>&amp;</c>.</param>
    /// <param name="value">The decoded text when successful.</param>
    /// <param name="length">The number of source characters consumed when successful.</param>
    /// <returns><see langword="true"/> when a known entity was decoded.</returns>
    public static bool TryDecode(string text, int position, out string value, out int length)
    {
        value = "";
        length = 0;

        var end = text.IndexOf(';', position + 1);
        if (end < 0 || end - position > MaxEntityLength)
            return false;

        var name = text.Substring(position + 1, end - position - 1);

        if (name.Length > 1 && name[0] == '#')
        {
            var isHex = name[1] is 'x' or 'X';
            var digits = isHex ? name.Substring(2) : name.Substring(1);
            var style = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;

            if (!int.TryParse(digits, style, CultureInfo.InvariantCulture, out var codePoint)
                || codePoint is < 0x20 or > 0x10FFFF or >= 0xD800 and <= 0xDFFF)
                return false;

            value = char.ConvertFromUtf32(codePoint);
        }
        else if (!Named.TryGetValue(name, out value!))
        {
            value = "";
            return false;
        }

        length = end - position + 1;
        return true;
    }
}
