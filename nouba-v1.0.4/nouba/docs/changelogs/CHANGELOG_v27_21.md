# Nouba v2.7.21 — Version commerciale : son TV silencieux + borne tablette

## Objectif

Tu m'as demandé une version « prête à vendre » : pas d'overlay « Activer le son »,
pas de bouton « Activer le son TV » que l'utilisateur final voit, et un son qui
fonctionne sur la TV sans intervention. Plus une borne qui s'adapte aux
tablettes.

## 1. Refonte complète du système audio

### Supprimé (UX non commerciale)
- **Overlay plein écran « Touchez l'écran pour activer le son »** : retiré
  du HTML.
- **Bouton flottant pulsant 🔊** : neutralisé (la fonction existe encore
  pour ne pas casser les appels, mais ne crée plus de bouton).
- **Bouton « Son TV » dans le header** : masqué via CSS (`display:none !important`).
- **Panneau diagnostic son** orange en bas-gauche : retiré.

### Nouvelle stratégie : Web Speech d'abord sur Smart TV

Avant : Piper en priorité → fallback Web Speech si Piper échoue. Problème :
sur Smart TV (Tizen, WebOS, Android TV), Piper retourne un WAV que le navigateur
ne décode pas correctement → `audio.onerror` → l'utilisateur voyait l'overlay.

Maintenant :
- **Smart TV détectée** (`isLikelyTvBrowser` via User-Agent) :
  **Web Speech API en priorité**. Web Speech utilise la voix de synthèse du
  système TV (qui marche sans déverrouillage utilisateur sur la plupart des
  Smart TV modernes). Piper en bonus si la première fail.
- **PC / navigateur classique** : Piper en priorité (voix neurale plus belle),
  Web Speech en fallback.

`announceTicket` ne bloque **plus jamais** sur `audioUnlocked === false`.
Les annonces partent toujours, et le moteur audio le plus probable de marcher
est essayé en premier.

### `playWithSpeechSynthesis(text, lang)` — nouveau helper

Wrapper propre autour de Web Speech API avec :
- Sélection de la voix selon la langue active (FR/AR/TZ/EN).
- Garde-fou 1.5 s : si `onstart` n'est jamais déclenché, on considère que
  Web Speech a échoué et on tombe sur Piper.
- Lorsque Web Speech démarre, `audioUnlocked` bascule à true → toute logique
  qui en dépendait continue à fonctionner.

## 2. Borne responsive tablette

L'ancien CSS avait **deux blocs `@media (max-width:900px)` qui se contredisaient**.
Refondu en breakpoints alignés sur les vraies tailles d'appareils :

| Breakpoint | Pour |
|---|---|
| 1024–1280px | Tablette paysage (iPad standard, Android grand format) |
| ≤900px | Tablette portrait + petits écrans, header empilé verticalement |
| ≤520px | Smartphone portrait étroit, services en 1 colonne |
| 901px+ portrait | Tablette/écran tactile sur pied vertical |
| 901px+ et height≤760px | Laptop 13" |

Toutes les tailles utilisent désormais `clamp(min, vw-based, max)` :
- Le titre principal, le sous-titre, l'horloge, le logo client, les noms de
  services, les boutons de priorité… s'adaptent fluidement à toute taille.
- Plus de tailles figées en `px` qui débordaient sur tablette.

## 3. Borne Confirmation responsive

- Carte étendue : `max-width:min(620px, 92vw)` au lieu de 520px figé.
- Numéro de ticket en `clamp(3.5rem, 12vw, 6.5rem)` : adapté du téléphone
  à la tablette.
- Suppression des doublons CSS qui écrasaient les `clamp()`.

## Ce qui reste possible si le son ne marche toujours pas

J'ai été honnête en fin de session précédente : **je ne peux pas tester sur
ta TV**. Web Speech a de très bonnes chances de marcher sur Smart TV moderne,
mais pas garanti à 100%. Si après cette version, ta TV ne parle toujours pas :

1. Ouvre F12 → console sur la TV (ou via Chrome remote debug si kiosque).
2. Fais appeler un ticket.
3. Tu devrais voir : `[Nouba TTS] Mode Web Speech uniquement (TV).` au load,
   puis des éventuelles erreurs si Web Speech échoue.
4. Si Web Speech échoue aussi sur ta TV, la solution standard du marché est
   d'utiliser un mini-PC Android TV (50€, Chrome installé) branché HDMI au
   TV, plutôt que le navigateur intégré de la TV. C'est ce que font les
   solutions concurrentes commerciales (kiosks dédiés).

## Vérifications passées
- 60 fichiers, 0 déséquilibre syntaxique.
- HTML balances OK (Display: 45/45 div, Borne: 41/41 div, Confirmation: 18/18 div).
- Aucun changement côté serveur, contrôleurs, modèles, DB.
- Aucun input admin cassé.

## Fichiers modifiés
- `Views/Display/Index.cshtml` : overlay supprimé, panneau debug supprimé,
  bouton son header masqué, fonctions UI neutralisées, `announceTicket`
  débloqué, `playWithSpeechSynthesis` ajouté, `speakNext` revu, `initAudioGate`
  simplifié.
- `Views/Borne/Index.cshtml` : refonte responsive complète.
- `Views/Borne/Confirmation.cshtml` : carte plus large + clamp() sur titres.
- `Nouba.csproj` : 2.7.20 → 2.7.21.
- `Infrastructure/LicenseInfo.cs` : 2.7.20 → 2.7.21.
