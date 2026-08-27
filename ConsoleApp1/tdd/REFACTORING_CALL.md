# Call to Refactoring – `IncomingMessageTdd.Parse`

> Written after **Rule 3 Green** (missing timestamp).  
> All 28 tests pass.  
> Next: implement Rule 4 (missing payload), then refactor before Rule 5.

---

## Identified Smells

| # | Smell | Location | Impact |
|---|-------|----------|--------|
| 1 | **Primitive obsession** | `Dictionary<string,object?>` parameter | No compile-time safety; callers must know magic keys. |
| 2 | **Duplicated validation pattern** | Lines 11‑12 vs 14‑15 | Same `TryGetValue + pattern match` repeated → maintenance burden. |
| 3 | **Magic strings** | `"id"`, `"timestamp"`, `"payload"` scattered | Typos only caught at runtime; hard to rename. |
| 4 | **Missing payload validation** | Line 17 | Direct cast throws `KeyNotFoundException` / `InvalidCastException` instead of our `ArgumentException`. |
| 5 | **Ignored `TryParse` result** | Lines 19‑20 | Malformed timestamp silently yields `default(DateTimeOffset)` → should throw `FormatException`. |
| 6 | **Inconsistent error messages** | Lines 12, 15 | Slightly different wording; not parameterized. |
| 7 | **Single method doing 4 things** | Whole method | Validate → extract → parse → construct. Harder to test in isolation. |
| 8 | **No reusable `ExtractString` helper** | — | Pattern repeated; original `IncomingMessage` had a private `ExtractString`. |

---

## Proposed Refactor (after Rule 4 Green)

Introduce tiny private helpers:

```csharp
private static string ExtractString(Dictionary<string,object?> dict, string key, string fieldName)
{
    if (!dict.TryGetValue(key, out var v) || v is not string s)
        throw new ArgumentException($"Missing or invalid '{fieldName}' field.");
    return s;
}

private static JsonElement ExtractPayload(Dictionary<string,object?> dict)
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
```

Then `Parse` shrinks to:

```csharp
public static IncomingMessageTdd Parse(Dictionary<string, object?> raw)
{
    var id = ExtractString(raw, "id", "id");
    var timestampRaw = ExtractString(raw, "timestamp", "timestamp");
    var payload = ExtractPayload(raw);
    var timestamp = ParseTimestamp(timestampRaw);
    return new IncomingMessageTdd(id, timestamp, payload);
}
```

Benefits:
- **DRY** – validation logic in one place.
- **Single responsibility** – each helper does one thing.
- **Testable** – helpers can be unit-tested independently (or stay private, covered by existing tests).
- **Consistent errors** – parameterized messages.
- **Fail-fast** – malformed timestamp now throws `FormatException`.

---

## Next Steps

1. **Rule 4** – Add spec, write failing test for missing payload, make Green.
2. **Refactor** – Apply the helpers above, run full suite (must stay Green).
3. **Rule 5** – Invalid timestamp → `FormatException`.
4. Continue Rules 6‑9 (wrong types) with same cycle.

---

*Commit after each Green + Refactor cycle.*
