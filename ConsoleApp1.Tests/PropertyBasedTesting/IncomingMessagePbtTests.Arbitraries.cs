using System.Globalization;
using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;

namespace ConsoleApp1.Tests.Tdd;


    // --- Generators, exposed as Arbitrary<T> keyed by the wrapper type ---
    public static class Arbitraries
    {
        private static readonly JsonElement[] NonObjectPayloads =
        {
            JsonSerializer.Deserialize<JsonElement>("\"string\""),
            JsonSerializer.Deserialize<JsonElement>("123"),
            JsonSerializer.Deserialize<JsonElement>("true"),
            JsonSerializer.Deserialize<JsonElement>("[]"),
        };

        public static Arbitrary<IncomingMessagePbtTests.ValidId> ValidId() =>
            ArbMap.Default.ArbFor<string>()
                .Generator
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new IncomingMessagePbtTests.ValidId(s))
                .ToArbitrary();

        public static Arbitrary<IncomingMessagePbtTests.ValidTimestamp> ValidTimestamp() =>
            // Seconds since the Unix epoch, up to ~10 years past 2020, formatted as ISO8601 'Z'.
            Gen.Choose(0, 1_600_000_000)
                .Select(secs => DateTimeOffset.UnixEpoch.AddSeconds(secs))
                .Select(dt => new IncomingMessagePbtTests.ValidTimestamp(
                    dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))
                .ToArbitrary();

        /// <summary>
        /// Generates a JSON *object* payload from a shrinkable model: an array of
        /// <c>(string key, int value)</c> entries.
        ///
        /// We never generate the opaque <see cref="JsonElement"/> directly — FsCheck cannot shrink
        /// it. Instead we start from <c>Arbitrary&lt;(string, int)[]&gt;</c>, whose array shrinker
        /// removes entries and minimises each key/value, and <c>Convert</c> that to
        /// <see cref="IncomingMessagePbtTests.ValidPayload"/>. A failing payload therefore shrinks
        /// toward the smallest object that still breaks the property (e.g. <c>{"": 0}</c>, or
        /// <c>{}</c>). The model is serialised to a <see cref="JsonElement"/> lazily by
        /// <see cref="IncomingMessagePbtTests.ValidPayload.Value"/>.
        ///
        /// The <c>from</c> direction of <c>Convert</c> rebuilds the entry array from the dictionary
        /// so FsCheck can feed shrink candidates back through the round trip.
        /// </summary>
        public static Arbitrary<IncomingMessagePbtTests.ValidPayload> ValidPayload() =>
            ArbMap.Default.ArbFor<Tuple<string, int>[]>()
                .Convert(
                    entries => new IncomingMessagePbtTests.ValidPayload(ToDictionary(entries)),
                    payload => ToEntries(payload.Model));

        private static Dictionary<string, object?> ToDictionary(Tuple<string, int>[] entries)
        {
            var d = new Dictionary<string, object?>(entries.Length);
            foreach (var e in entries)
                d[e.Item1 ?? string.Empty] = e.Item2; // last write wins on duplicate keys
            return d;
        }

        private static Tuple<string, int>[] ToEntries(Dictionary<string, object?> map)
        {
            var result = new Tuple<string, int>[map.Count];
            var i = 0;
            foreach (var (k, v) in map)
                result[i++] = Tuple.Create(k, v is int n ? n : 0);
            return result;
        }

        public static Arbitrary<IncomingMessagePbtTests.InvalidId> InvalidId() =>
            Gen.Elements<object?>(123, 12.5, true, new List<int>())
                .Select(o => new IncomingMessagePbtTests.InvalidId(o))
                .ToArbitrary();

        public static Arbitrary<IncomingMessagePbtTests.InvalidTimestampType> InvalidTimestampType() =>
            Gen.Elements<object?>(1234567890L, 12.5, true, new List<int>())
                .Select(o => new IncomingMessagePbtTests.InvalidTimestampType(o))
                .ToArbitrary();

        public static Arbitrary<IncomingMessagePbtTests.InvalidPayload> InvalidPayload() =>
            Gen.OneOf(
                    Gen.Elements(NonObjectPayloads).Select(e => (object?)e),
                    Gen.Elements<object?>("not-a-jsonelement", 42))
                .Select(o => new IncomingMessagePbtTests.InvalidPayload(o))
                .ToArbitrary();
    }
