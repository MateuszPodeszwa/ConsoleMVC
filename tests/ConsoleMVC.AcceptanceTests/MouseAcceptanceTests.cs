using ConsoleMVC.Testing;

namespace Acceptance;

/// <summary>
/// The mouse, as docs/guides/docs/controllers-and-forms.md describes it: "Clicking a menu option, a choice or a
/// checkbox answers it".
/// </summary>
public class MouseAcceptanceTests
{
    private static MvcTestHost Host(int width = 80)
    {
        var host = MvcTestHost.For<Controllers.HomeController>(builder => builder.Mouse = true);
        host.Width = width;
        return host;
    }

    /// <summary>The column where <paramref name="text"/> starts on the radio's row, as a user sees it.</summary>
    private static (int Column, int Row) Find(string screen, string text)
    {
        var rows = screen.Split('\n');
        for (var row = 0; row < rows.Length; row++)
        {
            var column = rows[row].IndexOf(text, StringComparison.Ordinal);
            if (column >= 0)
                return (column, row);
        }

        throw new Xunit.Sdk.XunitException($"'{text}' is not on the screen:\n{screen}");
    }

    [Theory]
    [InlineData("Small")]
    [InlineData("Medium")]
    [InlineData("Large")]
    public void ClickingASideBySideRadioChoice_AnswersIt(string choice)
    {
        // First see where the choice is drawn, as a user would before clicking it
        var waiting = Host().Run(new ScriptedInput("8").Keys(ConsoleKey.F12)).LastScreen;
        var (column, row) = Find(waiting, choice);

        var run = Host().Run(new ScriptedInput("8").Click(column + 1, row).Line("q"));

        Assert.Contains($"size={(choice == "Large" ? "Large and extra wide choice" : choice)}", run.LastScreen);
    }

    [Fact]
    public void ALongChoiceOnANarrowTerminal_Wraps_InsteadOfBeingCut()
    {
        var waiting = Host(width: 24).Run(new ScriptedInput("8").Keys(ConsoleKey.F12)).LastScreen;

        Assert.Contains("choice", waiting);   // the last word of "Large and extra wide choice" is still there
        Assert.All(waiting.Split('\n'), line => Assert.True(line.TrimEnd().Length <= 24, line));
    }

    [Fact]
    public void ClickingTheSecondLineOfAWrappedChoice_AnswersIt()
    {
        var waiting = Host(width: 24).Run(new ScriptedInput("8").Keys(ConsoleKey.F12)).LastScreen;
        var (column, row) = Find(waiting, "choice");

        var run = Host(width: 24).Run(new ScriptedInput("8").Click(column, row).Line("q"));

        // The answer wraps too at this width
        Assert.Contains("size=Large and extra wide choice", string.Join(" ", run.LastScreen.Split((char[])['\n', ' '], StringSplitOptions.RemoveEmptyEntries)));
    }
}
