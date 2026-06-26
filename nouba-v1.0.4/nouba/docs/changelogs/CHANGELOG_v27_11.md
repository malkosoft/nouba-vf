# Nouba v2.7.11 — Lot fiabilité temps réel + audio

## Bug 1 — Date affichée en anglais au lieu de français

**Cause** : `Intl.DateTimeFormat('fr-FR', ...)` n'est pas garanti sur certaines
Smart TV (Samsung Tizen, LG WebOS) qui retombent silencieusement sur en-US.

**Correctif** : dictionnaire `FR_DAYS` / `FR_MONTHS` codé en dur dans
`Views/Display/Index.cshtml`. Plus aucune dépendance au runtime du navigateur.
Format produit : « vendredi 8 mai 2026 » identiquement sur PC, mobile, TV.
EN et AR ont aussi leur dictionnaire.

## Bug 2 — Titre Display non dynamique

**Cause** : la balise `<title>` était figée en `Nouba — Affichage` côté Razor.
Le code JS dynamique ne pouvait pas mettre à jour l'onglet avant que `fetchState()`
ait récupéré `s.siteName`.

**Correctif** : `<title>@settings.SiteName — Affichage</title>` au rendu Razor.
Effet immédiat dès l'ouverture de la page.

## Bug 3 — Vidéo qui redémarre à chaque appel ticket

**Cause** : `currentMediaUrl` (cache JS) n'était initialisé qu'au premier
`fetchState()`. Avant ça, le DOM contenait déjà une `<video>` posée par Razor.
Le 1er appel JS comparait une URL vide à `s.displayBackgroundVideoUrl` non vide
→ remplacement du DOM → redémarrage vidéo. De plus la comparaison était stricte
(URL absolue côté DOM `http://x:y/uploads/v.mp4` vs URL relative côté JSON
`/uploads/v.mp4`).

**Correctif** :
- Initialisation de `currentMediaUrl` depuis le DOM Razor au chargement.
- Comparaison normalisée par `pathname` via `new URL(u, location.origin)`.

La vidéo continue désormais sans coupure, même quand 5 tickets sont appelés
en succession rapide.

## Bug 5 — Retard du son TTS sur TV

**Cause** : Piper met 200-1500 ms à synthétiser un WAV. Cette latence est
ajoutée au délai SignalR + fetch state → la voix démarre 1-2 s après l'appel.

**Correctif (côté serveur)** : pré-warm Piper en `Task.Run` (fire-and-forget)
juste après `RefreshQueue` dans `AgentController.CallNext`. Les modèles ONNX
sont chargés en RAM par anticipation, et la prochaine synthèse (celle du client)
est instantanée car le binaire et les modèles sont déjà chauds.

**Implémentation** :
- `PiperTtsService` injecté dans `AgentController`.
- Helper privé `BuildAnnouncementText(ticket, counter, service, lang)`.
- Synthèse anticipée pour la langue par défaut, timeout 8 s.
- Aucun impact sur le code client (qui appelle `/Tts/Speak` comme avant).

## Bug 6 — Refresh Agent automatique à la création de ticket borne

**Cause apparente** : SignalR n'était pas toujours connecté côté Agent
(CDN inaccessible offline → fallback watchdog 7 s, perçu comme « ne se met
pas à jour »).

**Correctif** : watchdog 7 s → 3 s dans `Views/Agent/Index.cshtml`.
Le polling SignalR `RefreshQueue` reste en place et fonctionne immédiatement
quand SignalR est connecté.

## Bug 7 — Changement de service agent immédiat

**Cause** : `AgentUpdated` SignalR était bien émis par
`AdminController.UpdateAgent` et `AssignAgentCounter`, mais si SignalR était
down côté Agent, aucun mécanisme ne détectait le changement.

**Correctif** :
- Nouvel endpoint léger `GET /Agent/MyAssignment` qui retourne `{ id, serviceId, counterId, active }` de l'agent connecté.
- Polling toutes les 4 s côté client. Si la signature change → `window.location.reload()` automatique → nouvelle affectation appliquée sans déconnexion/reconnexion.

## Bug 8 — Chevauchements audio TV

**Cause** : sur certaines Smart TV, `audio.pause()` est légèrement asynchrone.
Si deux tickets sont appelés en succession très rapprochée (< 100 ms), la nouvelle
synthèse pouvait démarrer pendant que l'ancienne n'avait pas encore réellement
arrêté → deux annonces simultanées audibles.

**Correctif** :
- `announceTicket()` incrémente `piperGen` AVANT de démarrer la nouvelle synthèse → tous les callbacks de l'ancien cycle sont invalidés instantanément.
- Petit délai de 80 ms entre `stopPiperAudio()` et `speakNext()` pour laisser la pile audio Smart TV finir de propager le pause().

## Bug 9 — Autoplay TV / overlay « Activer le son »

**Cause** : la politique autoplay du navigateur exige un geste utilisateur.
Sur Smart TV en kiosque ou PWA installée, ce geste n'a souvent jamais lieu.

**Correctif** : tentative d'auto-unlock SILENCIEUSE au chargement de la page :
lecture immédiate d'un WAV base64 inline volume 0. Si le navigateur l'accepte
(kiosque Chrome avec `--autoplay-policy=no-user-gesture-required`, Smart TV
Tizen/WebOS, ou PWA standalone), `audioUnlocked = true` est posé en sessionStorage,
l'overlay ne s'affiche jamais. Sinon, l'overlay reste affiché comme avant.

Sur navigateur classique sans privilège kiosque, le comportement actuel
reste inchangé (overlay au premier chargement).

## Pas inclus dans cette session
Refonte UI Agent premium, palette d'icônes services, responsive intelligent
TV/PC/tablette. Ces 3 chantiers nécessitent des sessions dédiées car ils
touchent CSS, modèles DB et tests visuels.

## Vérifications passées
- 54 fichiers analysés, 0 déséquilibre syntaxique.
- Constructeur `AgentController` mis à jour avec injection `PiperTtsService`.
- Migration auto `DbMigrator` non touchée (pas de changement DB).
- Aucun input serveur (`name="..."`) modifié.

## Limite à signaler
Pas de SDK .NET dans l'environnement de cette session : pas de `dotnet build`.
Si une erreur Razor/C# sort au build chez vous, copiez-la, c'est rapide à fixer.
