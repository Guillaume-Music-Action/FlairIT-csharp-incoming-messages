# TDD Spec – IncomingMessage

**Rule 1** – *Parse a valid incoming message*  
Given a dictionary containing the required fields `id` (string), `timestamp` (ISO‑8601 string) and `payload` (JSON object), `IncomingMessage.Parse` returns an `IncomingMessage` whose `Id`, `Timestamp` (as UTC `DateTimeOffset`) and `Payload` (`JsonElement`) match the input values.

**Rule 2** – *Missing required field throws ArgumentException*  
If the input dictionary lacks the `id` field (or it is null), `Parse` throws `ArgumentException` mentioning the missing field.

**Rule 3** – *Missing timestamp throws ArgumentException*  
If the input dictionary lacks the `timestamp` field (or it is null), `Parse` throws `ArgumentException` mentioning the missing field.
