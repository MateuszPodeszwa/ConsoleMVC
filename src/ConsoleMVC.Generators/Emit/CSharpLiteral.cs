#nullable enable

using System.Globalization;
using System.Text;

namespace ConsoleMVC.Generators.Emit;

/// <summary>
/// Produces C# source literals.
/// </summary>
/// <example><code>CSharpLiteral.String("say \"hi\"\n"); // "\"say \\\"hi\\\"\\n\""</code></example>
internal static class CSharpLiteral
{
    /// <summary>
    /// Returns <paramref name="value"/> as a quoted, escaped C# string literal. Control and non-ASCII
    /// characters are written as <c>\uXXXX</c> escapes so the generated file is plain ASCII.
    /// </summary>
    /// <param name="value">The string to quote.</param>
    /// <returns>The C# literal, including the surrounding quotes.</returns>
    public static string String(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < 0x20 || c > 0x7E)
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        builder.Append(c);
                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
