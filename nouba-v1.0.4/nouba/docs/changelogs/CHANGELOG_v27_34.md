# Nouba v2.7.34 — Apparences d'affichage : Compact retiré + Vidéo dominante corrigée

## 1. Suppression du mode « Compact »

Tu as raison : les modes « Standard » et « Compact » étaient quasi identiques
(1.18/0.82 contre 1.3/0.7 — différence à peine perceptible). Le mode Compact
est retiré de la liste des apparences dans l'Admin pour éviter la confusion.

Rétrocompatibilité assurée : si une installation avait déjà « Compact »
enregistré, elle bascule automatiquement et proprement sur « Standard »
(côté serveur et côté synchro temps réel). Aucune action requise.

Les apparences disponibles sont désormais : Standard, Plein écran, TV Wall,
Vidéo dominante, Minimal.

## 2. Mode « Vidéo dominante » corrigé

Le problème : la vidéo n'occupait qu'environ 57% de la hauteur (ratio
1.15/0.85), donc elle ne « dominait » pas vraiment l'écran — vidéo et ticket
se partageaient presque l'espace à parts égales.

Correction : la vidéo occupe maintenant environ 72% de la hauteur (ratio
2.6/1), le ticket prenant une bande basse bien lisible (~28%). La vidéo est
réellement dominante, tout en gardant le numéro de ticket et le guichet
parfaitement visibles. Les tailles de police du ticket ont été ajustées pour
rester équilibrées dans cette bande.

## Ce qui n'a PAS changé

- Aucune logique métier touchée.
- Les autres apparences (Standard, Plein écran, TV Wall, Minimal) sont
  inchangées.
- Toutes les améliorations précédentes (son fiable, 6 voix, prononciation
  arabe, carillon, effet wow, identité visuelle) restent en place.

## Fichiers modifiés

- `Views/Display/Index.cshtml` — ratio vidéo dominante corrigé, mapping
  compact→standard (résolution serveur + synchro JS).
- `Views/Admin/Index.cshtml` — option Compact retirée du sélecteur.
- `Nouba.csproj` — version 2.7.34.

## Vérification

- Rendu du mode « Vidéo dominante » vérifié visuellement : la vidéo occupe
  bien la majorité de l'écran, ticket lisible en bas.
- JavaScript de l'affichage revalidé, échappement Razor contrôlé.
- À tester de ton côté : dans l'Admin, choisis « Vidéo dominante », ajoute
  une vidéo, et vérifie sur l'écran que la vidéo prend bien la grande partie
  haute.
