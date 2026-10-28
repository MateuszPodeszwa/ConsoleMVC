using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.RemoteApp.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsoleMVC.Tests.Remote;

/// <summary>Builds the remote test application and waits for what a client sees.</summary>
internal static class RemoteTestSupport
{
    /// <summary>How long a test waits for output before it fails.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>Builds the application of RemoteAppTypes.cs, starting at Session/Index.</summary>
    public static MvcApplication BuildApp(Action<MvcApplicationBuilder>? configure = null)
    {
        var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.ApplicationAssembly = typeof(SessionController).Assembly;
        builder.DefaultController = "Session";
        builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(TextWriter.Null), () => 80);
        builder.Input = new ConsoleMVC.Testing.ScriptedInput();
        builder.Logging.ClearProviders();
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>Reads from <paramref name="stream"/> until the text read contains <paramref name="expected"/>.</summary>
    public static async Task<string> ReadUntilAsync(Stream stream, StringBuilder seen, string expected)
    {
        using var timeout = new CancellationTokenSource(Patience);
        var buffer = new byte[4096];
        while (!seen.ToString().Contains(expected, StringComparison.Ordinal))
        {
            int count;
            try
            {
                count = await stream.ReadAsync(buffer, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Waited for '{expected}' but saw:\n{seen}");
            }

            if (count == 0)
                throw new EndOfStreamException($"The connection closed before '{expected}' arrived. Saw:\n{seen}");

            // Telnet commands are bytes ≥ 240 and never valid UTF-8 on their own; keep only the text
            seen.Append(Encoding.UTF8.GetString(buffer.AsSpan(0, count).ToArray().Where(b => b < 240).ToArray()));
        }

        return seen.ToString();
    }

    /// <summary>Reads until the connection closes; returns what arrived.</summary>
    public static async Task<string> ReadToEndAsync(Stream stream)
    {
        using var timeout = new CancellationTokenSource(Patience);
        var seen = new StringBuilder();
        var buffer = new byte[4096];
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(buffer, timeout.Token);
                if (count == 0)
                    return seen.ToString();
                seen.Append(Encoding.UTF8.GetString(buffer, 0, count));
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"The connection stayed open. Saw:\n{seen}");
        }
        catch (IOException)
        {
            return seen.ToString(); // reset by the server
        }
    }

    /// <summary>Sends text as UTF-8.</summary>
    public static async Task SendAsync(Stream stream, string text)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text));
        await stream.FlushAsync();
    }

    /// <summary>Waits until <paramref name="condition"/> holds.</summary>
    public static async Task EventuallyAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Timed out waiting until {what}.");
            await Task.Delay(20);
        }
    }

    /// <summary>Connects a TCP client to <paramref name="server"/>.</summary>
    public static async Task<TcpClient> ConnectAsync(TerminalServer server)
    {
        var client = new TcpClient();
        await client.ConnectAsync(server.LocalEndPoint);
        return client;
    }
}

/// <summary>
/// A connection held in memory: the test feeds what the user types and reads what the terminal was sent.
/// </summary>
internal sealed class DuplexTestStream : Stream
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly MemoryStream _output = new();
    private byte[] _current = [];
    private int _offset;

    /// <summary>Types <paramref name="text"/>.</summary>
    public void Type(string text) => _input.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

    /// <summary>Sends raw bytes.</summary>
    public void Send(byte[] bytes) => _input.Writer.TryWrite(bytes);

    /// <summary>Ends the input, as when the client disconnects.</summary>
    public void EndInput() => _input.Writer.TryComplete();

    /// <summary>Gets everything written so far, as text.</summary>
    public string Output
    {
        get
        {
            lock (_output)
                return Encoding.UTF8.GetString(_output.ToArray());
        }
    }

    /// <summary>Gets everything written so far, as bytes.</summary>
    public byte[] OutputBytes
    {
        get
        {
            lock (_output)
                return _output.ToArray();
        }
    }

    public bool IsDisposed { get; private set; }

    /// <summary>Gets or sets whether writes fail, as when the client's connection breaks.</summary>
    public bool FailWrites { get; set; }

    /// <summary>Waits until the output contains <paramref name="expected"/>.</summary>
    public Task WaitForAsync(string expected) =>
        RemoteTestSupport.EventuallyAsync(() => Output.Contains(expected, StringComparison.Ordinal), $"the output contains '{expected}' (it is:\n{Output})");

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (_offset >= _current.Length)
        {
            if (!await _input.Reader.WaitToReadAsync(cancellationToken))
                return 0;
            if (_input.Reader.TryRead(out var next))
            {
                _current = next;
                _offset = 0;
            }
        }

        var count = Math.Min(buffer.Length, _current.Length - _offset);
        _current.AsMemory(_offset, count).CopyTo(buffer);
        _offset += count;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (FailWrites)
            throw new IOException("Broken pipe");
        lock (_output)
            _output.Write(buffer, offset, count);
    }

    public override void Flush()
    {
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        _input.Writer.TryComplete();
        base.Dispose(disposing);
    }
}
