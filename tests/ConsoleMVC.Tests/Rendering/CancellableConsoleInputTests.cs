using System.Diagnostics;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for <see cref="CancellableConsoleInput"/>: a cancelled run stops while the console waits for a key, rather
/// than after the user's next key.
/// </summary>
public class CancellableConsoleInputTests
{
    /// <summary>A console nobody types into: no key ever arrives, and reading one would block for ever.</summary>
    private sealed class SilentConsole : ITextInput
    {
        public bool CanReadKeys => true;
        public bool WaitForInput(TimeSpan timeout)
        {
            Thread.Sleep(timeout);
            return false;
        }

        public string ReadLine() => throw new InvalidOperationException("Read while no key was typed.");
        public string ReadMasked(char mask) => throw new InvalidOperationException("Read while no key was typed.");
        public ConsoleKeyInfo? ReadKey() => throw new InvalidOperationException("Read while no key was typed.");
    }

    [Fact]
    public void ACancelledRun_StopsWaitingForAKey()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var input = new CancellableConsoleInput(new SilentConsole(), cancellation.Token);
        var clock = Stopwatch.StartNew();

        Assert.Throws<OperationCanceledException>(() => input.ReadKey());
        Assert.Throws<OperationCanceledException>(() => input.ReadLine());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
    }

    [Fact]
    public void AKeyThatArrives_IsRead()
    {
        var input = new CancellableConsoleInput(new ScriptedInput("Ada"), CancellationToken.None);

        Assert.Equal("Ada", input.ReadLine());
    }
}
