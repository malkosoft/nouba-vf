# Nouba v2.7.41 — Suppression complète du module SMS

Le produit évolue vers un MVP plus simple et plus stable : toute la
fonctionnalité SMS (jamais activée par défaut, et complexe à maintenir avec
ses 3 fournisseurs) a été retirée proprement, sans laisser de code mort.

## Ce qui a été retiré

- **Backend** : `Controllers/SmsController.cs` et `Services/SmsService.cs`
  supprimés (les 3 fournisseurs Twilio / passerelle HTTP Android / modem GSM
  USB partent avec).
- **Démarrage de l'app** (`Program.cs`) : plus d'enregistrement des
  providers SMS ni de `HttpClient` générique (qui ne servait qu'à ça).
- **Modèle `Ticket`** : champs `PhoneNumber` et `SmsSentAt` retirés (ils ne
  servaient qu'au SMS — le suivi mobile par QR utilise `PublicId`, qui reste
  intact).
- **Modèle `UiSettings`** : 17 champs de configuration SMS retirés (Twilio,
  passerelle HTTP, modem GSM, modèles de message, indicatif pays, activation
  du clavier sur la borne).
- **Borne kiosque** (`Views/Borne/Index.cshtml`) : le clavier numérique de
  saisie du téléphone (déjà désactivé visuellement depuis la v2.7.37 mais
  toujours présent dans le code) est intégralement supprimé — HTML, CSS et
  JavaScript associés.
- **Administration** (`Views/Admin/Index.cshtml`) : onglet « SMS
  notifications » retiré (formulaire, sélecteur de fournisseur, test
  d'envoi, lien de navigation, fonctions JS dédiées).
- **Agent** (`AgentController.cs`) : bloc d'envoi SMS fire-and-forget au
  moment de l'appel d'un ticket retiré.
- **Borne** (`BorneController.cs`) : normalisation/validation du numéro de
  téléphone retirée.
- **Migrations** (`DbMigrator.cs`) : les nouvelles installations ne créent
  plus les colonnes SMS. Sur une base existante, les anciennes colonnes
  restent en place mais ne sont plus utilisées (aucune perte de données,
  aucune action requise).
- **Assistant Admin** (`NoubaAiService.cs`) : la recommandation « activez les
  SMS de rappel » en cas de fort taux d'absence est remplacée par une
  suggestion d'activer le suivi mobile par QR code (fonctionnalité qui,
  elle, reste active).
- `Nouba.csproj` : commentaire sur `System.IO.Ports` mis à jour (ce paquet
  reste nécessaire pour les imprimantes ESC/POS en port série — sans lien
  avec le SMS).

## Vérifications effectuées

- Recherche exhaustive (`sms`, `Sms`, `SMS`) sur tous les fichiers `.cs` et
  `.cshtml` : aucune occurrence restante.
- Recherche des types supprimés (`ISmsProvider`, `SmsService`,
  `TwilioSmsProvider`, `HttpGatewaySmsProvider`, `GsmModemSmsProvider`,
  `NullSmsProvider`) : aucune référence orpheline.
- `PhoneNumber` confirmé comme non utilisé ailleurs que par le SMS (pas
  affiché côté agent, pas utilisé par le suivi mobile).

## À faire de votre côté

- `dotnet build` pour confirmer la compilation (je ne peux pas exécuter
  .NET dans mon environnement actuel).
- Si vous avez une base de données existante avec des tickets, aucune
  migration manuelle n'est nécessaire : les anciennes colonnes SMS restent
  simplement inutilisées.
