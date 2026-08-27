using System.Collections.Generic;
using System.Text.Json;

namespace ConsoleApp1.Tdd;

public sealed record IncomingMessageTdd(string Id, DateTimeOffset Timestamp, JsonElement Payload)
{
    public static IncomingMessageTdd Parse(Dictionary<string, object?> raw)
    {
        throw new System.NotImplementedException();
    }
}
