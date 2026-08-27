# TDD Spec – IncomingMessage

**Rule 1** – *Parse a valid incoming message*  
Given a dictionary containing the required fields `id` (string), `timestamp` (ISO‑8601 string) and `payload` (JSON object), `IncomingMessage.Parse` returns an `IncomingMessage` whose `Id`, `Timestamp` (as UTC `DateTimeOffset`) and `Payload` (`JsonElement`) match the input values.
