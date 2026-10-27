using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for the styling primitives: <see cref="Colour"/>, <see cref="Length"/> and <see cref="TextStyle"/>.
/// </summary>
public class StylingTests
{
    // --- Colour ---

    [Theory]
    [InlineData("red", ConsoleColor.Red)]
    [InlineData("RED", ConsoleColor.Red)]
    [InlineData("darkred", ConsoleColor.DarkRed)]
    [InlineData("grey", ConsoleColor.Gray)]
    [InlineData("gray", ConsoleColor.Gray)]
    [InlineData("darkgrey", ConsoleColor.DarkGray)]
    [InlineData("dark-grey", ConsoleColor.DarkGray)]
    [InlineData("cyan", ConsoleColor.Cyan)]
    public void Colour_Parse_NamedColours(string text, ConsoleColor expected)
    {
        var colour = Colour.Parse(text);

        Assert.Equal(ColourKind.Console, colour.Kind);
        Assert.Equal(expected, colour.ConsoleColour);
    }

    [Theory]
    [InlineData("#ff8800", 0xff, 0x88, 0x00)]
    [InlineData("#F80", 0xff, 0x88, 0x00)]
    [InlineData("#000000", 0, 0, 0)]
    public void Colour_Parse_HexColours(string text, byte r, byte g, byte b)
    {
        var colour = Colour.Parse(text);

        Assert.Equal(ColourKind.Rgb, colour.Kind);
        Assert.Equal((r, g, b), (colour.R, colour.G, colour.B));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("")]
    public void Colour_Parse_DefaultColour(string text)
    {
        Assert.Equal(Colour.Default, Colour.Parse(text));
    }

    [Theory]
    [InlineData("purple-ish")]
    [InlineData("#12")]
    [InlineData("#gggggg")]
    [InlineData("#1234567")]
    public void Colour_TryParse_RejectsInvalidValues(string text)
    {
        Assert.False(Colour.TryParse(text, out _));
        Assert.Throws<FormatException>(() => Colour.Parse(text));
    }

    [Fact]
    public void Colour_ImplicitConversions_Work()
    {
        Colour fromEnum = ConsoleColor.Yellow;
        Colour fromString = "yellow";

        Assert.Equal(fromEnum, fromString);
        Assert.Equal(Colour.Yellow, fromEnum);
    }

    // --- Length ---

    [Theory]
    [InlineData("auto", LengthKind.Auto, 0)]
    [InlineData("40", LengthKind.Fixed, 40)]
    [InlineData(" 40 ", LengthKind.Fixed, 40)]
    [InlineData("50%", LengthKind.Percent, 50)]
    public void Length_Parse_ValidValues(string text, LengthKind kind, int value)
    {
        var length = Length.Parse(text);

        Assert.Equal(kind, length.Kind);
        Assert.Equal(value, length.Value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("101%")]
    [InlineData("wide")]
    [InlineData("")]
    public void Length_TryParse_RejectsInvalidValues(string text)
    {
        Assert.False(Length.TryParse(text, out _));
    }

    [Theory]
    [InlineData("auto", 80, 30, 30)]
    [InlineData("auto", 20, 30, 20)]
    [InlineData("40", 80, 10, 40)]
    [InlineData("40", 30, 10, 30)]
    [InlineData("50%", 80, 10, 40)]
    public void Length_Resolve_ClampsToAvailableWidth(string text, int available, int natural, int expected)
    {
        Assert.Equal(expected, Length.Parse(text).Resolve(available, natural));
    }

    // --- TextStyle ---

    [Fact]
    public void TextStyle_Plain_HasNoAttributes()
    {
        Assert.True(TextStyle.Plain.IsPlain);
        Assert.False(TextStyle.Plain.WithDecoration(TextDecoration.Bold).IsPlain);
        Assert.False(TextStyle.Plain.WithForeground(Colour.Red).IsPlain);
    }
}
