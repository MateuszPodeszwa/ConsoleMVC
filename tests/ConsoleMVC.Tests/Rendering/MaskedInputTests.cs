using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for the key-by-key editing behind <see cref="ConsoleTextInput.ReadMasked"/>, via <see cref="MaskedLineEditor"/>.
/// </summary>
public class MaskedInputTests
{
    private static (string? Value, string Echo) Type(params ConsoleKeyInfo[] keys)
    {
        var echo = new StringWriter();
        var editor = new MaskedLineEditor('*', echo);
        string? result = null;
        var finished = false;

        foreach (var key in keys)
        {
            if (editor.Process(key, out result))
            {
                finished = true;
                break;
            }
        }

        Assert.True(finished, "the editor did not finish");
        return (result, echo.ToString());
    }

    private static ConsoleKeyInfo Char(char c) => new(c, 0, false, false, false);
    private static readonly ConsoleKeyInfo Enter = new('\r', ConsoleKey.Enter, false, false, false);
    private static readonly ConsoleKeyInfo Backspace = new('\b', ConsoleKey.Backspace, false, false, false);
    private static readonly ConsoleKeyInfo CtrlD = new('\u0004', ConsoleKey.D, false, false, true);

    [Fact]
    public void Characters_AreMasked_AndReturned()
    {
        var (value, echo) = Type(Char('a'), Char('b'), Enter);

        Assert.Equal("ab", value);
        Assert.Equal("**" + Environment.NewLine, echo);
    }

    [Fact]
    public void SurrogatePair_ShowsOneMask_AndBackspaceRemovesItWhole()
    {
        var emoji = "😀";
        var (value, echo) = Type(Char('a'), Char(emoji[0]), Char(emoji[1]), Backspace, Enter);

        Assert.Equal("a", value);
        Assert.Equal("**\b \b" + Environment.NewLine, echo);
    }

    [Fact]
    public void SurrogatePair_IsKeptIntact()
    {
        var emoji = "😀";
        var (value, _) = Type(Char(emoji[0]), Char(emoji[1]), Enter);

        Assert.Equal(emoji, value);
    }

    [Fact]
    public void CtrlD_OnAnEmptyLine_IsEndOfInput()
    {
        var (value, _) = Type(CtrlD);

        Assert.Null(value);
    }

    [Fact]
    public void Backspace_OnEmptyLine_DoesNothing()
    {
        var (value, echo) = Type(Backspace, Char('x'), Enter);

        Assert.Equal("x", value);
        Assert.Equal("*" + Environment.NewLine, echo);
    }
}
