using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using ConsoleApp1.Tdd;
using ConsoleApp1.Tests.Tooling;
using FsCheck;
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
public partial class IncomingMessagePbtTests
{
    // --- Wrapper types: one per generated "kind" so FsCheck can tell them apart ---

    public readonly record struct ValidId(string Value);
    public readonly record struct ValidTimestamp(string Value);

    /// <summary>
    /// A JSON object payload. <see cref="Model"/> is the shrinkable source of truth
    /// (a plain <c>Dictionary&lt;string, object?&gt;</c> FsCheck knows how to minimise);
    /// <see cref="Value"/> is that same map serialised to a <see cref="JsonElement"/>
    /// for handing to <c>Parse</c>.
    /// </summary>
    public readonly record struct ValidPayload(Dictionary<string, object?> Model)
    {
        public JsonElement Value =>
            JsonSerializer.SerializeToElement(Model);
    }

    public readonly record struct InvalidId(object? Value);
    public readonly record struct InvalidTimestampType(object? Value);
    public readonly record struct InvalidPayload(object? Value);



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
        // Payload content survives the round trip, not just its kind.
        foreach (var (key, value) in payload.Model)
            result.Payload.GetProperty(key).GetInt32().Should().Be((int)value!);
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
