using System.Text.Json;
using ConsoleApp1;

namespace ConsoleApp1.Tests;

public class ValidateAndNormalizeTests
{
    private static Dictionary<string, object?> ValidMessage() => new()
    {
        ["id"] = "abc-123",
        ["timestamp"] = "2023-01-01T12:00:00+02:00",
        ["payload"] = JsonSerializer.Deserialize<JsonElement>("""{"foo":"bar"}""")
    };

    [Fact]
    public void ValidateAndNormalize_UppercasesId()
    {
        var result = Program.ValidateAndNormalize(ValidMessage());

        Assert.Equal("ABC-123", result["id"]);
    }

    [Fact]
    public void ValidateAndNormalize_NormalizesTimestampToUtcWithZ()
    {
        var result = Program.ValidateAndNormalize(ValidMessage());

        Assert.Equal("2023-01-01T10:00:00Z", result["timestamp"]);
    }

    [Fact]
    public void ValidateAndNormalize_AddsReceivedAtAsRecentUtcTimestamp()
    {
        var before = DateTime.UtcNow;

        var result = Program.ValidateAndNormalize(ValidMessage());

        Assert.True(result.ContainsKey("received_at"));
        var receivedAt = DateTime.ParseExact(
            (string)result["received_at"]!,
            "yyyy-MM-ddTHH:mm:ssZ",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

        Assert.InRange(receivedAt, before.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public void ValidateAndNormalize_PreservesPayload()
    {
        var result = Program.ValidateAndNormalize(ValidMessage());

        var payload = Assert.IsType<JsonElement>(result["payload"]);
        Assert.Equal("bar", payload.GetProperty("foo").GetString());
    }

    [Fact]
    public void ValidateAndNormalize_ReturnsNewDictionary_DoesNotMutateInput()
    {
        var input = ValidMessage();

        var result = Program.ValidateAndNormalize(input);

        Assert.NotSame(input, result);
        Assert.Equal("abc-123", input["id"]);
    }

    [Fact]
    public void ValidateAndNormalize_MissingId_ThrowsArgumentException()
    {
        var message = ValidMessage();
        message.Remove("id");

        Assert.Throws<ArgumentException>(() => Program.ValidateAndNormalize(message));
    }

    [Fact]
    public void ValidateAndNormalize_MissingTimestamp_ThrowsArgumentException()
    {
        var message = ValidMessage();
        message.Remove("timestamp");

        Assert.Throws<ArgumentException>(() => Program.ValidateAndNormalize(message));
    }

    [Fact]
    public void ValidateAndNormalize_InvalidTimestamp_ThrowsFormatException()
    {
        var message = ValidMessage();
        message["timestamp"] = "not-a-date";

        Assert.Throws<FormatException>(() => Program.ValidateAndNormalize(message));
    }

    [Fact]
    public void ValidateAndNormalize_MissingPayload_ThrowsArgumentException()
    {
        var message = ValidMessage();
        message.Remove("payload");

        Assert.Throws<ArgumentException>(() => Program.ValidateAndNormalize(message));
    }

    [Fact]
    public void ValidateAndNormalize_PayloadNotAnObject_ThrowsArgumentException()
    {
        var message = ValidMessage();
        message["payload"] = JsonSerializer.Deserialize<JsonElement>("42");

        Assert.Throws<ArgumentException>(() => Program.ValidateAndNormalize(message));
    }

    [Fact]
    public void ValidateAndNormalize_PreservesExtraFieldsFromInput()
    {
        var message = ValidMessage();
        message["extra_field"] = "kept-as-is";

        var result = Program.ValidateAndNormalize(message);

        Assert.Equal("kept-as-is", result["extra_field"]);
    }
}
