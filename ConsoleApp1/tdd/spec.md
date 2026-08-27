# TDD Spec – IncomingMessage

**Rule 1** – *Parse a valid incoming message*  
Given a dictionary containing the required fields `id` (string), `timestamp` (ISO‑8601 string) and `payload` (JSON object), `IncomingMessage.Parse` returns an `IncomingMessage` whose `Id`, `Timestamp` (as UTC `DateTimeOffset`) and `Payload` (`JsonElement`) match the input values.

**Rule 2** – *Missing required field throws ArgumentException*  
If the input dictionary lacks the `id` field (or it is null), `Parse` throws `ArgumentException` mentioning the missing field.

**Rule 3** – *Missing timestamp throws ArgumentException*  
If the input dictionary lacks the `timestamp` field (or it is null), `Parse` throws `ArgumentException` mentioning the missing field.

**Rule 4** – *Missing payload throws ArgumentException*  
If the input dictionary lacks the `payload` field (or it is null), `Parse` throws `ArgumentException` mentioning the missing field.

**Rule 5** – *Invalid timestamp throws FormatException*  
If the input dictionary contains a `timestamp` field that cannot be parsed as ISO‑8601, `Parse` throws `FormatException` mentioning the invalid value.

**Rule 6** – *Wrong id type throws ArgumentException*  
If the input dictionary contains an `id` field that is not a string, `Parse` throws `ArgumentException` mentioning the invalid type.

**Rule 7** – *Wrong timestamp type throws ArgumentException*  
If the input dictionary contains a `timestamp` field that is not a string, `Parse` throws `ArgumentException` mentioning the invalid type.

**Rule 8** – *Wrong payload type throws ArgumentException*  
If the input dictionary contains a `payload` field that is not a JSON object, `Parse` throws `ArgumentException` mentioning the invalid type.
