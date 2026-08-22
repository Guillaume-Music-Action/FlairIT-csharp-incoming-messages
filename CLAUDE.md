# ConsoleApp1

A small C# console utility that validates and normalizes incoming JSON messages before
ingestion.

## Structure

- `ConsoleApp1.slnx` — solution file (2 projects).
- `ConsoleApp1/` — the utility itself (`net10.0`, nullable enabled, `OutputType=Exe`).
  - `Program.cs` — everything lives here:
    - `IncomingMessage` (internal record) — a parsed, already-valid message
      (`Id: string`, `Timestamp: DateTimeOffset`, `Payload: JsonElement`).
      `IncomingMessage.Parse(Dictionary<string, object?>)` is the single place that
      validates the raw JSON shape (required fields `id`/`timestamp`/`payload`, correct
      types, timestamp parsing) and throws `ArgumentException` / `FormatException` on
      failure. Follows **parse, don't validate**: once an `IncomingMessage` exists, its
      type is proof of validity — nothing downstream re-checks it.
    - `Program.ValidateAndNormalize(Dictionary<string, object?>)` — public entry point.
      Delegates validation to `IncomingMessage.Parse`, then only normalizes: uppercases
      `id`, converts `timestamp` to UTC ISO8601 with `Z`, adds `received_at`.
    - `Program.Main()` — reads one JSON line from stdin, calls `ValidateAndNormalize`,
      writes normalized JSON to stdout. Errors go to stderr with a non-zero exit code.
      The `try/catch` is scoped tightly around `Deserialize`/`ValidateAndNormalize`/
      `Serialize` only — stdin read and stdout write are outside it, since they don't
      raise the exceptions being handled.
  - `InternalsVisibleTo` is set for `ConsoleApp1.Tests` so tests can reach `internal`
    types without changing visibility for production code.
- `ConsoleApp1.Tests/` — xUnit test project (`net10.0`).
  - `IncomingMessageTests.cs` — covers `IncomingMessage.Parse`: valid input, id as plain
    string vs `JsonElement`, missing/null/wrong-typed fields, timestamps with and
    without timezone info.
  - `ValidateAndNormalizeTests.cs` — covers `Program.ValidateAndNormalize`: uppercasing,
    timestamp normalization, `received_at` injection, payload/extra-field preservation,
    non-mutation of the input dictionary, exception propagation.
  - `Main` is intentionally **not** unit tested (stdin/stdout dependent, out of scope for
    unit testing).

## Conventions established in this codebase

- **Parse, don't validate**: prefer converting raw/untyped input into a strongly-typed
  value once, at a single boundary, over scattering `TryGetValue`/type checks through
  the codebase. See `IncomingMessage`.
- **Nullable reference types are enabled and meant to be trusted**: JSON dictionary
  values are `Dictionary<string, object?>` (not `object`) because JSON values really can
  be `null`. Don't add null checks/exceptions the type system already rules out (e.g. a
  non-nullable parameter doesn't need a redundant `is null` guard).
- **Narrow `try/catch` scope**: wrap only the statements that can actually throw the
  exceptions being caught. Keeps error attribution unambiguous and avoids silently
  swallowing unrelated failures (e.g. an I/O error on `Console.Out.WriteLine` should not
  be reported as "invalid message").
- Comments stay short — one line explaining a non-obvious *why*, not prose.

## How this was built

Built end-to-end with Claude Code (agentic coding) in a single session, iterating from a
spec into a reviewed, tested, committed state:

1. Initial implementation from a French spec: `ValidateAndNormalize` +
   `Main`, required-field validation, ISO8601 UTC normalization, `ArgumentException` /
   `FormatException` on invalid input.
2. Refactored to the **parse, don't validate** pattern — extracted `IncomingMessage` and
   its `Parse` factory so invalid state becomes unrepresentable past the parse boundary.
3. Trimmed comments that had gotten too verbose.
4. Enabled proper use of nullable reference types (`Dictionary<string, object?>`) and
   removed exceptions/checks that were redundant once nullability was accurate.
5. Narrowed the `try/catch` in `Main` to only the statements that can throw, with an
   explanation of why (clear error attribution, no accidental swallowing of unrelated
   I/O errors).
6. Added a provenance comment block at the end of `Program.cs` recording the prompts
   that shaped the file (kept as project history/context, not removed).
7. Added the `ConsoleApp1.Tests` xUnit project — 25 tests covering `IncomingMessage.Parse`
   and `Program.ValidateAndNormalize`, explicitly excluding `Main`.
8. Initialized the git repo, extended `.gitignore` to exclude `.idea/`, and created the
   root commit (`ff35b85`) with all source and test files. A remote is being set up next.

Provenance is also recorded directly in `ConsoleApp1/Program.cs` as an end-of-file
comment block — keep it up to date if the file changes further via agentic coding.
