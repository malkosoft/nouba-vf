# Nouba Pro — v2.7.57

Date : 2026-06-24
Type : allègement des textes du panneau Admin (niveau modéré)

## Objectif

Rendre le panneau d'administration plus professionnel et plus simple à lire,
en retirant le verbeux et les redondances, tout en gardant l'essentiel des
repères (les avertissements techniques importants sont conservés).

## Changements (textes raccourcis ou retirés)

- Pointeur redondant « Pour changer couleurs/logo… » → formulation courte.
- Aide « titres borne / bandeau TV » : phrase plus courte et scannable.
- Aide délai imprimante : reformulée en une phrase.
- Aide « comptes administrateurs » : condensée.
- Sous-titre Assistant IA : retrait du marketing (« Analyse intelligente »).
- Aide « préréglages secteur » : condensée.
- Aide « voix locale » : condensée.
- Aide « QR général écran TV » : condensée.
- Aide « titre écran TV » : phrase unique.
- Aide « suivi QR mobile » : deux phrases → une.

## Conservé volontairement

- Avertissement imprimante USB (nom exact Windows + bouton Détecter).
- Aides de configuration réseau (URL locale vs domaine public).
- Avertissement « Zone dangereuse » (actions irréversibles).
- Tous les libellés et sous-titres courts qui orientent l'utilisateur.

## Écrans client

Aucun changement : borne, confirmation et écran TV étaient déjà minimalistes.

## Fichiers modifiés

- `Views/Admin/Index.cshtml`
- `Nouba.csproj` (2.7.56 → 2.7.57)

## Vérifications

- Équilibrage `<div>` (551/551) et `<p>` (21/21) : OK.
- 6 blocs `@if (isProvider)` intacts ; syntaxe JS de toutes les vues : OK.
