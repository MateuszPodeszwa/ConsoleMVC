using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests.Remote;

/// <summary>Tests for <see cref="CrLfWriter"/>.</summary>
public class CrLfWriterTests
{
    private static (CrLfWriter Writer, StringWriter Inner) Create()
    {
        var inner = new StringWriter();
        return (new CrLfWriter(inner), inner);
    }

    [Fact]
    public void LoneLineFeeds_BecomeCrLf()
    {
        var (writer, inner) = Create();

        writer.Write("one\ntwo\n\nthree");

        Assert.Equal("one\r\ntwo\r\n\r\nthree", inner.ToString());
    }

    [Fact]
    public void CrLf_IsNotDoubled_EvenAcrossWrites()
    {
        var (writer, inner) = Create();

        writer.Write("a\r\nb\r");
        writer.Write('\n');
        writer.WriteLine("c");
        writer.WriteLine();

        Assert.Equal("a\r\nb\r\nc\r\n\r\n", inner.ToString());
    }

    [Fact]
    public void ABrokenConnection_IsReported_AndTheErrorKept()
    {
        var broken = 0;
        var writer = new CrLfWriter(new FailingWriter(), () => broken++);

        // The I/O error stays an I/O error, so code that already handles a lost terminal keeps handling it
        Assert.Throws<IOException>(() => writer.Write("x"));
        Assert.Throws<IOException>(() => writer.Flush());
        Assert.Equal(2, broken);
    }

    private sealed class FailingWriter : TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        public override void Write(char value) => throw new IOException("Broken pipe");
        public override void Write(ReadOnlySpan<char> buffer) => throw new IOException("Broken pipe");
        public override void Flush() => throw new IOException("Broken pipe");
    }
}
