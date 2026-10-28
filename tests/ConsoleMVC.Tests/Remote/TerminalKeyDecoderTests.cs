using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests.Remote;

/// <summary>Tests for <see cref="TerminalKeyDecoder"/> (docs/design/remote-sessions.md, section 5).</summary>
public class TerminalKeyDecoderTests
{
    /// <summary>Decodes <paramref name="typed"/>: a wait for more characters finds none, as after a pause.</summary>
    private static List<ConsoleKeyInfo> Decode(string typed, Action<int, int>? windowSize = null)
    {
        var queue = new Queue<char>(typed);
        var decoder = new TerminalKeyDecoder((TimeSpan? _, out char c) => queue.TryDequeue(out c), windowSize);

        var keys = new List<ConsoleKeyInfo>();
        while (decoder.Next() is { } key)
            keys.Add(key);
        return keys;
    }

    private static ConsoleKeyInfo Single(string typed) => Assert.Single(Decode(typed));

    [Fact]
    public void PrintableCharacters_AreTheirKeys()
    {
        var keys = Decode("aZ5 .");

        Assert.Equal([ConsoleKey.A, ConsoleKey.Z, ConsoleKey.D5, ConsoleKey.Spacebar, ConsoleKey.NoName], keys.Select(k => k.Key));
        Assert.Equal("aZ5 .", new string(keys.Select(k => k.KeyChar).ToArray()));
        Assert.Equal(ConsoleModifiers.Shift, keys[1].Modifiers);
        Assert.Equal((ConsoleModifiers)0, keys[0].Modifiers);
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\r\n")]
    [InlineData("\r\0")]
    [InlineData("\n")]
    public void EveryLineEnding_IsOneEnter(string typed)
    {
        var key = Single(typed);

        Assert.Equal(ConsoleKey.Enter, key.Key);
        Assert.Equal('\r', key.KeyChar);
    }

    [Fact]
    public void TheCharacterAfterACarriageReturn_IsNotLost()
    {
        Assert.Equal([ConsoleKey.Enter, ConsoleKey.X], Decode("\rx").Select(k => k.Key));
        Assert.Equal([ConsoleKey.Enter, ConsoleKey.Enter], Decode("\r\r").Select(k => k.Key));
    }

    [Theory]
    [InlineData("\u007f", ConsoleKey.Backspace)]
    [InlineData("\b", ConsoleKey.Backspace)]
    [InlineData("\t", ConsoleKey.Tab)]
    public void EditingKeys(string typed, ConsoleKey expected) => Assert.Equal(expected, Single(typed).Key);

    [Theory]
    [InlineData('\u0001', ConsoleKey.A)]
    [InlineData('\u0003', ConsoleKey.C)]
    [InlineData('\u0004', ConsoleKey.D)]
    [InlineData('\u001a', ConsoleKey.Z)]
    public void ControlCharacters_AreCtrlKeys(char typed, ConsoleKey expected)
    {
        var key = Single(typed.ToString());

        Assert.Equal(expected, key.Key);
        Assert.Equal(ConsoleModifiers.Control, key.Modifiers);
        Assert.Equal(typed, key.KeyChar);
    }

    [Fact]
    public void ALoneEscape_IsTheEscapeKey() => Assert.Equal(ConsoleKey.Escape, Single("\u001b").Key);

    [Fact]
    public void EscapeTwice_IsTwoEscapes_AndEscapeBeforeASequence_StaysAKey()
    {
        Assert.Equal([ConsoleKey.Escape, ConsoleKey.Escape], Decode("\u001b\u001b").Select(k => k.Key));
        Assert.Equal([ConsoleKey.Escape, ConsoleKey.UpArrow], Decode("\u001b\u001b[A").Select(k => k.Key));
    }

    [Theory]
    [InlineData("\u001b[A", ConsoleKey.UpArrow)]
    [InlineData("\u001b[B", ConsoleKey.DownArrow)]
    [InlineData("\u001b[C", ConsoleKey.RightArrow)]
    [InlineData("\u001b[D", ConsoleKey.LeftArrow)]
    [InlineData("\u001bOA", ConsoleKey.UpArrow)]
    [InlineData("\u001b[H", ConsoleKey.Home)]
    [InlineData("\u001b[F", ConsoleKey.End)]
    [InlineData("\u001bOH", ConsoleKey.Home)]
    [InlineData("\u001bOF", ConsoleKey.End)]
    [InlineData("\u001b[1~", ConsoleKey.Home)]
    [InlineData("\u001b[2~", ConsoleKey.Insert)]
    [InlineData("\u001b[3~", ConsoleKey.Delete)]
    [InlineData("\u001b[4~", ConsoleKey.End)]
    [InlineData("\u001b[5~", ConsoleKey.PageUp)]
    [InlineData("\u001b[6~", ConsoleKey.PageDown)]
    [InlineData("\u001bOP", ConsoleKey.F1)]
    [InlineData("\u001bOS", ConsoleKey.F4)]
    [InlineData("\u001b[11~", ConsoleKey.F1)]
    [InlineData("\u001b[15~", ConsoleKey.F5)]
    [InlineData("\u001b[17~", ConsoleKey.F6)]
    [InlineData("\u001b[21~", ConsoleKey.F10)]
    [InlineData("\u001b[23~", ConsoleKey.F11)]
    [InlineData("\u001b[24~", ConsoleKey.F12)]
    public void EscapeSequences_AreTheirKeys(string typed, ConsoleKey expected)
    {
        var key = Single(typed);

        Assert.Equal(expected, key.Key);
        Assert.Equal((ConsoleModifiers)0, key.Modifiers);
    }

    [Theory]
    [InlineData("\u001b[1;2D", ConsoleKey.LeftArrow, ConsoleModifiers.Shift)]
    [InlineData("\u001b[1;3A", ConsoleKey.UpArrow, ConsoleModifiers.Alt)]
    [InlineData("\u001b[1;5C", ConsoleKey.RightArrow, ConsoleModifiers.Control)]
    [InlineData("\u001b[3;5~", ConsoleKey.Delete, ConsoleModifiers.Control)]
    [InlineData("\u001b[15;6~", ConsoleKey.F5, ConsoleModifiers.Control | ConsoleModifiers.Shift)]
    [InlineData("\u001b[1;5P", ConsoleKey.F1, ConsoleModifiers.Control)]
    public void Modifiers_AreDecoded(string typed, ConsoleKey expected, ConsoleModifiers modifiers)
    {
        var key = Single(typed);

        Assert.Equal(expected, key.Key);
        Assert.Equal(modifiers, key.Modifiers);
    }

    [Fact]
    public void ShiftTab_IsTabWithShift()
    {
        var key = Single("\u001b[Z");

        Assert.Equal(ConsoleKey.Tab, key.Key);
        Assert.Equal(ConsoleModifiers.Shift, key.Modifiers);
    }

    [Fact]
    public void EscapeAndACharacter_IsAlt()
    {
        var key = Single("\u001bx");

        Assert.Equal(ConsoleKey.X, key.Key);
        Assert.Equal(ConsoleModifiers.Alt, key.Modifiers);
    }

    [Fact]
    public void MouseReports_ArePassedOnAsTyped()
    {
        var keys = Decode("\u001b[<0;3;9M");

        Assert.Equal(ConsoleKey.Escape, keys[0].Key);
        Assert.Equal("[<0;3;9M", new string(keys.Skip(1).Select(k => k.KeyChar).ToArray()));
    }

    [Fact]
    public void AWindowSizeReport_ResizesAndIsNotAKey()
    {
        (int Columns, int Rows)? size = null;

        var keys = Decode("\u001b[8;40;120tq", (columns, rows) => size = (columns, rows));

        Assert.Equal((120, 40), size);
        Assert.Equal(ConsoleKey.Q, Assert.Single(keys).Key);
    }

    [Theory]
    [InlineData("\u001b[99x", "[99x")]
    [InlineData("\u001b[7;1t", "[7;1t")]
    [InlineData("\u001bO", "O")]
    [InlineData("\u001b[12", "[12")]
    public void UnknownOrUnfinishedSequences_ArePassedOnAsTyped(string typed, string rest)
    {
        var keys = Decode(typed);

        // "ESC O" with nothing after it is Alt+Shift+O
        if (rest == "O")
        {
            Assert.Equal(ConsoleModifiers.Alt | ConsoleModifiers.Shift, Assert.Single(keys).Modifiers);
            return;
        }

        Assert.Equal(ConsoleKey.Escape, keys[0].Key);
        Assert.Equal(rest, new string(keys.Skip(1).Select(k => k.KeyChar).ToArray()));
    }

    [Fact]
    public void AVeryLongSequence_IsPassedOn_NotBufferedForEver()
    {
        var typed = "\u001b[" + new string('1', 100) + "~";

        var keys = Decode(typed);

        Assert.Equal(typed.Length, keys.Count);
    }

    [Fact]
    public void NulCharacters_AreIgnored() => Assert.Equal([ConsoleKey.A], Decode("\0a\0").Select(k => k.Key));
}
