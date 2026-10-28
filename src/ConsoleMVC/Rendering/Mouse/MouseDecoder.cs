using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Turns the mouse reports of a terminal (SGR mode: <c>ESC [ &lt; button ; column ; row M</c>, one character at a time)
/// into keys: a left click on a line an element registered (<see cref="IMarkupRenderer.MapClicks"/>) becomes that
/// line's keys, the wheel becomes the Up and Down arrows, and everything else is ignored. Other keys, including a lone
/// Escape, pass through unchanged. It wraps the raw input, so shortcuts and line editing see the keys it produces.
/// </summary>
internal sealed class MouseDecoder(ITextInput inner, IMarkupRenderer renderer) : ITextInput
{
    /// <summary>How long to wait for the rest of a sequence after Escape before taking it as the Escape key.</summary>
    private static readonly TimeSpan SequenceGap = TimeSpan.FromMilliseconds(25);

    /// <summary>The longest report read; anything longer is not a mouse report.</summary>
    private const int MaxReportLength = 20;

    private readonly Queue<ConsoleKeyInfo> _pending = new();

    /// <inheritdoc />
    public bool CanReadKeys => _pending.Count > 0 || inner.CanReadKeys;

    /// <inheritdoc />
    public bool WaitForInput(TimeSpan timeout) => _pending.Count > 0 || inner.WaitForInput(timeout);

    /// <inheritdoc />
    public string? ReadLine() => inner.ReadLine();

    /// <inheritdoc />
    public string? ReadMasked(char mask) => inner.ReadMasked(mask);

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey()
    {
        while (true)
        {
            if (_pending.TryDequeue(out var queued))
                return queued;

            if (inner.ReadKey() is not { } key)
                return null;

            if (key.Key != ConsoleKey.Escape && key.KeyChar != '\u001b')
                return key;

            // Escape on its own is a key; Escape followed at once by "[<" starts a mouse report
            var read = new List<ConsoleKeyInfo>();
            if (!TryReadReport(read, out var report))
            {
                foreach (var other in read)
                    _pending.Enqueue(other);
                return key;
            }

            Handle(report);
        }
    }

    /// <summary>Reads "[&lt;b;x;yM" after an Escape; the keys read are kept in <paramref name="read"/> either way.</summary>
    private bool TryReadReport(List<ConsoleKeyInfo> read, out (int Button, int Column, int Row, bool Press) report)
    {
        report = default;
        foreach (var expected in "[<")
        {
            if (!Next(read, out var c) || c != expected)
                return false;
        }

        var text = new System.Text.StringBuilder();
        while (text.Length < MaxReportLength)
        {
            if (!Next(read, out var c))
                return false;

            if (c is 'M' or 'm')
            {
                var parts = text.ToString().Split(';');
                if (parts.Length != 3
                    || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var button)
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var column)
                    || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var row))
                    return false;

                report = (button, column - 1, row - 1, c == 'M');
                return true;
            }

            if (!char.IsAsciiDigit(c) && c != ';')
                return false;

            text.Append(c);
        }

        return false;
    }

    private bool Next(List<ConsoleKeyInfo> read, out char c)
    {
        c = '\0';
        if (!inner.WaitForInput(SequenceGap) || inner.ReadKey() is not { } key)
            return false;

        read.Add(key);
        c = key.KeyChar;
        return true;
    }

    private void Handle((int Button, int Column, int Row, bool Press) report)
    {
        if (!report.Press)
            return;

        switch (report.Button)
        {
            case 0: // left button
                if (renderer.KeysAt(report.Row) is { } keys)
                {
                    foreach (var key in keys)
                        _pending.Enqueue(key);
                }

                break;
            case 64:
                _pending.Enqueue(ClickTarget.Key(ConsoleKey.UpArrow));
                break;
            case 65:
                _pending.Enqueue(ClickTarget.Key(ConsoleKey.DownArrow));
                break;
        }
    }
}
