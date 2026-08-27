using System;
using System.Collections.Generic;
using System.Text.Json;
using ConsoleApp1.Tdd;
using ConsoleApp1.Tests.Tooling;
using FsCheck;
using FsCheck.Xunit;
using Xunit;
using AwesomeAssertions;

namespace ConsoleApp1.Tests.Tdd;

public class IncomingMessagePbtTests
{
    // --- Generators ---

    private static Gen<string> NonEmptyStringGen =>
        Arb.Generate<string>()
            .Where(s => !string.IsNullOrWhiteSpace(s));

    private static Gen<string> ValidTimestampGen =>
        Gen.Choose(DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow.AddYears(10))
            .Select(dt => dt.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));

    private static Gen<JsonElement> ValidPayloadGen =>
        Gen.Elements(
                JsonSerializer.Deserialize<JsonElement>("""{"a":1}"""),
                JsonSerializer.Deserialize<JsonElement>("""{"b":"x"}"""),
                JsonSerializer.Deserialize<JsonElement>("""{"nested":{"c":true}}""")
            );

    private static Gen<object?> InvalidIdGen =>
        Gen.Elements(123, 12.5, true, new List<int>(), null);

    private static Gen<object?> InvalidTimestampGen =>
        Gen.Elements(1234567890L, 12.5, true, new List<int>(), null);

    private static Gen<object?> InvalidPayloadGen =>
        Gen.Elements(
            JsonSerializer.Deserialize<JsonElement>("\"string\""),
            JsonSerializer.Deserialize<JsonElement>("123"),
            JsonSerializer.Deserialize<JsonElement>("true"),
            JsonSerializer.Deserialize<JsonElement>("[]"),
            "not-a-jsonelement",
            42,
            null
        );

    // --- Properties ---

    [Property]
    public Property Parse_ValidInputs_RoundtripsCorrectly(
        NonEmptyStringGen id, ValidTimestampGen timestamp, ValidPayloadGen payload)
    {
        var raw = IncomingMessageScenario.Given()
            .WithId(id)
            .WithTimestamp(timestamp)
            .WithPayload(payload);

        var result = raw.Parse();

        return (result.Id == id
            && result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) == timestamp
            && result.Payload.ValueKind == JsonValueKind.Object)
            .ToProperty()
            .Label($"id={id}, timestamp={timestamp}, payloadKind={payload.ValueKind}");
    }

    [Property]
    public Property Parse_MissingId_ThrowsArgumentException(
        ValidTimestampGen timestamp, ValidPayloadGen payload)
    {
        var act = IncomingMessageScenario.Given()
            .WithoutId()
            .WithTimestamp(timestamp)
            .WithPayload(payload)
            .Parsing();

        return Prop.ForAll(_ => 
        {
            try { act(); return false; }
            catch (ArgumentException ex) when (ex.Message.Contains("id")) { return true; }
            catch { return false; }
        }).Label("missing id");
    }

    [Property]
    public Property Parse_MissingTimestamp_ThrowsArgumentException(
        NonEmptyStringGen id, ValidPayloadGen payload)
    {
        var act = IncomingMessageScenario.Given()
            .WithId(id)
            .WithoutTimestamp()
            .WithPayload(payload)
            .Parsing();

        return Prop.ForAll(_ => 
        {
            try { act(); return false; }
            catch (ArgumentException ex) when (ex.Message.Contains("timestamp")) { return true; }
            catch { return false; }
        }).Label("missing timestamp");
    }

    [Property]
    public Property Parse_MissingPayload_ThrowsArgumentException(
        NonEmptyStringGen id, ValidTimestampGen timestamp)
    {
        var act = IncomingMessageScenario.Given()
            .WithId(id)
            .WithTimestamp(timestamp)
            .WithoutPayload()
            .Parsing();

        return Prop.ForAll(_ => 
        {
            try { act(); return false; }
            catch (ArgumentException ex) when (ex.Message.Contains("payload")) { return true; }
            catch { return false; }
        }).Label("missing payload");
    }

    [Property]
    public Property Parse_InvalidTimestamp_ThrowsFormatException(
        NonEmptyStringGen id, ValidPayloadGen payload)
    {
        var act = IncomingMessageScenario.Given()
            .WithId(id)
            .WithTimestamp("not-a-timestamp")
            .WithPayload(payload)
            .Parsing();

        return Prop.ForAll(_ => 
        {
            try { act(); return false; }
            catch (FormatException ex) when (ex.Message.Contains("not-a-timestamp")) { return true; }
            catch { return false; }
        }).Label("invalid timestamp");
    }

    [Property]
    public Property Parse_WrongIdType_ThrowsArgumentException(
        InvalidIdGen badId, ValidTimestampGen timestamp, ValidPayloadGen payload)
    {
        var act = IncomingMessageScenario.Given()
            .WithId(badId)
            .WithTimestamp(timestamp)
            .WithPayload(payload)
            .Parsing();

        return Prop.ForAll(_ => 
        {
            try { act(); return false; }
            catch (ArgumentException ex) when (ex.Message.Contains("id")) { return true; }
            catch { return false; }
        }).Label($"bad id type");
    }

    [Property]
    public Property Parse_WrongTimestampType_ThrowsArgumentException(
        NonEmptyStringGen id, InvalidTimestampGen badTs, ValidPayloadGen payload)
    {
        var act = IncomingMessageScenario.Given()
            .WithId(id)
            .WithTimestamp(badTs)
            .WithPayload(payload)
            .Parsing();

        return Prop.ForAll(_ => 
        {
            try { act(); return false; }
            catch (ArgumentException ex) when (ex.Message.Contains("timestamp")) { return true; }
            catch { return false; }
        }).Label($"bad timestamp type");
    }

    [Property]
    public Property Parse_WrongPayloadType_ThrowsArgumentException(
        NonEmptyStringGen id, ValidTimestampGen timestamp, InvalidPayloadGen badPayload)
    {
        var act = IncomingMessageScenario.Given()
            .WithId(id)
            .WithTimestamp(timestamp)
            .WithPayload(badPayload)
            .Parsing();

        return Prop.ForAll(_ => 
        {
            try { act(); return false; }
            catch (ArgumentException ex) when (ex.Message.Contains("payload")) { return true; }
            catch { return false; }
        }).Label($"bad payload type");
    }
}
