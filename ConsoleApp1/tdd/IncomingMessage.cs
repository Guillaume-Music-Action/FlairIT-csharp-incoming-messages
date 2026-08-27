using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ConsoleApp1.Tdd;

public sealed record IncomingMessageTdd(string Id, DateTimeOffset Timestamp, JsonElement Payload)
{
    public static IncomingMessageTdd Parse(Dictionary<string, object?> raw)
    {
        if (!raw.TryGetValue("id", out var idObj) || idObj is not string id)
            throw new ArgumentException("Missing or invalid 'id' field.");

        if (!raw.TryGetValue("timestamp", out var tsObj) || tsObj is not string timestampRaw)
            throw new ArgumentException("Missing or invalid 'timestamp' field.");

        var payloadElement = (JsonElement)raw["payload"]!;

        DateTimeOffset.TryParse(timestampRaw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedTimestamp);

        return new IncomingMessageTdd(id, parsedTimestamp, payloadElement);
    }
}
