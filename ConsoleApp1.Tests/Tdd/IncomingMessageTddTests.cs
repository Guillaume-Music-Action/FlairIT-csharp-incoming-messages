using System.Text.Json;
using ConsoleApp1.Tests.Tooling;
using ConsoleApp1.Tdd;
using Xunit;
using AwesomeAssertions;

namespace ConsoleApp1.Tests.Tdd;

public class IncomingMessageTddTests
{
    [Fact]
    public void Parse_ValidMessage_ReturnsIncomingMessageWithCorrectValues()
    {
        IncomingMessageScenario.Given()
            .WithId("abc-123")
            .WithTimestamp("2023-01-01T12:00:00+02:00")
            .WithValidPayload()
            .Should()
            .WithId("abc-123")
            .WithTimestamp(new DateTimeOffset(2023, 1, 1, 10, 0, 0, TimeSpan.Zero))
            .WithPayload(JsonValueKind.Object)
            .WithPayloadContaining("foo", "bar");
    }

    [Fact]
    public void Parse_MissingId_ThrowsArgumentException()
    {
        IncomingMessageScenario.Given()
            .WithoutId()
            .WithTimestamp("2023-01-01T12:00:00+02:00")
            .WithValidPayload()
            .ShouldThrow()
            .ArgumentException("id");
    }

    [Fact]
    public void Parse_MissingTimestamp_ThrowsArgumentException()
    {
        IncomingMessageScenario.Given()
            .WithId("abc-123")
            .WithoutTimestamp()
            .WithValidPayload()
            .ShouldThrow()
            .ArgumentException("timestamp");
    }

    [Fact]
    public void Parse_MissingPayload_ThrowsArgumentException()
    {
        IncomingMessageScenario.Given()
            .WithId("abc-123")
            .WithTimestamp("2023-01-01T12:00:00+02:00")
            .WithoutPayload()
            .ShouldThrow()
            .ArgumentException("payload");
    }

    [Fact]
    public void Parse_InvalidTimestamp_ThrowsFormatException()
    {
        IncomingMessageScenario.Given()
            .WithId("abc-123")
            .WithTimestamp("not-a-valid-timestamp")
            .WithValidPayload()
            .ShouldThrow()
            .FormatException("not-a-valid-timestamp");
    }

    [Fact]
    public void Parse_WrongIdType_ThrowsArgumentException()
    {
        IncomingMessageScenario.Given()
            .WithId(123)
            .WithTimestamp("2023-01-01T12:00:00+02:00")
            .WithValidPayload()
            .ShouldThrow()
            .ArgumentException("id");
    }

    [Fact]
    public void Parse_WrongTimestampType_ThrowsArgumentException()
    {
        IncomingMessageScenario.Given()
            .WithId("abc-123")
            .WithTimestamp(1234567890)
            .WithValidPayload()
            .ShouldThrow()
            .ArgumentException("timestamp");
    }

    [Fact]
    public void Parse_WrongPayloadType_ThrowsArgumentException()
    {
        IncomingMessageScenario.Given()
            .WithId("abc-123")
            .WithTimestamp("2023-01-01T12:00:00+02:00")
            .WithPayload(JsonSerializer.Deserialize<JsonElement>("\"not-an-object\""))
            .ShouldThrow()
            .ArgumentException("payload");
    }
}
