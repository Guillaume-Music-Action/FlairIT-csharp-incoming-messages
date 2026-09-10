using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using ConsoleApp1.Tdd;
using ConsoleApp1.Tests.Tooling;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;
using AwesomeAssertions;

namespace ConsoleApp1.Tests.Tdd;

/// <summary>
/// Property-based mirror of <see cref="IncomingMessageTddTests"/>.
///
/// FsCheck picks a generator by the *parameter type*, not by a parameter name: each
/// [Property] below declares <c>Arbitrary = new[] { typeof(Arbitraries) }</c>, and FsCheck
/// matches each parameter to the <see cref="Arbitrary{T}"/>-returning member of that class
/// whose <c>T</c> equals the parameter type. Wrapper structs (<see cref="ValidId"/> …) give
/// each "kind" of value its own type so several <c>string</c>/<c>object?</c> generators can
/// coexist in one signature.
/// </summary>
public class IncomingMessagePbtTests
{
    // --- Wrapper types: one per generated "kind" so FsCheck can tell them apart ---

    public readonly record struct ValidId(string Value);
    public readonly record struct ValidTimestamp(string Value);
    public readonly record struct ValidPayload(JsonElement Value);
    public readonly record struct InvalidId(object? Value);
    public readonly record struct InvalidTimestampType(object? Value);
    public readonly record struct InvalidPayload(object? Value);

    // --- Generators, exposed as Arbitrary<T> keyed by the wrapper type ---

    public static class Arbitraries
    {
        private static readonly JsonElement[] ObjectPayloads =
        {
            JsonSerializer.Deserialize<JsonElement>("""{"a":1}"""),
            JsonSerializer.Deserialize<JsonElement>("""{"b":"x"}"""),
            JsonSerializer.Deserialize<JsonElement>("""{"nested":{"c":true}}"""),
        };

        private static readonly JsonElement[] NonObjectPayloads =
        {
            JsonSerializer.Deserialize<JsonElement>("\"string\""),
            JsonSerializer.Deserialize<JsonElement>("123"),
            JsonSerializer.Deserialize<JsonElement>("true"),
            JsonSerializer.Deserialize<JsonElement>("[]"),
        };

        public static Arbitrary<ValidId> ValidId() =>
            ArbMap.Default.ArbFor<string>()
                .Generator
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new ValidId(s))
                .ToArbitrary();

        public static Arbitrary<ValidTimestamp> ValidTimestamp() =>
            // Seconds since the Unix epoch, up to ~10 years past 2020, formatted as ISO8601 'Z'.
            Gen.Choose(0, 1_600_000_000)
                .Select(secs => DateTimeOffset.UnixEpoch.AddSeconds(secs))
                .Select(dt => new ValidTimestamp(
                    dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))
                .ToArbitrary();

        public static Arbitrary<ValidPayload> ValidPayload() =>
            Gen.Elements(ObjectPayloads)
                .Select(e => new ValidPayload(e))
                .ToArbitrary();

        public static Arbitrary<InvalidId> InvalidId() =>
            Gen.Elements<object?>(123, 12.5, true, new List<int>())
                .Select(o => new InvalidId(o))
                .ToArbitrary();

        public static Arbitrary<InvalidTimestampType> InvalidTimestampType() =>
            Gen.Elements<object?>(1234567890L, 12.5, true, new List<int>())
                .Select(o => new InvalidTimestampType(o))
                .ToArbitrary();

        public static Arbitrary<InvalidPayload> InvalidPayload() =>
            Gen.OneOf(
                    Gen.Elements(NonObjectPayloads).Select(e => (object?)e),
                    Gen.Elements<object?>("not-a-jsonelement", 42))
                .Select(o => new InvalidPayload(o))
                .ToArbitrary();
    }

    // --- Properties (one per IncomingMessageTddTests fact) ---

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_ValidInputs_RoundtripCorrectly(ValidId id, ValidTimestamp timestamp, ValidPayload payload)
    {
        var result = IncomingMessageScenario.Given()
            .WithId(id.Value)
            .WithTimestamp(timestamp.Value)
            .WithPayload(payload.Value)
            .Parse();

        result.Id.Should().Be(id.Value);
        result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            .Should().Be(timestamp.Value);
        result.Payload.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_MissingId_ThrowsArgumentException(ValidTimestamp timestamp, ValidPayload payload)
    {
        IncomingMessageScenario.Given()
            .WithoutId()
            .WithTimestamp(timestamp.Value)
            .WithPayload(payload.Value)
            .Parsing()
            .Should().Throw<ArgumentException>().WithMessage("*id*");
    }

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_MissingTimestamp_ThrowsArgumentException(ValidId id, ValidPayload payload)
    {
        IncomingMessageScenario.Given()
            .WithId(id.Value)
            .WithoutTimestamp()
            .WithPayload(payload.Value)
            .Parsing()
            .Should().Throw<ArgumentException>().WithMessage("*timestamp*");
    }

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_MissingPayload_ThrowsArgumentException(ValidId id, ValidTimestamp timestamp)
    {
        IncomingMessageScenario.Given()
            .WithId(id.Value)
            .WithTimestamp(timestamp.Value)
            .WithoutPayload()
            .Parsing()
            .Should().Throw<ArgumentException>().WithMessage("*payload*");
    }

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_InvalidTimestamp_ThrowsFormatException(ValidId id, ValidPayload payload)
    {
        IncomingMessageScenario.Given()
            .WithId(id.Value)
            .WithTimestamp("not-a-timestamp")
            .WithPayload(payload.Value)
            .Parsing()
            .Should().Throw<FormatException>().WithMessage("*not-a-timestamp*");
    }

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_WrongIdType_ThrowsArgumentException(InvalidId badId, ValidTimestamp timestamp, ValidPayload payload)
    {
        IncomingMessageScenario.Given()
            .WithId(badId.Value)
            .WithTimestamp(timestamp.Value)
            .WithPayload(payload.Value)
            .Parsing()
            .Should().Throw<ArgumentException>().WithMessage("*id*");
    }

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_WrongTimestampType_ThrowsArgumentException(ValidId id, InvalidTimestampType badTs, ValidPayload payload)
    {
        IncomingMessageScenario.Given()
            .WithId(id.Value)
            .WithTimestamp(badTs.Value)
            .WithPayload(payload.Value)
            .Parsing()
            .Should().Throw<ArgumentException>().WithMessage("*timestamp*");
    }

    [Property(Arbitrary = new[] { typeof(Arbitraries) })]
    public void Parse_WrongPayloadType_ThrowsArgumentException(ValidId id, ValidTimestamp timestamp, InvalidPayload badPayload)
    {
        IncomingMessageScenario.Given()
            .WithId(id.Value)
            .WithTimestamp(timestamp.Value)
            .WithPayload(badPayload.Value)
            .Parsing()
            .Should().Throw<ArgumentException>().WithMessage("*payload*");
    }
}
