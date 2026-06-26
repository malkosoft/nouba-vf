# Nouba v2.7.13 — Fix QR borne + son TV

## Bug 1 — Le QR ne s'affichait pas sur la borne

**Cause** : conflit de routing ASP.NET Core. La route classe `[Route("suivi")]`
combinée à `[HttpGet("{publicId}")]` (page Track) et `[HttpGet("qr/{publicId}")]`
(image PNG) créait une ambiguïté. Sur certaines configurations, l'URL
`/suivi/qr/8F7K2Q` était routée vers `Track` avec `publicId = "qr"` au lieu
de `Qr` avec `publicId = "8F7K2Q"`. Résultat : la balise `<img>` recevait du
HTML (page Disabled) au lieu d'un PNG → image cassée.

**Correctif** : route renommée en `/suivi/qr-img/{publicId}` (avec et sans
`.png` final). Plus aucun chevauchement possible avec la route Track.
Vue `Confirmation.cshtml` mise à jour pour utiliser la nouvelle URL.

## Bug 2 — Le son ne marche pas sur la TV

**Cause(s)** : politique autoplay des navigateurs Smart TV (Tizen, WebOS,
Android TV) qui exige un geste utilisateur. Sur ces TV, l'utilisateur
n'a souvent qu'une télécommande, et les boutons remappés (Volume, Channel,
HOME) n'envoient pas toujours d'event clavier reconnaissable. L'auto-unlock
silencieux de v2.7.11 ne suffisait pas dans tous les cas.

**Correctifs combinés** (3 leviers) :

1. **Capture ULTRA-large des gestes** : `keydown`, `keyup`, `keypress`,
   `click`, `touchstart`, `touchend`, `pointerdown`, `mousedown` sont
   tous capturés. La condition `isTvOkKey` (qui filtrait OK/Enter/Space
   uniquement) est supprimée pour le déverrouillage. Toute touche de
   télécommande ou tap d'écran tactile débloque maintenant.

2. **Tentative d'audio même quand `audioUnlocked = false`** : avant, dès
   que `audioUnlocked` était false, on stockait juste l'annonce dans
   `pendingAnnouncements` et on attendait. Maintenant on tente quand
   même le `play()` — sur Smart TV, le navigateur l'autorise parfois
   spontanément, surtout au tout premier ticket appelé.

3. **Bascule `audioUnlocked = true` au succès du play()** : quand
   `audio.play()` réussit dans `playWithPiper`, on set immédiatement
   `audioUnlocked = true`, on cache l'overlay et on stocke le flag en
   `sessionStorage`. Plus aucune annonce suivante ne sera bloquée.

## Comment tester

- **QR** : prendre un ticket sur `/Borne`. L'image QR doit s'afficher
  immédiatement sur l'écran Confirmation. Plus aucun « Image cassée ».
- **Son TV** : ouvrir `/Display` sur la TV. Faire appeler un ticket par un
  agent. Si la TV est en kiosque ou que l'utilisateur a touché à la
  télécommande au moins une fois depuis l'ouverture, le son démarre
  immédiatement. Si vraiment rien ne marche, ouvrir F12 sur le navigateur
  TV pour voir les messages `[Nouba TTS] …` qui indiquent précisément
  ce qui bloque.

## Fichiers modifiés
- `Controllers/SuiviController.cs` : route `qr/` → `qr-img/`
- `Views/Borne/Confirmation.cshtml` : `<img src>` mis à jour
- `Views/Display/Index.cshtml` : capture étendue des gestes,
  tentative d'audio sans bloquer, bascule auto de `audioUnlocked`
