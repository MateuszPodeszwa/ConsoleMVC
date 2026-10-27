using System.Runtime.InteropServices;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Detects what the current terminal supports and creates the most capable <see cref="IRenderTarget"/>.
/// </summary>
/// <remarks>
/// Detection rules, in order:
/// <list type="number">
///   <item><description>Output redirected to a file or pipe → <see cref="PlainTextRenderTarget"/>.</description></item>
///   <item><description><c>NO_COLOR</c> set (https://no-color.org) or <c>TERM=dumb</c> → no colours.</description></item>
///   <item><description>Windows: enable virtual terminal processing; if that fails, fall back to <see cref="SystemConsoleRenderTarget"/>.</description></item>
///   <item><description><c>COLORTERM=truecolor|24bit</c> or Windows Terminal (<c>WT_SESSION</c>) → <see cref="ColourSupport.TrueColour"/>; otherwise <see cref="ColourSupport.Basic"/>.</description></item>
/// </list>
/// </remarks>
/// <example><code>IRenderTarget target = TerminalCapabilities.CreateRenderTarget();</code></example>
public static class TerminalCapabilities
{
    /// <summary>The width used when the terminal width cannot be determined (e.g. redirected output).</summary>
    public const int FallbackWidth = 80;

    /// <summary>
    /// Creates the best render target for the current process's standard output.
    /// </summary>
    /// <returns>A render target suited to the terminal.</returns>
    public static IRenderTarget CreateRenderTarget()
    {
        if (Console.IsOutputRedirected)
            return new PlainTextRenderTarget(Console.Out);

        var colourSupport = DetectColourSupport(Environment.GetEnvironmentVariable);

        if (OperatingSystem.IsWindows() && !WindowsConsoleMode.TryEnableVirtualTerminal())
            return new SystemConsoleRenderTarget(useColour: colourSupport != ColourSupport.None);

        return new AnsiRenderTarget(Console.Out, colourSupport);
    }

    /// <summary>
    /// Determines colour support from environment variables.
    /// </summary>
    /// <param name="getVariable">Reads an environment variable; injectable for testing.</param>
    /// <returns>The detected colour support.</returns>
    /// <example><code>var support = TerminalCapabilities.DetectColourSupport(Environment.GetEnvironmentVariable);</code></example>
    public static ColourSupport DetectColourSupport(Func<string, string?> getVariable)
    {
        if (!string.IsNullOrEmpty(getVariable("NO_COLOR")))
            return ColourSupport.None;

        if (string.Equals(getVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase))
            return ColourSupport.None;

        var colourTerm = getVariable("COLORTERM");
        if (string.Equals(colourTerm, "truecolor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(colourTerm, "24bit", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(getVariable("WT_SESSION")))
            return ColourSupport.TrueColour;

        return ColourSupport.Basic;
    }

    /// <summary>
    /// Returns the usable width of the terminal in columns. One column is kept free because writing
    /// into the last column makes some terminals wrap early.
    /// </summary>
    /// <returns>The terminal width, or <see cref="FallbackWidth"/> when it cannot be determined.</returns>
    public static int GetWidth()
    {
        if (Console.IsOutputRedirected)
            return FallbackWidth;

        try
        {
            var width = Console.WindowWidth;
            return width > 1 ? width - 1 : FallbackWidth;
        }
        catch (Exception e) when (e is IOException or PlatformNotSupportedException)
        {
            return FallbackWidth;
        }
    }

    /// <summary>
    /// Enables ANSI escape sequence processing on Windows consoles via <c>SetConsoleMode</c>.
    /// </summary>
    private static class WindowsConsoleMode
    {
        private const int StandardOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        /// <summary>Attempts to switch the console into VT mode; returns whether it is now enabled.</summary>
        public static bool TryEnableVirtualTerminal()
        {
            try
            {
                var handle = GetStdHandle(StandardOutputHandle);
                if (handle == IntPtr.Zero || !GetConsoleMode(handle, out var mode))
                    return false;

                return (mode & EnableVirtualTerminalProcessing) != 0
                       || SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConsoleMode(IntPtr handle, uint mode);
    }
}
