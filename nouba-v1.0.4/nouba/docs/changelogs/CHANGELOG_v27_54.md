# Nouba Pro — v2.7.54

Date : 2026-06-23
Type : corrections écran Display (diagnostic son + voix navigateur arabe)

## Contexte

Retours terrain depuis le site Air Algérie — Tizi-Ouzou (écran Display en
arabe, voix locale Piper non installée sur le poste de test) :

1. Le panneau « Diagnostic son » apparaissait en bas-gauche de l'écran TV en
   exploitation normale.
2. En arabe avec repli sur la voix du navigateur, aucune annonce vocale.

## Corrections

### 1. Panneau « Diagnostic son » masqué en production
Le panneau n'avait pas été réellement neutralisé : il se ré-affichait dès la
première erreur TTS. Il est désormais **réservé au mode debug** (URL `?debug=1`
ou `localStorage NOUBA_DEBUG=1`). En exploitation normale, rien ne s'affiche à
l'écran ; les messages partent toujours dans `console.warn` (F12) pour
l'installateur.

- Pour diagnostiquer sur site sans clavier/F12 : ouvrir l'écran avec
  `…/display?debug=1`.

### 2. Voix navigateur en arabe : annonce de secours audible
La plupart des postes Windows n'ont **aucune voix arabe** installée. Le code
retombait alors sur une voix latine (française/anglaise) à qui l'on demandait
de prononcer du texte en caractères arabes → silence ou erreur du moteur.

Désormais, quand l'écran est en arabe **et** qu'aucune voix arabe n'est
disponible **et** que Piper est absent, l'annonce de secours est faite **en
français** (compris en Algérie) plutôt que de rester muette. Nouveau helper
`browserHasArabicVoice()` ; repli construit via `buildAnnouncement(..., 'fr')`.

> Pour une **vraie voix arabe** (et la meilleure qualité), installer les
> modèles Piper `.onnx` dans `wwwroot/tts/piper` : la voix locale offline
> arabe est alors utilisée en priorité, sans dépendre du navigateur.

## Fichiers modifiés

- `Views/Display/Index.cshtml`
  - `noubaSoundLog` : panneau diagnostic gated sur `window.NOUBA_DEBUG`.
  - `browserHasArabicVoice()` : nouveau helper.
  - `speakNext` : repli arabe → français quand aucune voix arabe navigateur.
- `Nouba.csproj` : version 2.7.53 → 2.7.54.

## Vérifications effectuées

- Syntaxe JavaScript de toutes les vues `.cshtml` : OK (`node --check`).
- Aucune régression sur le rendu DOM sûr du Display (toujours 0 `innerHTML`).

## À valider sur site

- Écran Display en arabe **sans** modèles Piper : l'annonce doit sortir en
  français et le panneau diagnostic doit rester invisible.
- Écran Display en arabe **avec** modèles Piper installés : voix arabe locale.
- `?debug=1` : le panneau « Diagnostic son » réapparaît (outil installateur).
