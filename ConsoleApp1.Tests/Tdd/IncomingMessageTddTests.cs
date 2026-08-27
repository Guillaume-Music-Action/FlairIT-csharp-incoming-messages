using System;
using System.Collections.Generic;
using System.Text.Json;
using ConsoleApp1.Tdd;
using Xunit;
using AwesomeAssertions;

namespace ConsoleApp1.Tests.Tdd;

public class IncomingMessageTddTests
{
    [Fact]
    public void Parse_ValidMessage_ReturnsIncomingMessageWithCorrectValues()
    {
        // Arrange
        var raw = new Dictionary<string, object?>
        {
            ["id"] = "abc-123",
            ["timestamp"] = "2023-01-01T12:00:00+02:00",
            ["payload"] = JsonSerializer.Deserialize<JsonElement>("""{"foo":"bar"}""")
        };

        // Act
        var result = IncomingMessageTdd.Parse(raw);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("abc-123");
        result.Timestamp.Should().Be(new DateTimeOffset(2023, 1, 1, 10, 0, 0, TimeSpan.Zero));
        result.Payload.ValueKind.Should().Be(JsonValueKind.Object);
    }
}
