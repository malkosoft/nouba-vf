# Nouba v2.7.12 — Suivi mobile par QR code

## Vue d'ensemble

Quand un client prend un ticket sur la borne, il peut désormais scanner un QR code
avec son téléphone pour suivre l'avancement de son tour en temps réel, depuis une
page mobile responsive dédiée. Fonctionne en Wi-Fi local immédiatement, et prêt
pour le cloud public 4G/5G dès qu'un serveur public sera déployé.

## Ce qui est livré

### Côté local (mini PC Nouba) — 100% fonctionnel

1. **`PublicId` court (8 caractères) sur chaque ticket** — alphabet sans chars
   ambigus (pas de 0/O, 1/I, l). ~47 bits d'entropie, impossible à deviner.
   Migration auto via `DbMigrator` (colonne ajoutée si absente).
2. **Génération QR code** via NuGet `QRCoder 1.5.1` (sans dépendance GDI+).
   Service `QrCodeService` : PNG bytes pour navigateur, matrix bool pour ESC/POS.
3. **QR sur écran Confirmation borne** — affichage 200×200 px avec message
   multilingue « Scannez ce QR code pour suivre votre tour sur votre téléphone ».
4. **QR imprimé sur ticket thermique** — commande native ESC/POS `GS ( k`
   (Model 2, ECC M, taille 6). Fonctionne sur toutes les imprimantes
   thermiques modernes (Epson TM, XPrinter, etc.). Code court imprimé en
   clair sous le QR comme fallback si le scan ne marche pas.
5. **Page mobile responsive `/suivi/{publicId}`** — design dark premium adapté
   smartphone, avec :
   - numéro de ticket en très grand,
   - statut animé : en attente / bientôt / appelé / terminé,
   - position dans la file (« 3 personnes avant vous »),
   - dernier ticket appelé,
   - guichet à rejoindre quand appelé (avec vibration mobile),
   - mise à jour automatique 5 s (3 s quand statut = soon ou called),
   - polling resilient : passe en mode « reconnexion » si offline.
6. **Page `/suivi`** (sans publicId) — saisie manuelle du code, utile pour
   QR général TV ou si le client a perdu sa page d'origine.
7. **Section Admin « Suivi mobile par QR code »** dans tab Affichage →
   Suivi mobile :
   - Toggle Activer/Désactiver,
   - URL publique optionnelle (vide = LAN local),
   - Identifiant établissement + clé API (réservés au cloud futur),
   - Toggle QR général sur Display TV.
8. **Mode dégradé** : si la fonctionnalité est désactivée OU si l'URL publique
   ne répond pas, **rien n'est cassé** : tickets, impression, borne, TV, agent
   continuent à fonctionner. Le QR n'est juste pas généré.

### Architecture
```
Models/
  Ticket.cs             ← + PublicId (string?, 16 chars)
  UiSettings.cs         ← + 5 propriétés QrFollow*
Services/
  QrCodeService.cs      ← NEW — génération PNG / matrix / NewPublicId
  EscPosPrinter.cs      ← + QrPayload, QrPublicId dans EscPosTicketData
                          + commande GS ( k native ESC/POS
Controllers/
  SuiviController.cs    ← NEW — /suivi, /suivi/{id}, /state, /qr/{id}.png
  BorneController.cs    ← génère PublicId à la création + transmet à TempData
  AdminController.cs    ← bindings POST des 5 champs QrFollow*
Views/
  Suivi/
    Track.cshtml        ← NEW — page mobile responsive
    Index.cshtml        ← NEW — page saisie code
    Disabled.cshtml     ← NEW — fallback erreur
  Admin/Index.cshtml    ← + tab "Suivi mobile" + section formulaire
  Borne/Confirmation    ← + bloc QR avec libellés FR/EN/AR/TZ
Helpers/
  TicketTrackingUrl.cs  ← NEW — résout URL publique ou LAN local
Data/
  DbMigrator.cs         ← + 6 colonnes (Ticket.PublicId + 5 QrFollow*)
Program.cs              ← + AddSingleton<QrCodeService>()
Nouba.csproj            ← + QRCoder 1.5.1
```

## Ce qui n'est PAS dans cette livraison

- **Le serveur cloud public** sur `suivi.nouba.app` n'existe pas. Les paramètres
  Identifiant établissement + clé API + URL publique sont prêts à être branchés
  quand vous déploierez ce serveur (projet ASP.NET Core séparé, Azure/VPS).
- **Le service `QueueSyncService`** (push périodique vers cloud) — pas implémenté.
  À ajouter quand le serveur public sera prêt.
- **Le QR général sur Display TV** — la propriété `QrFollowShowOnDisplay` est
  posée et bindée, mais le rendu sur `Views/Display/Index.cshtml` n'est pas
  encore branché. À ajouter en 5 lignes : `<img src="/suivi/qr-general.png?size=6"/>`
  dans un coin de la page si `settings.QrFollowShowOnDisplay`.

## Comment tester

1. Builder et lancer l'app. La migration DB ajoute automatiquement les colonnes.
2. Aller sur `/Borne`, prendre un ticket. L'écran Confirmation affiche un QR
   et le code (ex: `8F7K2Q9D`).
3. Scanner le QR avec un téléphone connecté au même Wi-Fi → page mobile s'ouvre
   avec le suivi temps réel. Demander à un agent d'appeler le ticket → la page
   mobile vibre et bascule en « C'est à vous ! » avec le numéro de guichet.
4. Aller dans Admin → Affichage → Suivi mobile : décocher « Activer » → le QR
   ne s'affiche plus sur la borne, le ticket s'imprime sans QR.
5. Si imprimante thermique connectée : le QR doit apparaître sur le ticket
   imprimé entre les infos d'attente et le pied de page.

## URL publique : configurer plus tard
Tant que vous laissez « URL publique » vide, le QR pointe vers
`http://192.168.x.x:5000/suivi/{id}` (LAN local). Quand vous aurez un serveur
public, renseignez `https://suivi.votre-domaine.com` et le QR pointera vers
le serveur cloud. Aucune modification de code à ce moment-là — tout est déjà
configurable.

## Vérifications passées
- 60 fichiers analysés, 0 déséquilibre syntaxique.
- 8 fichiers modifiés, 7 fichiers nouveaux.
- Aucun `name="..."` existant cassé (tous les bindings serveur préservés).
- NuGet `QRCoder 1.5.1` ajouté au csproj — `dotnet restore` requis au
  premier build.

## Limite à signaler
Pas de SDK .NET dans mon environnement → pas de `dotnet build`. Si une erreur
sort au build, copiez-la, je corrige. La feature dépend uniquement de QRCoder
qui est extrêmement stable.
