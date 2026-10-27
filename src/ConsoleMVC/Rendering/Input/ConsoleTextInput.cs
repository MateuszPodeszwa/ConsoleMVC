using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Reads input from <see cref="Console.In"/>. Masked reading intercepts keys so typed characters are never
/// echoed; when input is redirected (keys cannot be intercepted) it falls back to <see cref="Console.ReadLine"/>.
/// </summary>
/// <example><code>var password = ConsoleTextInput.Instance.ReadMasked('*');</code></example>
public sealed class ConsoleTextInput : ITextInput
{
    private ConsoleTextInput()
    {
    }

    /// <summary>Gets the shared instance.</summary>
    public static ConsoleTextInput Instance { get; } = new();

    /// <inheritdoc />
    public string? ReadLine() => Console.ReadLine();

    /// <inheritdoc />
    /// <remarks>Supports Backspace. Control characters other than Enter and Backspace are ignored.</remarks>
    public string? ReadMasked(char mask)
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var value = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (char.IsControl(key.KeyChar))
                continue;

            value.Append(key.KeyChar);
            Console.Write(mask);
        }
    }
}
