using System.Globalization;
using System.Text.Json;

namespace ConsoleApp1;

/// <summary>
/// Message entrant déjà validé (id/timestamp/payload garantis présents et bien typés).
/// Parse, don't validate : une fois cet objet construit, son type prouve sa validité
/// et plus rien en aval n'a besoin de revérifier.
/// </summary>
internal sealed record IncomingMessage(string Id, DateTimeOffset Timestamp, JsonElement Payload)
{
    /// <summary>
    /// Seul point du programme où l'on vérifie la forme des données brutes.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Levée si "id", "timestamp" ou "payload" sont absents, ou si leur type ne correspond pas
    /// à celui attendu (respectivement string, string, objet/dictionnaire JSON).
    /// </exception>
    /// <exception cref="FormatException">
    /// Levée si le champ "timestamp" est présent et de type string mais ne peut pas être parsé
    /// comme une date/heure valide (avec ou sans information de fuseau).
    /// </exception>
    public static IncomingMessage Parse(Dictionary<string, object?> raw)
    {
        // --- "id" ---
        if (!raw.TryGetValue("id", out var idValue) || idValue is null)
            throw new ArgumentException("Le champ obligatoire 'id' est absent.");

        var id = ExtractString(idValue, "id");

        // --- "timestamp" ---
        if (!raw.TryGetValue("timestamp", out var timestampValue) || timestampValue is null)
            throw new ArgumentException("Le champ obligatoire 'timestamp' est absent.");

        var timestampRaw = ExtractString(timestampValue, "timestamp");

        // Accepte les timestamps avec ou sans fuseau ; AssumeUniversal + AdjustToUniversal
        // ramène tout vers UTC.
        if (!DateTimeOffset.TryParse(
                timestampRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedTimestamp))
            throw new FormatException(
                $"Le champ 'timestamp' ('{timestampRaw}') n'est pas un timestamp ISO8601 valide.");

        // --- "payload" ---
        if (!raw.TryGetValue("payload", out var payloadValue) || payloadValue is null)
            throw new ArgumentException("Le champ obligatoire 'payload' est absent.");

        // Un objet JSON imbriqué est désérialisé comme JsonElement de ValueKind.Object.
        if (payloadValue is not JsonElement { ValueKind: JsonValueKind.Object } payloadElement)
            throw new ArgumentException("Le champ 'payload' doit être un objet JSON (dictionnaire).");

        return new IncomingMessage(id, parsedTimestamp, payloadElement);
    }

    /// <summary>Extrait une string, que la valeur soit un string natif ou un JsonElement.</summary>
    /// <exception cref="ArgumentException">Si la valeur n'est pas une string.</exception>
    private static string ExtractString(object? value, string fieldName) => value switch
    {
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } je => je.GetString()!,
        _ => throw new ArgumentException($"Le champ '{fieldName}' doit être une chaîne de caractères (string).")
    };
}

/// <summary>
/// Utilitaire de validation et de normalisation de messages JSON entrants avant ingestion.
/// </summary>
internal static class Program
{
    // Format ISO8601 raccourci avec 'Z', ex: 2023-01-01T12:00:00Z
    private const string Iso8601ZFormat = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>
    /// Valide les champs obligatoires du message ("id", "timestamp", "payload") et retourne
    /// une nouvelle Dictionary normalisée (id en majuscules, timestamp en UTC/ISO8601 avec 'Z',
    /// et un champ "received_at" ajouté). La validation est déléguée à IncomingMessage.Parse ;
    /// ici on ne fait que normaliser des données déjà garanties valides.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Levée si "id", "timestamp" ou "payload" sont absents, ou si leur type ne correspond pas
    /// à celui attendu (respectivement string, string, objet/dictionnaire JSON).
    /// </exception>
    /// <exception cref="FormatException">
    /// Levée si le champ "timestamp" est présent et de type string mais ne peut pas être parsé
    /// comme une date/heure valide (avec ou sans information de fuseau).
    /// </exception>
    public static Dictionary<string, object?> ValidateAndNormalize(Dictionary<string, object?> message)
    {
        var parsed = IncomingMessage.Parse(message);

        var normalizedTimestamp = parsed.Timestamp.UtcDateTime.ToString(Iso8601ZFormat, CultureInfo.InvariantCulture);

        var normalized = new Dictionary<string, object?>(message)
        {
            ["id"] = parsed.Id.ToUpperInvariant(),
            ["timestamp"] = normalizedTimestamp,
            ["payload"] = parsed.Payload,
            ["received_at"] = DateTime.UtcNow.ToString(Iso8601ZFormat, CultureInfo.InvariantCulture)
        };

        return normalized;
    }

    private static int Main()
    {
        // Lecture d'une seule ligne JSON depuis stdin : pas d'exception métier attendue ici,
        // donc hors du try.
        var line = Console.In.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            Console.Error.WriteLine($"chaine vide, non acceptée");
            return 1;
        }

        string output;
        try
        {
            var messageBody = JsonSerializer.Deserialize<Dictionary<string, object?>>(line)
                          ?? throw new ArgumentException("Le JSON fourni ne représente pas un objet valide.");

            output = JsonSerializer.Serialize(ValidateAndNormalize(messageBody), new JsonSerializerOptions { WriteIndented = false });
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Erreur de parsing JSON : {ex.Message}");
            return 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Message invalide : {ex.Message}");
            return 1;
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"Erreur de format : {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erreur inattendue : {ex.Message}");
            return 1;
        }

        // Écriture du résultat : pas d'exception métier attendue ici non plus, donc hors du try.
        Console.Out.WriteLine(output);
        return 0;
    }
}

// ---------------------------------------------------------------------------
// Provenance : ce fichier a été produit par agentic coding (Claude Code).
// Étapes / prompts successifs ayant mené à ce résultat :
//
// 1. spécification initiale complète
//    (ValidateAndNormalize + Main, champs id/timestamp/payload, exceptions
//    ArgumentException/FormatException, normalisation ISO8601 UTC).
// 2. "appliquer le pattern: parse don't validate, et expliquer en commentaire
//    pourquoi" — extraction d'un type IncomingMessage construit via
//    Parse(), pour rendre l'état invalide irreprésentable après le point
//    d'entrée.
// 3. "plus court les commentaires, c'est trop verbeux" — réduction des
//    commentaires XML/inline à l'essentiel.
// 4. "nullables are enabled, use them, avoid unecessary exceptions" —
//    passage à Dictionary<string, object?> pour refléter la vraie
//    nullabilité des valeurs JSON, suppression des checks/exceptions
//    devenus redondants avec les nullable reference types.
// 5. "dans le Main, réduit le scope du try/catch uniquement là où les
//    exceptions peuvent se produire et explique le bénéfice de faire cela" —
//    resserrement du try/catch autour de Deserialize/ValidateAndNormalize/
//    Serialize uniquement, lecture stdin et écriture stdout sorties du bloc.
// 6. Ecrire les tests unitaires. En effet, je n'ai pas fait de TDD ici parce que 30mn n'auraient
// PEUT ETRE pas été suffisants et je ne voulais pas me perdre le temps de l'examen
// dans la vraie vie, pour des vraies règles métiers , j'aurai fait des tests unitaires en 1er
// 7. "écrit un fichier de test unitaires, pour tout sauf pour la fonction Main" — projet
//    ConsoleApp1.Tests (xUnit) créé et ajouté à la solution, avec InternalsVisibleTo pour
//    exposer les types internal. IncomingMessageTests.cs couvre IncomingMessage.Parse
//    (id/timestamp/payload valides, manquants, null, mal typés ; timestamp avec/sans fuseau).
//    ValidateAndNormalizeTests.cs couvre Program.ValidateAndNormalize (majuscules sur id,
//    normalisation timestamp, ajout received_at, préservation payload/champs additionnels,
//    non-mutation de l'input, propagation des exceptions). Main non testé : dépend de
//    stdin/stdout, hors périmètre du unit testing.
// ---------------------------------------------------------------------------
