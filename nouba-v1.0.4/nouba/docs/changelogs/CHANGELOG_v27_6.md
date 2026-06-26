# Nouba v2.7.6 — Borne plus rapide, popup native remplacée

## Bug 1 : popup native moche pour les tickets prioritaires

Sur les boutons « Enceinte » et « Handicap », le code utilisait `confirm()`
JavaScript natif → la popup affichait `127.0.0.1:5000 indique` (URL technique
visible côté client) avec des boutons OK/Annuler du système d'exploitation.
Pas professionnel.

**Correctif** : remplacement par une **modale custom intégrée** à la borne :
- Fond flouté avec animation de fondu.
- Carte centrale avec icône (🤰 ou ♿), titre traduit (FR/EN/AR), texte
  d'explication et 2 boutons Annuler / Confirmer.
- Style cohérent avec le reste de la borne (gradient or pour Enceinte,
  bleu pour Handicap).
- Animation pop à l'ouverture, clic en dehors = annuler, échap aussi.
- Aucune référence à l'URL technique du serveur.

## Bug 2 : lenteur perçue après le clic

L'utilisateur cliquait sur un service et restait jusqu'à 5 secondes sur
l'écran d'accueil avant de voir le ticket. Cause identifiée dans
`BorneController.CreateTicket` :

L'impression ESC/POS était `await`ée AVANT le redirect, avec un timeout
jusqu'à `PrinterTimeoutMs + 500 ms` (= ~5.5 s par défaut). Donc si
l'imprimante était lente / éteinte / sur le réseau, la réponse HTTP
attendait jusqu'à ce timeout avant de rediriger vers `/Borne/Confirmation`.
Le ticket était bien en DB, mais le client voyait une borne « gelée ».

**Correctif côté serveur** :
- L'impression ESC/POS passe en **fire-and-forget** : `Task.Run(...)`,
  pas de `await`. La réponse HTTP redirige immédiatement (< 200 ms) vers
  Confirmation, et l'imprimante reçoit l'ordre en parallèle.
- `TempData["EscPosOk"] = "queued"` informe la vue Confirmation que
  l'impression est en cours plutôt qu'achevée.
- Vue `Confirmation.cshtml` étendue : nouveau message « Impression du
  ticket… » (vert clair) pour le statut `queued`.
- Le retour automatique à l'accueil (4 s en mode escpos pur) marche aussi
  pour `queued`.
- Les erreurs d'impression sont toujours loguées côté serveur (`_logger`).

**Correctif côté UI borne** :
- Au moment du submit (clic sur un service ou confirmation prioritaire),
  affichage instantané d'un overlay **« Création du ticket en cours… »**
  avec spinner animé, en plein écran.
- Multilingue (FR/EN/AR/TZ).
- Si la modale SMS optionnelle est attendue, on n'affiche PAS l'overlay
  loading prématurément — l'overlay se déclenche au moment où l'utilisateur
  finalise le SMS (Skip ou Confirmer).

## Comment tester
1. Cliquer sur le bouton **Enceinte** (priorité) → la modale custom doit
   s'ouvrir, plus aucune popup `127.0.0.1:5000`. Confirmer → overlay
   « Création du ticket… » → écran Confirmation.
2. Cliquer sur un service standard → overlay loading instantané, écran
   Confirmation en moins de 500 ms même si l'imprimante est éteinte.
3. Confirmation : message « Impression du ticket… » (orange) si ESC/POS
   actif, retour automatique à l'accueil après 4 s.

## Conservé tel quel
Tickets, langues, CSV, IA admin, agents, guichets, services, monitoring
imprimante, licence, SMS, voix Piper, presets thème (8 secteurs), 6 mises
en page, sliders voix.
