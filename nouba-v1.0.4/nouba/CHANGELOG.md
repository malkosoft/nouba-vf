# Nouba Pro — Journal des versions

Version actuelle : **1.0.4** (2026-06-25)

## 1.0.4 — Lot 2 (2/2) : son TV + fluidité
- #4 Son TV : réveil AudioContext sur geste (piste Hisense), repli bip hors-réseau,
  message d'échec précis, diagnostic VISIBLE à l'écran après un geste.
- #2 Fluidité : retrait du backdrop-filter:blur plein écran de l'effet wow.
  Détail : docs/changelogs/CHANGELOG_v1_0_4.md.

## 1.0.3 — Lot 2 (1/2) : temps réel SignalR
- #6 Réaffectation agent en temps réel (session relue en BD + écoute AgentUpdated).
- #10 Services à jour en direct (nouvel événement ServicesChanged → borne/affichage/agent).
  Détail : docs/changelogs/CHANGELOG_v1_0_3.md.

## 1.0.2 — Lot 1 : visuel affichage + sécurité borne
- #5 Logo affichage : liseré/artefact supprimé, logo sur chip blanc propre.
- #8 Ticket prioritaire : aucun service par défaut, choix obligatoire + message.
- #11 Code service retiré du bouton borne.
- #13 Signe tamazight ⵣ en SVG (plus de carré vide). Détail : docs/changelogs/CHANGELOG_v1_0_2.md.

## 1.0.1 — Correctifs admin (voix arabe UI, diagnostic, points)
- Arabe aligné sur FR/EN : un seul bouton « Tester AR », genre via le sélecteur ;
  carte d'état « Arabe » unique.
- Voix navigateur au lieu de Piper : doublon `testVoice` supprimé ; le bouton
  Tester affiche la cause exacte (modèle absent / synthèse échouée / lecture auto
  bloquée). Souvent : Piper génère le son mais le navigateur bloque la lecture.
- Points/taches : icônes décoratives en tête de titres/labels/onglets masquées
  (CSS ciblé, boutons préservés). Détail : `docs/changelogs/CHANGELOG_v1_0_1.md`.

## 1.0.0 — Première version commerciale
- Voix : page de réglage interne `/tts-tuning.html` (vitesse en direct +
  bascule tashkeel arabe, comparaison A/B) ; `/Tts/Speak` accepte `lengthScale`
  et `strip` en option ; helper de retrait des voyelles arabes.
- Stabilisation : version alignée en 1.0.0 partout (badge `v1.0.0`), lecture de
  version durcie, réglages TTS hérités documentés comme inertes.
- Exécutable : `publish-windows.bat` (self-contained single-file win-x64) +
  guide `docs/BUILD_EXE.md`.
- Détail complet : `docs/changelogs/CHANGELOG_v1_0_0.md`.

## 2.7.58 — Finitions (icônes, voix, rôle client, agent)
- Icônes : correction du bug d'encodage qui les affichait en points/carrés.
- Voix : message corrigé — masculin + féminin pour FR/EN/AR, voix unique pour TZ.
- Client : carte imprimante du tableau de bord réservée au fournisseur.
- Agent : page plein écran sans ascenseur, tout visible d'un coup d'œil.

## 2.7.57 — Allègement des textes Admin
- Panneau d'administration épuré (verbeux et redondances retirés, essentiel
  conservé). Écrans client inchangés (déjà minimalistes).

## 2.7.56 — Nouveau logo professionnel
- Refonte du logo (concept file d'attente, exécution nette et moderne) et de
  tous les formats (icône, favicon, wordmark). Remplacement direct, sans
  changement de code.

## 2.7.55 — Rôles administrateur (client / fournisseur)
- Séparation RBAC : le client gère le métier, le fournisseur garde la technique
  (imprimante, réseau, licence, diagnostics) + la gestion des comptes.
- 1er compte d'installation = fournisseur ; installations existantes migrées en
  fournisseur (aucun accès perdu).
- Détail : `CHANGELOG_v27_55.md`.

## 2.7.54 — Corrections écran Display
- Panneau « Diagnostic son » masqué en production (mode `?debug=1`).
- Voix navigateur arabe : repli en français quand aucune voix arabe installée.

## 2.7.53 — Revue d'ingénierie senior + durcissement
- Revue statique complète ; `Ai/Translate` réservé à l'admin ; livrable nettoyé.

## Historique
Journaux v2.5 → v2.7.54 archivés dans `docs/changelogs/`.

---
© 2026 Nouba Software. Tous droits réservés.
