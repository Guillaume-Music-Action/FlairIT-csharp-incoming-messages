# Property-Based Testing — a tutorial from this codebase

This folder contains a property-based test suite for `IncomingMessageTdd.Parse`
(`ConsoleApp1/tdd/IncomingMessage.cs`). It is a mirror of the example-based suite in
`../Tdd/IncomingMessageTddTests.cs`: same behaviours, expressed as *properties* instead
of hand-picked cases.

This README is a walk-through of every PBT concept the suite uses, ordered from the
basics to the more advanced. Read it top to bottom the first time; use it as a reference
after that. The stack is **FsCheck 3.x** + **FsCheck.Xunit** + **xUnit**, C# flavour.

---

## Table of contents

1. [What a property is](#1-what-a-property-is)
2. [Example-based vs property-based](#2-example-based-vs-property-based)
3. [Generators — `Gen<T>`](#3-generators--gent)
4. [Arbitraries — `Arbitrary<T>` = generator + shrinker](#4-arbitraries--arbitraryt--generator--shrinker)
5. [Writing a property with `[Property]`](#5-writing-a-property-with-property)
6. [How FsCheck picks a generator: type-directed resolution](#6-how-fscheck-picks-a-generator-type-directed-resolution)
7. [Wrapper types: making one .NET type into many logical kinds](#7-wrapper-types-making-one-net-type-into-many-logical-kinds)
8. [Combinators: `Select`, `Where`, `Zip`, `OneOf`, `Elements`, `ListOf`](#8-combinators-select-where-zip-oneof-elements-listof)
9. [Shrinking — the feature that makes PBT worth it](#9-shrinking--the-feature-that-makes-pbt-worth-it)
10. [When shrinking silently stops working](#10-when-shrinking-silently-stops-working)
11. [The model pattern: generate a shrinkable model, project to the real type](#11-the-model-pattern-generate-a-shrinkable-model-project-to-the-real-type)
12. [`Arbitrary.Convert` and why the reverse direction matters](#12-arbitraryconvert-and-why-the-reverse-direction-matters)
13. [Categories of property: the ones we use and the ones we should](#13-categories-of-property-the-ones-we-use-and-the-ones-we-should)
14. [Oracles: what you actually assert](#14-oracles-what-you-actually-assert)
15. [Reading a failure report](#15-reading-a-failure-report)
16. [Tuning: iteration count, size, replay, config](#16-tuning-iteration-count-size-replay-config)
17. [Common pitfalls](#17-common-pitfalls)
18. [A critique of this suite, and how to make it stronger](#18-a-critique-of-this-suite-and-how-to-make-it-stronger)
19. [Glossary](#19-glossary)

---

## 1. What a property is

An **example-based test** asserts one concrete input/output pair:

```csharp
Parse({ id: "abc", timestamp: "2023-01-01T12:00:00+02:00", payload: {...} })
    .Timestamp
    .Should().Be(2023-01-01T10:00:00Z);
```

A **property** is a statement that must hold *for all* inputs in some set:

> For every valid `id` string, every valid ISO-8601 `timestamp`, and every JSON object
> `payload`, `Parse` returns a message whose `Id` equals the input `id`, whose
> `Timestamp` re-formats to the input string, and whose `Payload` is a JSON object.

A property-based testing library turns that "for every…" into an executable check: it
**generates** many random inputs from the set, runs the property body on each, and fails
if any input makes it throw or return `false`. By default FsCheck runs **100** inputs per
property.

The mental shift: you stop choosing inputs and start describing the *space* of inputs
(the generator) and the *invariant* that space must satisfy (the property body).

---

## 2. Example-based vs property-based

| | Example-based (`[Fact]`, `[Theory]`) | Property-based (`[Property]`) |
|---|---|---|
| Inputs | You write them | The library generates them |
| Coverage | Exactly the cases you thought of | A random sample of the whole space, different every run |
| Regression pinning | Excellent — a `[Fact]` is a named, stable case | Weaker — a random run may not re-hit a past bug unless you pin the seed |
| Failure output | The input is right there in the test | The library reports a *shrunk* minimal counterexample |
| Cost to write | Low per case, high for many cases | High up front (generators), low to add another property |
| Best at | Known corner cases, documenting intent | Invariants, round-trips, "I don't know what breaks this" |

They are complementary. This codebase keeps **both** suites. Good practice: when a
property finds a bug, add a `[Fact]` pinning that exact minimal case, then fix the code.

---

## 3. Generators — `Gen<T>`

`Gen<T>` is a recipe for producing random `T` values. You rarely implement one from
scratch; you build it from primitives and combinators.

Primitives used in this suite (all from `FsCheck.Fluent.Gen`):

```csharp
Gen.Choose(0, 1_600_000_000)          // uniform int in [lo, hi]
Gen.Elements(a, b, c)                  // pick one of a fixed set, uniformly
Gen.Elements<object?>(123, 12.5, true) // same, with an explicit element type
Gen.OneOf(genA, genB)                  // pick one of several *generators*, then run it
```

And the default generator for a plain type, pulled from the ambient registry:

```csharp
ArbMap.Default.ArbFor<string>().Generator     // Gen<string> — FsCheck's built-in
ArbMap.Default.ArbFor<Tuple<string,int>[]>()  // Arbitrary<(string,int)[]> — built-in, composed
```

`ArbMap.Default` is FsCheck's registry of built-in arbitraries for primitives,
collections, tuples, records, options, etc. `ArbFor<T>()` looks one up.

A `Gen<T>` on its own has **no shrinking**. That is what `Arbitrary<T>` adds.

---

## 4. Arbitraries — `Arbitrary<T>` = generator + shrinker

```
Arbitrary<T>  ≈  { Gen<T> Generator;  IEnumerable<T> Shrinker(T failing); }
```

- **Generator** — makes random values (section 3).
- **Shrinker** — given a value that *failed* the property, produces a sequence of
  "smaller" candidates to try instead. Section 9 covers this in full.

You turn a `Gen<T>` into an `Arbitrary<T>` with `.ToArbitrary()`:

```csharp
Gen.Elements(1, 2, 3).ToArbitrary()   // Arbitrary<int> with an EMPTY shrinker
```

`.ToArbitrary()` with no argument gives you `Shrinker = _ => []`. That is the single
most common reason shrinking "doesn't work" (section 10).

The built-in arbitraries in `ArbMap.Default` come with real shrinkers. `int` shrinks
toward 0, `string` toward `""`, arrays toward `[]` by dropping elements and shrinking
what remains. **Prefer building on those.**

---

## 5. Writing a property with `[Property]`

`FsCheck.Xunit` gives you the `[Property]` attribute. A property is a method whose
**parameters are the generated inputs** and whose body is the invariant.

From `IncomingMessagePbtTests.cs`:

```csharp
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
```

Three ways a property can pass or fail:

| Body style | Passes when | Fails when |
|---|---|---|
| `void`, uses assertions (this suite) | body returns without throwing | any assertion throws |
| returns `bool` | `true` | `false` |
| returns `Property` (from `.ToProperty()`, `Prop.ForAll`, `.When(...)`, `.Label(...)`) | the composed property holds | it doesn't |

This suite uses the `void` + AwesomeAssertions style throughout — it reads like the
`[Fact]` mirror and gives good assertion messages. The `bool` / `Property` styles matter
when you need `.Classify`, `.Label`, `==>` (conditional), or `.And`/`.Or`.

FsCheck runs the body **100 times** with fresh inputs (configurable, section 16).

---

## 6. How FsCheck picks a generator: type-directed resolution

**FsCheck matches a parameter to a generator by the parameter's static type, never by
its name.** This trips up everyone once.

`[Property(Arbitrary = new[] { typeof(Arbitraries) })]` tells FsCheck: "before falling
back to the default registry, look in `Arbitraries` for `Arbitrary<T>`-returning members."

`Arbitraries` (in `IncomingMessagePbtTests.Arbitraries.cs`) is a plain class of static
methods:

```csharp
public static class Arbitraries
{
    public static Arbitrary<IncomingMessagePbtTests.ValidId> ValidId() => ...;
    public static Arbitrary<IncomingMessagePbtTests.ValidTimestamp> ValidTimestamp() => ...;
    public static Arbitrary<IncomingMessagePbtTests.ValidPayload> ValidPayload() => ...;
    public static Arbitrary<IncomingMessagePbtTests.InvalidId> InvalidId() => ...;
    // ...
}
```

When FsCheck sees `Parse_MissingId_ThrowsArgumentException(ValidTimestamp timestamp,
ValidPayload payload)`, it:

1. needs an `Arbitrary<ValidTimestamp>` → finds `Arbitraries.ValidTimestamp()`,
2. needs an `Arbitrary<ValidPayload>` → finds `Arbitraries.ValidPayload()`.

The method *name* `ValidTimestamp` is irrelevant; only its return type `Arbitrary<ValidTimestamp>`
is used for matching. If no member returns `Arbitrary<T>` for a parameter type `T`,
FsCheck falls back to `ArbMap.Default` (reflection-generating records/primitives), and if
that also fails you get a runtime error — **not** a compile error.

Ways to register, from most local to most global:

```csharp
[Property(Arbitrary = new[] { typeof(Arbitraries) })]   // per method  (this suite)
[Properties(Arbitrary = new[] { typeof(Arbitraries) })] // per class / per assembly (on an assembly-level attribute)
Arb.Register<Arbitraries>();                            // imperative, e.g. in a fixture
```

This suite repeats the per-method form on every property. Hoisting it to a class-level
`[Properties(...)]` would remove eight identical attributes — a reasonable cleanup.

---

## 7. Wrapper types: making one .NET type into many logical kinds

Type-directed resolution has a consequence: **if two parameters need different
generators, they need different types.**

`Parse` takes a `Dictionary<string, object?>`. Its `id` is a `string`; a *valid* id and
an *invalid* id are both, at the CLR level, `object?`. FsCheck can't give
`string validId` and `string invalidId` different generators — they're the same type.

The fix: one-field wrapper structs, one per logical kind.

```csharp
public readonly record struct ValidId(string Value);
public readonly record struct ValidTimestamp(string Value);
public readonly record struct InvalidId(object? Value);
public readonly record struct InvalidTimestampType(object? Value);
public readonly record struct InvalidPayload(object? Value);

public readonly record struct ValidPayload(Dictionary<string, object?> Model)
{
    public JsonElement Value => JsonSerializer.SerializeToElement(Model);
}
```

Now `ValidId` and `InvalidId` are distinct types, so `Arbitraries.ValidId()` and
`Arbitraries.InvalidId()` resolve independently. In the property body you unwrap with
`.Value`.

**Cost:** ceremony. Six wrapper types and a `.Value` at every use site, existing purely
to satisfy the resolver. This is inherent to type-directed PBT frameworks (FsCheck,
Hedgehog-via-Arb, Hypothesis's `@given` is name-directed and avoids it). FsCheck ships
some ready-made wrappers for exactly this — `NonNull<T>`, `NonEmptyString`,
`NonWhiteSpaceString`, `PositiveInt`, `NonNegativeInt`, `IntWithMinMax` — and you can
often use those instead of rolling your own.

`ValidPayload` also demonstrates the **model pattern** (section 11): the constructor
takes the *shrinkable* thing (`Dictionary<string, object?>`), and `Value` is a computed
projection to the type `Parse` actually wants (`JsonElement`).

---

## 8. Combinators: `Select`, `Where`, `Zip`, `OneOf`, `Elements`, `ListOf`

These build big generators from small ones. All are on `Gen<T>` (LINQ-style) in
`FsCheck.Fluent`.

### `Select` — map (functor)

Transform every generated value. Shrinking is **preserved** — FsCheck shrinks the
source, then re-applies your function.

```csharp
Gen.Choose(0, 1_600_000_000)                              // Gen<int>
    .Select(secs => DateTimeOffset.UnixEpoch.AddSeconds(secs))  // Gen<DateTimeOffset>
    .Select(dt => new ValidTimestamp(dt.ToString("yyyy-MM-ddTHH:mm:ssZ", Invariant)))
```

### `Where` / `Filter` — reject values that don't match a predicate

```csharp
ArbMap.Default.ArbFor<string>().Generator
    .Where(s => !string.IsNullOrWhiteSpace(s))   // drop null / "" / "  "
```

**Caveat:** `Where` works by generate-and-retry. If the predicate rejects most values,
generation gets slow or gives up ("exhausted"). Only use it for cheap, usually-true
predicates. To *exclude a small slice*, filter. To *hit a narrow target*, construct the
value directly with `Select` instead.

### `Zip` — combine two generators into a tuple

```csharp
Gen.Zip(keyGen, valueGen)   // Gen<(string, int)>
```

### `Gen.OneOf` — choose among generators

```csharp
Gen.OneOf(
    Gen.Elements(NonObjectPayloads).Select(e => (object?)e),  // a JSON scalar/array
    Gen.Elements<object?>("not-a-jsonelement", 42))           // a non-JsonElement value
```

Each branch is itself a generator; FsCheck picks a branch, then runs it. Contrast with
`Gen.Elements`, which picks among **values**.

### `ListOf` / `ArrayOf` / `ListOf(n)` — collections

```csharp
someGen.ListOf()      // Gen<List<T>>   — random length
someGen.ArrayOf()     // Gen<T[]>
someGen.ListOf(5)     // exactly 5
```

Collection generators from FsCheck shrink well: fewer elements, then smaller elements.

---

## 9. Shrinking — the feature that makes PBT worth it

When a property fails, the failing input is usually large and noisy — a 40-character
random `id`, a dictionary with 12 junk keys. That tells you little.

**Shrinking** is FsCheck's search for a *minimal* input that still fails. After the first
failure it repeatedly asks the arbitrary's shrinker for "smaller" candidates, tries each,
and keeps any that still fails, recursing until no smaller failing input exists.

You then get reported both the **Original** random failure and the **Shrunk** minimal
one. The shrunk value is the bug report.

Example from developing this suite. A deliberately-broken assertion ("payload must have
fewer than 2 keys") failed:

```
Falsifiable, after 15 tests (3 shrinks)
Original: ValidPayload { Value = {"":0,"s":-1} }
Shrunk:   ValidPayload { Value = {"":0,"a":0} }
```

Shrinking drove the value `-1 → 0`, the key `"s" → "a"`, and tried to drop entries but
couldn't go below 2 (that's the failure boundary). `{"":0,"a":0}` is the minimal object
that trips the assertion — far more legible than the original.

Good shrinkers converge toward a canonical "zero": `0`, `""`, `[]`, `{}`, shortest
string, fewest collection elements. FsCheck's built-ins already do this for primitives
and collections.

### What "smaller" means, structurally

- `int` → toward 0 (halving, then ±1)
- `string` → shorter, then characters toward `'a'`/low codepoints
- `T[]` / `List<T>` → fewer elements (removes chunks, then singles), then shrink each element
- tuple / record → shrink each field independently
- discriminated-union-ish (via `OneOf`) → toward the earlier/simpler case

---

## 10. When shrinking silently stops working

Shrinking never errors. It just does nothing, and you get `(0 shrinks)` with a big ugly
`Original` and no `Shrunk` line. Causes, all present in the history of this file:

1. **`Gen.Elements(...).ToArbitrary()`** — `.ToArbitrary()` with no shrinker argument
   installs the empty shrinker. `Gen.Elements` over a fixed list can't shrink anyway
   (there's no order on arbitrary elements), but even a shrinkable `Gen` loses shrinking
   here.
   *In this suite:* `InvalidId`, `InvalidTimestampType`, `InvalidPayload` all use this.
   Acceptable, because each picks from 2–4 hand-written values — there's nothing to
   minimise.

2. **`Gen.Choose(...).Select(...).ToArbitrary()`** — `Gen.Choose` shrinks as an int, but
   `.ToArbitrary()` throws that away.
   *In this suite:* `ValidTimestamp`. A failing timestamp is reported as whatever random
   instant first broke the property, not minimised toward the epoch. Low stakes here;
   would matter if timestamp logic were the thing under test.

3. **Generating an opaque type FsCheck has no shrinker for** — `JsonElement`, `XElement`,
   `Regex`, your own class with no registered `Arbitrary`. Reflection can't take it apart.
   *In this suite:* the original `ValidPayload` generated `JsonElement` directly via
   `Gen.Elements(threeHardcodedElements)`. `0 shrinks`, always.

4. **`Arbitrary.Convert` with a broken reverse function** — see section 12.

The fixes, in order of preference:

- Generate a **type FsCheck already shrinks** (primitive, tuple, array, record, or a
  built-in wrapper) and `Select`/`Convert` to the type you need. ← section 11
- Provide an explicit shrinker: `Arb.From(gen, shrinkFn)` /
  `Gen.ToArbitrary(gen, shrinkFn)`.
- Accept no shrinking when the domain is a handful of literals (the `Invalid*` cases).

---

## 11. The model pattern: generate a shrinkable model, project to the real type

This is the central technique for anything tree-shaped: **JSON, XML, ASTs, DOMs, config
trees.**

> Don't generate the hard type. Generate a *model* — a plain structure FsCheck already
> knows how to shrink — and convert the model to the hard type at the boundary.

`ValidPayload` is the worked example. `Parse` wants a `JsonElement` of
`ValueKind.Object`. `JsonElement` is opaque and unshrinkable. So:

**The model** is `Tuple<string, int>[]` — an array of key/value pairs. FsCheck's built-in
array arbitrary shrinks it thoroughly (drop pairs; shrink each key string toward `""`;
shrink each int toward 0).

**The generator** (`Arbitraries.ValidPayload()`):

```csharp
public static Arbitrary<ValidPayload> ValidPayload() =>
    ArbMap.Default.ArbFor<Tuple<string, int>[]>()   // shrinkable source arbitrary
        .Convert(
            entries => new ValidPayload(ToDictionary(entries)),   // model  -> wrapper
            payload => ToEntries(payload.Model));                 // wrapper -> model
```

`ToDictionary` folds the pairs into a `Dictionary<string, object?>` ("last write wins"
on duplicate keys, so the array can contain repeats harmlessly). The wrapper's `Value`
property then serialises that dictionary to a `JsonElement` on demand:

```csharp
public JsonElement Value => JsonSerializer.SerializeToElement(Model);
```

**The property** consumes `payload.Value` (the `JsonElement`) to call `Parse`, and
`payload.Model` (the dictionary) as the oracle for what should have survived:

```csharp
result.Payload.ValueKind.Should().Be(JsonValueKind.Object);
foreach (var (key, value) in payload.Model)
    result.Payload.GetProperty(key).GetInt32().Should().Be((int)value!);
```

**What we gave up:** payloads are now flat `{ string: int }` objects — no nesting, no
string/bool values, no arrays. That's the price of a model the framework shrinks for
free. Since `Parse` only checks `ValueKind == Object`, the flat model exercises
everything `Parse` looks at. If you needed nested JSON, you'd define a recursive model
type and a sized generator — see section 18.

**The general recipe:**

| Hard type | Shrinkable model | Convert |
|---|---|---|
| `JsonElement` (object) | `(string,int)[]` / `Dictionary<string,int>` / a record | serialise |
| `JsonElement` (arbitrary) | a recursive `JsonModel` DU with `Gen.Sized` | fold to `JsonNode`, then `JsonElement` |
| `XElement` | nested record | build the tree |
| SQL query string | a `Query` record (table, columns, predicate model) | render |
| `HttpRequestMessage` | `(Method, PathSegments, Headers, BodyModel)` | construct |

---

## 12. `Arbitrary.Convert` and why the reverse direction matters

```csharp
Arbitrary<U> Convert<T, U>(this Arbitrary<T> source, Func<T, U> to, Func<U, T> from)
```

`Convert` builds an `Arbitrary<U>` from an `Arbitrary<T>` plus a **bijection** (a pair of
functions mapping back and forth). Crucially, it **keeps `T`'s shrinker**:

- **Generation:** run `source.Generator` to get a `T`, apply `to`, yield the `U`.
- **Shrinking a failing `u`:** apply `from` to get a `t`, ask `source`'s shrinker for
  smaller `t'` values, apply `to` to each → smaller `u'` candidates.

That is why `from` (`payload => ToEntries(payload.Model)`) is not optional and not a
stub. If `from` loses information or is wrong, the round trip `to(from(u))` doesn't
reconstruct `u`, and shrinking either does nothing or explores the wrong neighbourhood.
In `ValidPayload`, `ToEntries` faithfully turns the dictionary back into a
`Tuple<string,int>[]` so FsCheck can walk it.

Contrast with `Select` (section 8), which is one-directional (`Func<T,U>` only). `Select`
preserves shrinking too, but only because FsCheck keeps shrinking the *source* `T`
internally and never needs to map a `U` back. Use `Select` when you generate straight
through to the final type; use `Convert` when your `Arbitrary` is publicly typed as `U`
but you want `T`'s shrinker underneath (the wrapper-type case).

FsCheck also exposes `Convert` in F# style (`FsCheck.FSharp.Arb.Convert`) and a
fluent static (`FsCheck.Fluent.Arb.Convert`). This suite uses the instance-method form
on `Arbitrary<T>`.

---

## 13. Categories of property: the ones we use and the ones we should

A catalogue (from Scott Wlaschin's "Choosing properties for property-based testing"),
annotated with where this suite stands.

| Pattern | Shape | In this suite? |
|---|---|---|
| **Round-trip / there-and-back** | `decode(encode(x)) == x` | Partial — `Parse_ValidInputs_RoundtripCorrectly` checks id and payload survive; timestamp only via string re-format |
| **Invariant / "some things never change"** | property `P` holds on the output whenever it held on the input | Yes — "valid inputs ⇒ `Parse` returns an object payload" |
| **Idempotence** | `f(f(x)) == f(x)` | No — could add: `Normalize(Normalize(x)) == Normalize(x)` for `ValidateAndNormalize` |
| **Oracle / test against a reference** | `f(x)` agrees with a simpler independent implementation | No — could parse the timestamp with a second method and compare instants |
| **Metamorphic** | relate `f(x)` and `f(transform(x))` without knowing either exactly | No — e.g. upper-casing the input `id` must not change `Parse(...).Id.ToUpper()` |
| **"Hard to prove, easy to verify"** | generating a solution is hard, checking one is easy | N/A for a parser |
| **Structural induction / model** | generate a model, assert `f` matches a model interpretation | Yes, mechanically — `ValidPayload` is a model; the assertion is a direct interpretation |
| **Failure / rejection properties** | malformed input ⇒ specific error | Yes — the six `Throws…` properties |

The suite is strong on *invariant* and *rejection* and weak on *round-trip with a real
oracle*, *idempotence*, and *metamorphic*. Section 18 turns that into concrete
additions.

---

## 14. Oracles: what you actually assert

An **oracle** is the thing that decides pass/fail. Weak oracle → the property runs 100
times and proves almost nothing.

Anti-pattern in `Parse_ValidInputs_RoundtripCorrectly` as originally written:

```csharp
result.Payload.ValueKind.Should().Be(JsonValueKind.Object);   // that's ALL it checked
```

100 iterations that only confirm "the output is *an* object". A bug that dropped or
mangled payload contents passes.

Strengthened (current code):

```csharp
foreach (var (key, value) in payload.Model)
    result.Payload.GetProperty(key).GetInt32().Should().Be((int)value!);
```

Now the oracle is the model itself: every generated entry must be present and equal in
the output. This is only possible *because* we kept the model (`payload.Model`) around —
another payoff of section 11.

Oracle strategies, strongest first:

1. **Independent reference implementation** — a second, obviously-correct way to compute
   the answer; assert equality. (Timestamp parsing could use `DateTime.Parse` +
   manual offset math.)
2. **The model interprets itself** — as above; the generator's model *is* the expected
   answer.
3. **Full inversion** — reconstruct the input from the output and compare
   (`serialize(Parse(json))` deep-equals `json`).
4. **Structural relation** — output relates to input in a checkable way without either
   being fully known (metamorphic).
5. **Weak invariant** — "output is non-null / of the right kind". Better than nothing;
   rarely enough on its own.

For the timestamp string comparison in this suite:

```csharp
result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ", Invariant).Should().Be(timestamp.Value);
```

This is weak by construction — the generator *built* `timestamp.Value` with the same
format string, so it can only catch gross breakage, not a timezone-handling bug
(`AssumeUniversal | AdjustToUniversal`). A real timestamp property is in section 18.

---

## 15. Reading a failure report

```
Falsifiable, after 15 tests (3 shrinks) (10840326949857908462,8573439228257251607).
Original:
 (ValidId { Value = "r }, ValidTimestamp { Value = 2006-02-05T19:12:47Z },
  ValidPayload { Value = {"":0,"s":-1} })
Shrunk:
 (ValidId { Value = "r }, ValidTimestamp { Value = ... },
  ValidPayload { Value = {"":0,"a":0} })
---- Expected result.Payload.EnumerateObject().Count() to be less than 2, but found 2.
```

Line by line:

- **`Falsifiable, after 15 tests`** — the property failed on the 15th generated input.
- **`(3 shrinks)`** — from that failure, FsCheck found 3 successively smaller inputs that
  also failed. `(0 shrinks)` means shrinking did nothing → suspect section 10.
- **`(10840326949857908462,8573439228257251607)`** — the **PRNG seed**. Pin it to replay
  the exact run: `[Property(Replay = "10840326949857908462,8573439228257251607")]`.
- **`Original:`** — the raw 15th input, as a tuple of the property's parameters.
- **`Shrunk:`** — the minimal failing input. **This is the bug.** Copy it into a `[Fact]`.
- The assertion message is from AwesomeAssertions, unchanged from how it reads in a
  normal unit test.

If you instead see **`Arguments exhausted after N tests`**: a `Where` filter is rejecting
too much (section 8). Loosen the predicate or construct the value directly.

---

## 16. Tuning: iteration count, size, replay, config

On `[Property]`:

```csharp
[Property(
    MaxTest = 500,          // inputs to try (default 100)
    MaxRejected = 1000,     // give up after this many Where-rejections (default 100 * MaxTest)
    StartSize = 0,          // "size" passed to generators on the first test
    EndSize = 100,          // ... and on the last; FsCheck ramps size across the run
    Replay = "12345,67890", // fixed PRNG seed — reproduce a failure
    QuietOnSuccess = true,  // don't print the "Ok, passed 100 tests" line
    Arbitrary = new[] { typeof(Arbitraries) })]
```

**Size** is a number FsCheck grows from `StartSize` to `EndSize` over the run;
size-aware generators use it as a magnitude knob (max list length, max int, recursion
depth). `Gen.Sized(n => ...)` reads it. `Gen.Choose` ignores it (always uniform on its
range) — which is why `ValidTimestamp` samples the whole 50-year span from test 1, and
why a size-aware version would be gentler on shrinking.

Assembly-wide defaults: put `[assembly: Properties(MaxTest = 200, Arbitrary = new[] {
typeof(Arbitraries) })]` in an `AssemblyInfo.cs`.

For deep control, implement `Config` / use `PropertyConfig` and pass via a custom
attribute subclass — not needed in this suite.

---

## 17. Common pitfalls

1. **Parameter typed by the generator, not the value.**
   `Parse_X(NonEmptyStringGen id, ...)` does not compile / does not resolve.
   `NonEmptyStringGen` must be a *type* with a registered `Arbitrary<NonEmptyStringGen>`.
   (This was the bug that broke the file originally.)

2. **`Gen.Choose` with a non-integer type.** It's `int`/`int64` only. For a
   `DateTimeOffset` range, `Choose` on a `long` of ticks/seconds and `Select` into the
   date.

3. **`Gen.Elements(..., null)`** — the `null` makes overload resolution ambiguous. Use
   `Gen.Elements<object?>(a, b, null)` with an explicit type argument.

4. **Silent loss of shrinking** via `.ToArbitrary()` — section 10.

5. **`Convert` with a lossy or wrong reverse function** — shrinking explores garbage or
   nothing. `to(from(x))` must round-trip.

6. **Over-tight `Where`** — `Arguments exhausted`. Filter to *exclude a slice*, construct
   to *hit a target*.

7. **Non-deterministic property body** — reading `DateTime.Now`, a shared static, the
   filesystem. A property must be a pure function of its inputs, or shrinking and replay
   are meaningless. (`Parse` itself reads no clock — good. `ValidateAndNormalize` writes
   `received_at = DateTime.UtcNow`, so a property on *that* must not assert on
   `received_at`'s exact value.)

8. **Mutating a generated value in the body** and expecting the next iteration to see it
   fresh — generated reference types (`Dictionary`, arrays) may be reused across shrink
   attempts. Treat inputs as read-only; copy before mutating. (`IncomingMessageScenario`
   copies into its own dict, so this suite is safe.)

9. **Asserting on `float`/`double` equality** after arithmetic — use a tolerance.

10. **One giant property with five parameters and ten assertions.** When it fails you
    can't tell which invariant broke. Prefer several focused properties (this suite does
    — one per behaviour).

---

## 18. A critique of this suite, and how to make it stronger

**What's good:** mechanics are correct; one property per behaviour; the `ValidPayload`
model pattern with a real oracle; wrapper types cleanly separate valid/invalid kinds.

**What's weak:**

- **Several "properties" barely quantify over anything.**
  `Parse_MissingId_ThrowsArgumentException` varies `timestamp` and `payload` — fields
  `Parse` never inspects before it throws on the missing `id`. 100 iterations ≈ 1 test
  case. `Parse_InvalidTimestamp` hard-codes `"not-a-timestamp"`; the id/payload it varies
  are irrelevant to the code path.
- **`Parse_WrongIdType` / `WrongTimestampType` / `WrongPayloadType`** pick from 2–4
  literals — a `[Theory]` with `[InlineData]` in disguise, plus generator machinery.
- **No property touches the one genuinely interesting logic in `Parse`:** timestamp
  normalisation (`AssumeUniversal | AdjustToUniversal`, offsets, `Z`, fractional
  seconds). The current timestamp check compares a string to a string built with the
  same format — it cannot fail on a timezone bug.
- **No idempotence / metamorphic / independent-oracle properties.**

**Concrete improvements:**

1. **Real timestamp round-trip with an independent oracle.**
   Generate an arbitrary `DateTimeOffset` *and* a rendering style (`…Z`, `…+02:00`,
   `…-05:30`, no offset, fractional seconds). Assert:
   ```
   Parse(render(dt, style)).Timestamp.ToUniversalTime() == dt.ToUniversalTime()
   ```
   This quantifies over *all* instants and *all* valid renderings, and exercises the
   normalisation. Oracle = `DateTimeOffset` equality, not string equality.

2. **Collapse the six rejection properties into one "corrupt a valid message" property.**
   Generate a valid `Dictionary<string, object?>`, then generate a *mutation*: drop one
   required key, or replace one value with a wrong-typed one. Assert `Parse` throws the
   right exception type naming the right field. One property, all six current cases,
   genuine generation.

3. **Payload deep round-trip.**
   `JsonSerializer.SerializeToElement(Parse(input).Payload)` should deep-equal the input
   payload element — stronger than the per-key `int` check, and lets the model regain
   nesting.

4. **Nested-JSON model** (if you want structural payloads back).
   ```csharp
   // sketch
   public abstract record JsonModel;
   public sealed record JStr(string V)   : JsonModel;
   public sealed record JNum(int V)      : JsonModel;
   public sealed record JBool(bool V)    : JsonModel;
   public sealed record JArr(JsonModel[] Items)            : JsonModel;
   public sealed record JObj((string Key, JsonModel Val)[] Fields) : JsonModel;
   ```
   Generate with `Gen.Sized` to bound depth; `Convert` to/from `JsonNode`, then to
   `JsonElement`. Every case is a shrinkable record/array, so FsCheck minimises a failing
   tree to its smallest failing shape.

5. **Idempotence for `ValidateAndNormalize`.**
   `Normalize` a valid message, feed the result back in (adjusting for the fields
   `Normalize` rewrites), assert the twice-normalised form equals the once-normalised one
   — modulo `received_at`, which is clock-based and must be excluded from the oracle.

6. **Metamorphic id property.**
   For any valid message, `Parse(m).Id == Parse(withIdUpperCased(m)).Id.ToUpperInvariant()`
   — relates two runs without either expected value being spelled out.

7. **Hoist `Arbitrary = new[] { typeof(Arbitraries) }`** to a class-level
   `[Properties(...)]` and delete the eight repetitions.

8. **Give `ValidTimestamp` a shrinker** (or build it size-aware) so a failing timestamp
   minimises toward the epoch instead of being reported as a random 2006 instant.

---

## 19. Glossary

| Term | Meaning |
|---|---|
| **Property** | A statement true for all inputs in a domain; executable via a PBT runner. |
| **`Gen<T>`** | A recipe for random `T`. No shrinking on its own. |
| **`Arbitrary<T>`** | `Gen<T>` + a shrinker for `T`. What `[Property]` parameters resolve to. |
| **Shrinker** | Function: failing value → sequence of smaller candidates to retry. |
| **Shrinking** | The post-failure search for a minimal failing input. |
| **`ArbMap.Default`** | FsCheck's registry of built-in arbitraries (primitives, collections, records…). |
| **Type-directed resolution** | FsCheck matches a parameter to a generator by its *type*, not its name. |
| **Wrapper type** | A one-field struct giving a logical kind its own type so the resolver can distinguish it. |
| **Model pattern** | Generate a shrinkable model type; project to the hard/opaque real type at the boundary. |
| **`Convert(to, from)`** | Build `Arbitrary<U>` from `Arbitrary<T>` + a bijection, keeping `T`'s shrinker. |
| **`Select`** | One-directional map over a generator; preserves shrinking of the source. |
| **`Where` / `Filter`** | Generate-and-retry rejection of values failing a predicate. |
| **Oracle** | Whatever decides pass/fail for a generated input. |
| **Round-trip property** | `decode(encode(x)) == x` and relatives. |
| **Metamorphic property** | Relates `f(x)` and `f(transform(x))` without knowing either value. |
| **Idempotence** | `f(f(x)) == f(x)`. |
| **Size** | A magnitude parameter FsCheck ramps up across a run; size-aware generators read it. |
| **Replay / seed** | The PRNG seed printed on failure; pin it to reproduce the run exactly. |
| **Falsifiable** | FsCheck's word for "this property failed". |
| **Exhausted** | Generation gave up because a filter rejected too many candidates. |

---

## Files in this folder

| File | Contents |
|---|---|
| `IncomingMessagePbtTests.cs` | The wrapper types and the eight `[Property]` methods. |
| `IncomingMessagePbtTests.Arbitraries.cs` | The `Arbitraries` class — one `Arbitrary<T>` factory per wrapper type. |
| `README.md` | This tutorial. |

Run just this suite:

```bash
dotnet test --filter "FullyQualifiedName~IncomingMessagePbtTests"
```
