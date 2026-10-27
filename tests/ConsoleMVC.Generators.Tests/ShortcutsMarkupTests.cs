using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for the <c>&lt;shortcuts /&gt;</c> element.</summary>
public class ShortcutsMarkupTests
{
    [Fact]
    public void Shortcuts_RenderNothingOutsideTheApplication()
    {
        Assert.Equal("a\n", ViewHarness.Render("<text>a</text>\n<shortcuts class=\"muted\" separator=\" | \" key-color=\"cyan\" />\nreturn NavigationResult.Quit();"));
    }

    [Fact]
    public void Shortcuts_AreTheViewsHints()
    {
        const string view = """
            this.ShortcutHints = [new ConsoleMVC.Rendering.ShortcutHint("F1", "Help"), new ConsoleMVC.Rendering.ShortcutHint("Esc", "Back")];
            <shortcuts separator=" | " />
            return NavigationResult.Quit();
            """;

        var result = ViewHarness.Generate(view);

        // ShortcutHints is set by the application; views cannot assign it
        Assert.Contains(result.CompilationDiagnostics, d => d.Id is "CS0272" or "CS0200");
    }

    [Fact]
    public void Shortcuts_RejectContent()
    {
        var result = ViewHarness.Generate("<shortcuts>x</shortcuts>\nreturn NavigationResult.Quit();");

        Assert.NotEmpty(result.GeneratorDiagnostics);
    }
}
