using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ConsoleApp1.Tdd;

public sealed record IncomingMessageTdd(string Id, DateTimeOffset Timestamp, JsonElement Payload)
{
    public static IncomingMessageTdd Parse(Dictionary<string, object?> raw)
    {
        var id = (string)raw["id"]!;
        var timestampRaw = (string)raw["timestamp"]!;
        var payloadElement = (JsonElement)raw["payload"]!;

        DateTimeOffset.TryParse(timestampRaw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedTimestamp);

        return new IncomingMessageTdd(id, parsedTimestamp, payloadElement);
    }
}
