using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for <c>&lt;progress&gt;</c>, <c>&lt;spinner&gt;</c> and live regions (docs/design/live-elements.md).
/// </summary>
public class LiveElementTests
{
    private static string Line(Element element, int width = 40) =>
        string.Join('\n', element.Layout(width).Select(l => l.ToPlainText()));

    private static (MarkupRenderer Renderer, RewritableScreen Screen) Screen(int width = 40)
    {
        var screen = new RewritableScreen();
        var renderer = new MarkupRenderer(screen, () => width);
        renderer.Clear();
        return (renderer, screen);
    }

    private static (MarkupRenderer Renderer, StringWriter Output) Piped(int width = 40)
    {
        var output = new StringWriter { NewLine = "\n" };
        return (new MarkupRenderer(new PlainTextRenderTarget(output), () => width), output);
    }

    // ------------------------------------------------------------------ <progress>

    [Theory]
    [InlineData(0, "░░░░░░░░░░   0%")]
    [InlineData(25, "███░░░░░░░  25%")]
    [InlineData(50, "█████░░░░░  50%")]
    [InlineData(100, "██████████ 100%")]
    [InlineData(150, "██████████ 100%")]
    [InlineData(-5, "░░░░░░░░░░   0%")]
    public void Progress_FillsTheBar_AndClampsToTheRange(double value, string expected)
    {
        var progress = new ProgressElement { Value = value, BarWidth = 10 };

        Assert.Equal(expected, Line(progress));
    }

    [Fact]
    public void Progress_FillsTheWidthAroundItsLabel_WhenNoBarWidthIsSet()
    {
        var progress = new ProgressElement { Label = "Import ", Value = 3, Max = 4 };

        var line = Line(progress, 30);

        Assert.Equal(30, line.Length);
        Assert.StartsWith("Import ", line);
        Assert.EndsWith("  75%", line);
        Assert.Equal(14, line.Count(c => c == '█')); // 18 columns of bar, three quarters filled
    }

    [Fact]
    public void Progress_CanHideThePercentage_AndUseAnotherStyle()
    {
        var progress = new ProgressElement { Value = 1, Max = 2, BarWidth = 8, ShowPercent = false, Style = ProgressStyle.Ascii };

        Assert.Equal("####----", Line(progress));
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(double.NaN, 10)]
    [InlineData(5, double.NaN)]
    [InlineData(5, double.PositiveInfinity)]
    public void Progress_IsEmpty_WhenTheValuesMakeNoSense(double value, double max)
    {
        var progress = new ProgressElement { Value = value, Max = max, BarWidth = 4 };

        Assert.Equal(0, progress.Fraction);
        Assert.Equal("░░░░   0%", Line(progress));
    }

    [Fact]
    public void Progress_KeepsTheBar_OnANarrowLine_AndSanitisesTheLabel()
    {
        var progress = new ProgressElement { Label = "A very long label\u001b[31m", Value = 50 };

        var line = Line(progress, 12);

        Assert.DoesNotContain('\u001b', line);
        Assert.True(line.Length <= 12);
        Assert.Contains('█', line);
    }

    [Fact]
    public void Progress_MeasuresItsFixedWidth_OrFillsItsContainer()
    {
        Assert.Equal(0, new ProgressElement().MeasureWidth(40));
        Assert.Equal(15, new ProgressElement { BarWidth = 10 }.MeasureWidth(40));
        Assert.Equal(17, new ProgressElement { BarWidth = 10, Label = "Go", ShowPercent = true }.MeasureWidth(40));
    }

    // ------------------------------------------------------------------ <spinner>

    [Fact]
    public void Spinner_ShowsItsFrame_AndAdvancesThroughThem()
    {
        var spinner = new SpinnerElement { Label = "Loading", Style = SpinnerStyle.Line };

        var frames = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            frames.Add(Line(spinner));
            spinner.Advance();
        }

        Assert.Equal(["| Loading", "/ Loading", "- Loading", "\\ Loading", "| Loading"], frames);
    }

    [Fact]
    public void Spinner_ShowsItsDoneSymbol_WhenDone()
    {
        var spinner = new SpinnerElement { Label = "Connected", Done = true };

        Assert.Equal("✓ Connected", Line(spinner));

        spinner.DoneSymbol = "OK";
        Assert.Equal("OK Connected", Line(spinner));
    }

    [Fact]
    public void Spinner_WrapsAnyFrameNumber_AndDrawsWithoutALabel()
    {
        var spinner = new SpinnerElement { Style = SpinnerStyle.Line, Frame = -3 };

        Assert.Equal("/", Line(spinner));
        Assert.Equal(1, spinner.MeasureWidth(40));
    }

    [Fact]
    public void SpinnerStyle_RefusesFramesOfDifferentWidths()
    {
        Assert.Throws<ArgumentException>(() => new SpinnerStyle("bad", ["a", "bb"]));
        Assert.Throws<ArgumentException>(() => new SpinnerStyle("empty", []));
    }

    // ------------------------------------------------------------------ live regions

    [Fact]
    public void LiveRegion_RedrawsInPlace_WhenUpdated()
    {
        var (renderer, screen) = Screen();
        var display = new LiveDisplay(renderer);
        var progress = new ProgressElement { Label = "Copy ", BarWidth = 4, Max = 4 };

        using (var live = display.Start(progress, TimeSpan.FromMinutes(1)))
        {
            Assert.Equal("Copy ░░░░   0%\n", screen.Screen);

            live.Update(() => progress.Value = 2);
            Assert.Equal("Copy ██░░  50%\n", screen.Screen);

            live.Update(() => progress.Value = 4);
        }

        Assert.Equal("Copy ████ 100%\n", screen.Screen);
    }

    [Fact]
    public void LiveRegion_AnimatesSpinners_InsideContainers_OnEachTick()
    {
        var (renderer, screen) = Screen();
        var display = new LiveDisplay(renderer);
        var spinner = new SpinnerElement { Label = "Wait", Style = SpinnerStyle.Line };
        var box = new BoxElement { Border = BorderStyle.Ascii };
        box.Add(spinner);

        using var live = display.Start(box, TimeSpan.FromMinutes(1));
        live.Tick();
        live.Tick();

        Assert.Equal(2, spinner.Frame);
        Assert.Contains("- Wait", screen.Screen);
        Assert.Single(screen.Screen.Split('\n'), l => l.Contains("Wait")); // replaced, not appended
    }

    [Fact]
    public void LiveRegion_DrawsOnlyTheFinalState_WhenTheOutputCannotBeRewritten()
    {
        var (renderer, output) = Piped();
        var display = new LiveDisplay(renderer);
        var progress = new ProgressElement { BarWidth = 4 };

        using (var live = display.Start(progress, TimeSpan.FromMinutes(1)))
        {
            live.Update(() => progress.Value = 50);
            live.Tick();
            Assert.Equal("", output.ToString());
            live.Update(() => progress.Value = 100);
        }

        Assert.Equal("████ 100%\n", output.ToString());
    }

    [Fact]
    public void LiveRegion_StopsAfterDispose()
    {
        var (renderer, screen) = Screen();
        var spinner = new SpinnerElement { Label = "x" };
        var live = new LiveDisplay(renderer).Start(spinner, TimeSpan.FromMinutes(1));

        live.Dispose();
        live.Dispose(); // twice is fine
        live.Tick();
        live.Update(() => spinner.Label = "changed");

        Assert.Equal(0, spinner.Frame);
        Assert.Equal("changed", spinner.Label); // the change is still made, just not drawn
        Assert.Equal("⠋ x\n", screen.Screen);
    }

    [Fact]
    public async Task LiveRegion_TicksOnItsOwn_WithTheSystemClock()
    {
        var (renderer, _) = Screen();
        var spinner = new SpinnerElement();

        await using (new LiveDisplay(renderer).Start(spinner, TimeSpan.FromMilliseconds(20)))
        {
            var waited = System.Diagnostics.Stopwatch.StartNew();
            while (spinner.Frame < 2 && waited.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(10);
        }

        Assert.True(spinner.Frame >= 2);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(120_000)]
    public void LiveDisplay_RefusesIntervalsOutOfRange(int milliseconds)
    {
        var (renderer, _) = Screen();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LiveDisplay(renderer).Start(new SpinnerElement(), TimeSpan.FromMilliseconds(milliseconds)));
    }
}
