# Spécification initiale (version courte, langage naturel)

**But**  
Lire une ligne JSON sur l'entrée standard, la valider, la normaliser et écrire le résultat sur la sortie standard.

**Message attendu**  
- `id` : chaîne obligatoire.  
- `timestamp` : chaîne obligatoire, format ISO‑8601 (avec ou sans fuseau). Si le fuseau manque, on considère que c'est de l'UTC.  
- `payload` : objet JSON (dictionnaire) obligatoire.  
- Tout autre champ est accepté tel quel.

**Règles de validation**  
- Champ manquant ou `null` → erreur *ArgumentException*.  
- Type incorrect (ex. `id` numérique, `timestamp` non‑chaîne, `payload` non‑objet) → *ArgumentException*.  
- `timestamp` non parsable → *FormatException*.

**Normalisation**  
- `id` passé en majuscules.  
- `timestamp` converti en UTC et formaté `yyyy‑MM‑ddTHH:mm:ssZ` (ex. `2023-01-01T10:00:00Z`).  
- `payload` conservé inchangé.  
- Ajout d'un champ `received_at` = heure UTC courante au même format.  
- Les champs supplémentaires du message d'entrée sont recopiés sans modification.  
- L'entrée n'est jamais modifiée (nouveau dictionnaire retourné).

**Gestion des erreurs dans `Main`**  
- Ligne vide → message d'erreur, code retour 1.  
- JSON invalide → *JsonException*, code retour 1.  
- Erreurs de validation (`ArgumentException`, `FormatException`) → message d'erreur, code retour 1.  
- Toute autre exception → message générique, code retour 1.  
- Succès → JSON compact sur stdout, code retour 0.

**Principe « Parse‑don’t‑validate »**  
Toute la validation se fait une seule fois dans `IncomingMessage.Parse`, qui renvoie un objet immuable `IncomingMessage`. Le reste du code manipule uniquement cet objet garanti valide.

**Tests**  
- Tests unitaires pour `IncomingMessage.Parse` (cas valides, manquants, types wrong, timestamp avec/sans fuseau).  
- Tests unitaires pour `ValidateAndNormalize` (majuscules, conversion UTC, `received_at`, conservation payload, non‑mutation, propagation des exceptions, champs extra).  
- `Main` non testé (dépend de stdin/stdout).
