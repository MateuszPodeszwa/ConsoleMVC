using System.Text;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Testing;

/// <summary>
/// A render target that keeps what the application draws as plain text, one entry per screen: every
/// <see cref="Clear"/> (the framework clears before each view) starts a new screen. It can erase lines like a
/// terminal, so arrow-key fields are redrawn in place and the recorded screen shows their final state.
/// </summary>
/// <example>
/// <code>
/// var recorder = new ScreenRecorder();
/// builder.Renderer = new MarkupRenderer(recorder, () => 80);
/// // after the run
/// string first = recorder.Screens[0];
/// </code>
/// </example>
public sealed class ScreenRecorder : IRenderTarget
{
    private readonly object _sync = new();
    private readonly List<List<string>> _screens = [[""]];

    /// <summary>Gets the screens drawn, oldest first; screens on which nothing was drawn are left out.</summary>
    /// <example><code>Assert.Contains("Welcome", recorder.Screens[0]);</code></example>
    public IReadOnlyList<string> Screens
    {
        get
        {
            lock (_sync)
                return _screens.Select(Text).Where(s => s.Length > 0).ToArray();
        }
    }

    /// <summary>Gets everything drawn, all screens one after another.</summary>
    /// <example><code>Assert.Contains("Goodbye", recorder.Output);</code></example>
    public string Output => string.Concat(Screens);

    /// <inheritdoc />
    public void Write(RenderedLine line)
    {
        lock (_sync)
        {
            var screen = _screens[^1];
            screen[^1] += line.ToPlainText();
            screen.Add("");
        }
    }

    /// <inheritdoc />
    public void WriteInline(RenderedLine line)
    {
        lock (_sync)
        {
            var screen = _screens[^1];
            screen[^1] += line.ToPlainText();
        }
    }

    /// <inheritdoc />
    /// <remarks>Starts a new screen, unless nothing has been drawn on the current one.</remarks>
    public void Clear()
    {
        lock (_sync)
        {
            if (Text(_screens[^1]).Length > 0)
                _screens.Add([""]);
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
    }

    /// <inheritdoc />
    /// <remarks>Removes the last <paramref name="count"/> complete lines of the current screen.</remarks>
    public bool TryEraseLines(int count)
    {
        lock (_sync)
        {
            var screen = _screens[^1];
            var complete = screen.Count - 1;
            var erase = Math.Min(Math.Max(count, 0), complete);
            screen.RemoveRange(complete - erase, erase);
            return true;
        }
    }

    private static string Text(List<string> lines)
    {
        var text = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                text.Append('\n');
            text.Append(lines[i]);
        }

        return text.ToString();
    }
}
