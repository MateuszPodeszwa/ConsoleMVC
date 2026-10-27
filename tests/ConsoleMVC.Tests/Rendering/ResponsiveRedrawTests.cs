using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for redrawing the screen when the terminal is resized, so tables, boxes and columns follow the new width
/// instead of being re-wrapped by the terminal.
/// </summary>
public class ResponsiveRedrawTests
{
    /// <summary>A render target that keeps the screen as plain text, like a terminal would show it.</summary>
    private sealed class ScreenTarget : IRenderTarget
    {
        private readonly System.Text.StringBuilder _screen = new();

        public int Clears { get; private set; }

        public string Screen => _screen.ToString();

        public void Write(RenderedLine line) => _screen.Append(line.ToPlainText()).Append('\n');

        public void WriteInline(RenderedLine line) => _screen.Append(line.ToPlainText());

        public void Clear()
        {
            Clears++;
            _screen.Clear();
        }

        public void Flush() { }
    }

    private static TableElement Table()
    {
        var table = new TableElement();
        var row = new TableRowElement();
        foreach (var text in new[] { "Element", "A rather long description that wraps when narrow" })
        {
            var cell = new TableCellElement();
            cell.AppendText(text);
            row.Add(cell);
        }

        table.Add(row);
        return table;
    }

    private static (MarkupRenderer Renderer, ScreenTarget Target, Func<int, int> SetWidth) Create(int width)
    {
        var target = new ScreenTarget();
        var current = width;
        var renderer = new MarkupRenderer(target, () => current);
        return (renderer, target, w => current = w);
    }

    [Fact]
    public void Redraw_LaysTheScreenOutAgainAtTheNewWidth()
    {
        var (renderer, target, setWidth) = Create(80);
        renderer.Clear();
        renderer.Render(Table());
        Assert.All(target.Screen.TrimEnd('\n').Split('\n'), line => Assert.True(line.Length > 40));

        setWidth(30);
        Assert.True(renderer.RedrawIfWidthChanged());

        var lines = target.Screen.TrimEnd('\n').Split('\n');
        Assert.All(lines, line => Assert.True(line.Length <= 30, line));
        Assert.True(lines.Length > 3); // the long cell now wraps inside the table
    }

    [Fact]
    public void Redraw_ReplaysPromptsAndWhatTheUserTyped()
    {
        var (renderer, target, setWidth) = Create(60);
        renderer.Clear();
        renderer.WritePrompt("Name: ", TextStyle.Plain);
        renderer.RecordInput("Ada");
        renderer.WritePrompt("Age: ", TextStyle.Plain);

        setWidth(40);
        renderer.RedrawIfWidthChanged();

        Assert.Equal("Name: Ada\nAge: ", target.Screen);
    }

    [Fact]
    public void Redraw_DoesNothingWhenTheWidthIsUnchanged()
    {
        var (renderer, target, _) = Create(60);
        renderer.Clear();
        renderer.Render(Table());

        Assert.False(renderer.RedrawIfWidthChanged());
        Assert.Equal(1, target.Clears);
    }

    [Fact]
    public void Redraw_NeedsAScreenTheRendererCleared()
    {
        // Without a clear the screen holds whatever was there before the application started
        var (renderer, target, setWidth) = Create(60);
        renderer.Render(Table());

        setWidth(30);

        Assert.False(renderer.RedrawIfWidthChanged());
        Assert.Equal(0, target.Clears);
    }

    [Fact]
    public void Redraw_IsSkipped_WhenSomethingElseWroteToTheScreen()
    {
        var (renderer, _, setWidth) = Create(60);
        renderer.Clear();
        renderer.Render(Table());
        renderer.MarkForeignOutput(); // e.g. Console.WriteLine in a plain C# view

        setWidth(30);

        Assert.False(renderer.RedrawIfWidthChanged());
    }

    [Fact]
    public void Clear_StartsANewScreen()
    {
        var (renderer, target, setWidth) = Create(60);
        renderer.Clear();
        renderer.Render(Table());
        renderer.MarkForeignOutput();
        renderer.Clear();
        var text = new TextElement();
        text.AppendText("Second screen");
        renderer.Render(text);

        setWidth(30);

        Assert.True(renderer.RedrawIfWidthChanged());
        Assert.Equal("Second screen\n", target.Screen);
    }

    [Fact]
    public void Redraw_IsSkipped_WhenTheScreenHistoryIsTooLong()
    {
        var (renderer, _, setWidth) = Create(60);
        renderer.Clear();
        for (var i = 0; i <= MarkupRenderer.MaxRedrawHistory; i++)
            renderer.WritePrompt("x", TextStyle.Plain);

        setWidth(30);

        Assert.False(renderer.RedrawIfWidthChanged());
    }

    [Fact]
    public void ForeignOutputTracker_MarksOnlyWritesOutsideTheRenderer()
    {
        var (renderer, _, setWidth) = Create(60);
        var original = new StringWriter();
        var tracker = new ForeignOutputTracker(original, renderer);
        renderer.Clear();
        renderer.Render(Table());

        using (ForeignOutputTracker.Suppress())
            tracker.Write("typed by the user");

        setWidth(30);
        Assert.True(renderer.RedrawIfWidthChanged());

        tracker.WriteLine("written by a view");
        setWidth(40);
        Assert.False(renderer.RedrawIfWidthChanged());
        Assert.Contains("written by a view", original.ToString());
    }

    [Fact]
    public void InputAndMenus_RecordWhatTheUserTyped()
    {
        var (renderer, target, setWidth) = Create(60);
        renderer.Clear();
        var input = new InputElement { Name = "Name", Label = "Name: " };
        var password = new InputElement { Name = "Pin", Label = "Pin: ", Masked = true };
        var menu = new MenuElement { Prompt = "> " };
        var option = new MenuOptionElement { Key = "q", Quit = true };
        option.AppendText("Quit");
        menu.Add(option);
        var scripted = new ScriptedInput("Ada", "1234", "q");

        input.Read(renderer, scripted);
        password.Read(renderer, scripted);
        menu.Choose(renderer, scripted);

        setWidth(40);
        renderer.RedrawIfWidthChanged();

        Assert.Equal("Name: Ada\nPin: ****\n[q] Quit\n> q\n", target.Screen);
    }

    [Fact]
    public void Dispose_StopsWatchingAndIsIdempotent()
    {
        var (renderer, _, _) = Create(60);

        renderer.Dispose();
        renderer.Dispose();
    }
}
