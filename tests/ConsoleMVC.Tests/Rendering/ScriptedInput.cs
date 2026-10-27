using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// An <see cref="ITextInput"/> that returns pre-scripted lines, then <see langword="null"/> (end of input).
/// </summary>
/// <example><code>var input = new ScriptedInput("1", "Ada");</code></example>
public sealed class ScriptedInput(params string[] lines) : ITextInput
{
    private readonly Queue<string> _lines = new(lines);

    /// <summary>Gets how many times <see cref="ReadMasked"/> was called.</summary>
    public int MaskedReads { get; private set; }

    /// <inheritdoc />
    public string? ReadLine() => _lines.Count > 0 ? _lines.Dequeue() : null;

    /// <inheritdoc />
    public string? ReadMasked(char mask)
    {
        MaskedReads++;
        return ReadLine();
    }
}
