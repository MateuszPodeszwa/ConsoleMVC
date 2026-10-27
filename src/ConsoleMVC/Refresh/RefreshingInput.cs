using System.Diagnostics;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Thrown by <see cref="RefreshingInput"/> when a screen's refresh interval passes before the user starts answering; the
/// application loop catches it and runs the same route again.
/// </summary>
internal sealed class ViewRefreshException() : Exception("The screen's refresh interval passed before the user answered.");

/// <summary>
/// Wraps the application's input so a screen with a refresh interval (<c>@refresh</c> or <see cref="ViewResult.RefreshEvery"/>)
/// is drawn again when nothing is typed in time. Before every read it asks the input to <see cref="ITextInput.WaitForInput"/>
/// for the rest of the interval; once the user starts answering, the screen stops refreshing until it is left.
/// </summary>
internal sealed class RefreshingInput(ITextInput inner) : ITextInput
{
    private TimeSpan? _interval;
    private long _deadline;
    private bool _answering;

    /// <summary>Gets the wrapped input.</summary>
    public ITextInput Inner => inner;

    /// <summary>Starts timing a screen: it refreshes after <paramref name="interval"/>, or never when it is <see langword="null"/>.</summary>
    public void Arm(TimeSpan? interval)
    {
        _interval = interval;
        _deadline = Stopwatch.GetTimestamp() + (long)((interval ?? TimeSpan.Zero).TotalSeconds * Stopwatch.Frequency);
        _answering = false;
    }

    /// <summary>Stops timing: the next screen does not refresh unless it is armed again.</summary>
    public void Disarm() => _interval = null;

    /// <inheritdoc />
    public bool CanReadKeys => inner.CanReadKeys;

    /// <inheritdoc />
    public bool WaitForInput(TimeSpan timeout) => inner.WaitForInput(timeout);

    /// <inheritdoc />
    public string? ReadLine()
    {
        AwaitAnswer();
        return inner.ReadLine();
    }

    /// <inheritdoc />
    public string? ReadMasked(char mask)
    {
        AwaitAnswer();
        return inner.ReadMasked(mask);
    }

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey()
    {
        AwaitAnswer();
        return inner.ReadKey();
    }

    /// <summary>Waits for the user's first key on this screen, or throws <see cref="ViewRefreshException"/> when time runs out.</summary>
    private void AwaitAnswer()
    {
        if (_interval is null || _answering)
            return;

        var remaining = TimeSpan.FromSeconds(Math.Max(_deadline - Stopwatch.GetTimestamp(), 0) / (double)Stopwatch.Frequency);
        if (!inner.WaitForInput(remaining))
            throw new ViewRefreshException();

        _answering = true;
    }
}
