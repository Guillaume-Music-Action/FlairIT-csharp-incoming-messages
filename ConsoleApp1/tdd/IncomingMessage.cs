using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ConsoleApp1.Tdd;

public sealed record IncomingMessageTdd(string Id, DateTimeOffset Timestamp, JsonElement Payload)
{
    public static IncomingMessageTdd Parse(Dictionary<string, object?> raw)
    {
        var id = ExtractString(raw, "id", "id");
        var timestampRaw = ExtractString(raw, "timestamp", "timestamp");
        var payload = ExtractPayload(raw);
        var timestamp = ParseTimestamp(timestampRaw);
        return new IncomingMessageTdd(id, timestamp, payload);
    }

    private static string ExtractString(Dictionary<string, object?> dict, string key, string fieldName)
    {
        if (!dict.TryGetValue(key, out var v) || v is not string s)
            throw new ArgumentException($"Missing or invalid '{fieldName}' field.");
        return s;
    }

    private static JsonElement ExtractPayload(Dictionary<string, object?> dict)
    {
        if (!dict.TryGetValue("payload", out var v) || v is not JsonElement { ValueKind: JsonValueKind.Object } je)
            throw new ArgumentException("Missing or invalid 'payload' field.");

        return je;
    }

    private static DateTimeOffset ParseTimestamp(string raw)
    {
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            throw new FormatException($"Invalid timestamp '{raw}'.");
        return dt;
    }
}
