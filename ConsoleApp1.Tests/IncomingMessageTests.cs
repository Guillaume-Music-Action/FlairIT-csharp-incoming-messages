using System.Text.Json;
using ConsoleApp1;

namespace ConsoleApp1.Tests;

public class IncomingMessageTests
{
    private static Dictionary<string, object?> ValidRaw() => new()
    {
        ["id"] = "abc-123",
        ["timestamp"] = "2023-01-01T12:00:00Z",
        ["payload"] = JsonSerializer.Deserialize<JsonElement>("""{"foo":"bar"}""")
    };

    [Fact]
    public void Parse_WithValidMessage_ReturnsIncomingMessage()
    {
        var result = IncomingMessage.Parse(ValidRaw());

        Assert.Equal("abc-123", result.Id);
        Assert.Equal(new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero), result.Timestamp);
        Assert.Equal(JsonValueKind.Object, result.Payload.ValueKind);
    }

    [Fact]
    public void Parse_AcceptsPlainStringId()
    {
        var raw = ValidRaw();
        raw["id"] = "plain-string";

        var result = IncomingMessage.Parse(raw);

        Assert.Equal("plain-string", result.Id);
    }

    [Fact]
    public void Parse_AcceptsJsonElementStringId()
    {
        var raw = ValidRaw();
        raw["id"] = JsonSerializer.Deserialize<JsonElement>("\"json-element-id\"");

        var result = IncomingMessage.Parse(raw);

        Assert.Equal("json-element-id", result.Id);
    }

    [Fact]
    public void Parse_MissingId_ThrowsArgumentException()
    {
        var raw = ValidRaw();
        raw.Remove("id");

        var ex = Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public void Parse_NullId_ThrowsArgumentException()
    {
        var raw = ValidRaw();
        raw["id"] = null;

        Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
    }

    [Fact]
    public void Parse_IdWithWrongType_ThrowsArgumentException()
    {
        var raw = ValidRaw();
        raw["id"] = 123;

        Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
    }

    [Fact]
    public void Parse_MissingTimestamp_ThrowsArgumentException()
    {
        var raw = ValidRaw();
        raw.Remove("timestamp");

        Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
    }

    [Fact]
    public void Parse_TimestampWithWrongType_ThrowsArgumentException()
    {
        var raw = ValidRaw();
        raw["timestamp"] = 42;

        Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
    }

    [Fact]
    public void Parse_UnparsableTimestamp_ThrowsFormatException()
    {
        var raw = ValidRaw();
        raw["timestamp"] = "not-a-date";

        Assert.Throws<FormatException>(() => IncomingMessage.Parse(raw));
    }

    [Fact]
    public void Parse_TimestampWithoutTimezone_IsAssumedUtc()
    {
        var raw = ValidRaw();
        raw["timestamp"] = "2023-06-15T08:30:00";

        var result = IncomingMessage.Parse(raw);

        Assert.Equal(new DateTimeOffset(2023, 6, 15, 8, 30, 0, TimeSpan.Zero), result.Timestamp);
    }

    [Fact]
    public void Parse_TimestampWithOffset_IsConvertedToUtc()
    {
        var raw = ValidRaw();
        raw["timestamp"] = "2023-01-01T12:00:00+02:00";

        var result = IncomingMessage.Parse(raw);

        Assert.Equal(new DateTimeOffset(2023, 1, 1, 10, 0, 0, TimeSpan.Zero), result.Timestamp);
    }

    [Fact]
    public void Parse_MissingPayload_ThrowsArgumentException()
    {
        var raw = ValidRaw();
        raw.Remove("payload");

        Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
    }

    [Fact]
    public void Parse_PayloadNotAnObject_ThrowsArgumentException()
    {
        var raw = ValidRaw();
        raw["payload"] = JsonSerializer.Deserialize<JsonElement>("\"not-an-object\"");

        Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
    }

    [Fact]
    public void Parse_PayloadAsPlainDictionary_ThrowsArgumentException()
    {
        // Un IDictionary "brut" (pas un JsonElement) n'est pas accepté : après désérialisation
        // System.Text.Json, un objet imbriqué est toujours un JsonElement.
        var raw = ValidRaw();
        raw["payload"] = new Dictionary<string, object?> { ["a"] = 1 };

        Assert.Throws<ArgumentException>(() => IncomingMessage.Parse(raw));
    }
}
