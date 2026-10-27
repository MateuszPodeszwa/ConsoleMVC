namespace ConsoleMVC.Rendering;

/// <summary>
/// Writes lines through <see cref="System.Console"/>'s colour properties. Used as a fallback on legacy
/// Windows consoles that cannot process ANSI escape sequences. Only the 16 console colours are
/// available; decorations other than colour are ignored.
/// </summary>
/// <example><code>IRenderTarget target = new SystemConsoleRenderTarget(useColour: true);</code></example>
public sealed class SystemConsoleRenderTarget : IRenderTarget
{
    private readonly bool _useColour;

    /// <summary>
    /// Creates a render target that writes to <see cref="System.Console"/>.
    /// </summary>
    /// <param name="useColour"><see langword="false"/> to ignore colours (for example when <c>NO_COLOR</c> is set).</param>
    public SystemConsoleRenderTarget(bool useColour)
    {
        _useColour = useColour;
    }

    /// <inheritdoc />
    public void Write(RenderedLine line)
    {
        WriteInline(line);
        Console.WriteLine();
    }

    /// <inheritdoc />
    public void WriteInline(RenderedLine line)
    {
        foreach (var span in line.Spans)
        {
            var coloured = _useColour && (!span.Style.Foreground.IsDefault || !span.Style.Background.IsDefault);

            if (coloured)
            {
                if (!span.Style.Foreground.IsDefault)
                    Console.ForegroundColor = ConsolePalette.ToConsoleColour(span.Style.Foreground);
                if (!span.Style.Background.IsDefault)
                    Console.BackgroundColor = ConsolePalette.ToConsoleColour(span.Style.Background);
            }

            Console.Write(span.Text);

            if (coloured)
                Console.ResetColor();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // The console handle is not a real console (e.g. redirected); nothing to clear.
        }
    }

    /// <inheritdoc />
    public void Flush() => Console.Out.Flush();
}
