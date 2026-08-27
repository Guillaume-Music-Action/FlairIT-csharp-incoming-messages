using System;
using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using ConsoleApp1.Tdd;

namespace ConsoleApp1.Tests.Tooling;

/// <summary>
/// Fluent DSL for testing IncomingMessageTdd.Parse — reads like the spec sentences.
/// </summary>
public sealed class IncomingMessageScenario
{
    private readonly Dictionary<string, object?> _raw = new();

    private IncomingMessageScenario() { }

    public static IncomingMessageScenario Given() => new();

    public IncomingMessageScenario WithId(string id)
    {
        _raw["id"] = id;
        return this;
    }

    public IncomingMessageScenario WithId(object? id)
    {
        _raw["id"] = id;
        return this;
    }

    public IncomingMessageScenario WithoutId()
    {
        _raw.Remove("id");
        return this;
    }

    public IncomingMessageScenario WithTimestamp(string timestamp)
    {
        _raw["timestamp"] = timestamp;
        return this;
    }

    public IncomingMessageScenario WithTimestamp(object? timestamp)
    {
        _raw["timestamp"] = timestamp;
        return this;
    }

    public IncomingMessageScenario WithoutTimestamp()
    {
        _raw.Remove("timestamp");
        return this;
    }

    public IncomingMessageScenario WithPayload(JsonElement payload)
    {
        _raw["payload"] = payload;
        return this;
    }

    public IncomingMessageScenario WithPayload(object? payload)
    {
        _raw["payload"] = payload;
        return this;
    }

    public IncomingMessageScenario WithoutPayload()
    {
        _raw.Remove("payload");
        return this;
    }

    public IncomingMessageScenario WithValidPayload()
    {
        _raw["payload"] = JsonSerializer.Deserialize<JsonElement>("""{"foo":"bar"}""");
        return this;
    }

    // --- When / Then ---

    public IncomingMessageTdd Parse()
    {
        return IncomingMessageTdd.Parse(_raw);
    }

    public Action Parsing() => () => IncomingMessageTdd.Parse(_raw);

    // --- Assertions ---

    public IncomingMessageAssertions Should() => new(Parse());

    public ExceptionAssertions ShouldThrow() => new(Parsing());
}

/// <summary>
/// Assertions for successful parse results.
/// </summary>
public sealed class IncomingMessageAssertions
{
    private readonly IncomingMessageTdd _msg;

    internal IncomingMessageAssertions(IncomingMessageTdd msg) => _msg = msg;

    public IncomingMessageAssertions WithId(string expectedId)
    {
        _msg.Id.Should().Be(expectedId);
        return this;
    }

    public IncomingMessageAssertions WithTimestamp(DateTimeOffset expected)
    {
        _msg.Timestamp.Should().Be(expected);
        return this;
    }

    public IncomingMessageAssertions WithPayload(JsonValueKind expectedKind)
    {
        _msg.Payload.ValueKind.Should().Be(expectedKind);
        return this;
    }

    public IncomingMessageAssertions WithPayloadContaining(string propertyName, string expectedValue)
    {
        _msg.Payload.GetProperty(propertyName).GetString().Should().Be(expectedValue);
        return this;
    }
}

/// <summary>
/// Assertions for exception-throwing scenarios.
/// </summary>
public sealed class ExceptionAssertions
{
    private readonly Action _act;

    internal ExceptionAssertions(Action act) => _act = act;

    public AndConstraint<ExceptionAssertions> ArgumentException(string mentioning)
    {
        _act.Should().Throw<ArgumentException>().WithMessage($"*{mentioning}*");
        return new AndConstraint<ExceptionAssertions>(this);
    }

    public AndConstraint<ExceptionAssertions> FormatException(string mentioning)
    {
        _act.Should().Throw<FormatException>().WithMessage($"*{mentioning}*");
        return new AndConstraint<ExceptionAssertions>(this);
    }
}
