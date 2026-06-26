# Nouba Pro — v2.7.56

Date : 2026-06-24
Type : identité visuelle — nouveau logo professionnel

## Logo

Refonte du logo, en conservant le concept « file d'attente » (des personnages
en file, reconnaissable pour un système de gestion de file), mais avec une
exécution nette et moderne :

- Géométrie propre : têtes circulaires + corps en arche ∩ à épaisseur régulière,
  bords francs (fini le rendu flou/clipart précédent).
- Palette harmonisée : bleu roi, sarcelle, vert, ambre — versions assainies des
  couleurs d'origine, pour garder la reconnaissance de marque.
- Lisible sur fond clair ET sombre (barre latérale admin navy comprise).

Assets régénérés (remplacement direct, mêmes noms — aucun changement de code) :
`nouba-icon.png` (512), `nouba-icon-512/256/128.png`, `favicon.png` (64),
`nouba-logo.png` (wordmark horizontal « Nouba » + pastille « PRO »).

## Fichiers modifiés

- `wwwroot/images/nouba-icon.png`
- `wwwroot/images/nouba-icon-512.png`
- `wwwroot/images/nouba-icon-256.png`
- `wwwroot/images/nouba-icon-128.png`
- `wwwroot/images/favicon.png`
- `wwwroot/images/nouba-logo.png`
- `Nouba.csproj` (2.7.55 → 2.7.56)

## Note — allègement des textes

Audit des écrans réalisé : les écrans **client** (borne, confirmation, affichage
TV) sont déjà minimalistes (titre court + numéro), donc déjà « simples ». La
densité de texte est dans le panneau **admin** (textes d'aide). Un allègement
ciblé y est possible mais dépend de ce que l'intégrateur juge utile ; il sera
fait sur indication des écrans/sections à simplifier, pour ne pas retirer des
repères nécessaires.
