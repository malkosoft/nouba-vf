# Nouba v2.7.45 — Ticket borne rogné à l'impression (texte coupé à gauche)

Corrige le bug visible sur capture d'écran : à l'impression, le ticket
n'affichait que la fin de chaque ligne (ex. « ...uzou » au lieu du nom du
site, « ...26 15:28 » au lieu de la date complète), comme si tout le
contenu était décalé hors de la page sur la gauche.

## Diagnostic

Deux manques dans le CSS d'impression du ticket
(`Views/Borne/Confirmation.cshtml`) :

1. **Aucune taille de page définie** (`@page`). Le navigateur utilisait
   donc le papier par défaut du pilote d'imprimante sélectionné (souvent
   A4/Lettre pour une imprimante bureau ou « Microsoft Print to PDF »)
   plutôt qu'un vrai rouleau thermique étroit — d'où la grande page
   presque vide visible sur la capture, avec le ticket réduit au centre.

2. **Pas de direction figée pour l'impression.** Le ticket hérite de
   `dir="rtl"` quand il est créé en arabe. Sans direction forcée, tout
   contenu plus large que la zone imprimable (un nom de site un peu long,
   une date complète) était rogné du **côté gauche** au lieu du droit —
   on perd alors le DÉBUT de la ligne plutôt que la fin. C'est exactement
   la signature du bug montré en capture.

## Correctif

- Ajout d'une règle `@page { size: 72mm auto; margin: 0; }` : la page
  imprimée correspond désormais toujours à la largeur d'un ticket
  thermique, quel que soit le pilote d'imprimante par défaut du poste.
- La carte du ticket est désormais forcée en `direction: ltr` à
  l'impression, quelle que soit la langue du ticket. Le texte arabe
  lui-même reste parfaitement lisible (l'algorithme bidi Unicode gère ça
  indépendamment de cette propriété), seule la mise en page en bloc
  (sens du débordement, ordre des lignes service/valeur) est figée en
  LTR pour éviter ce rognage.

## Important — à confirmer de votre côté

Je n'ai pas pu reproduire ni tester ce rendu (pas de navigateur ni
d'imprimante dans mon environnement) : mon diagnostic s'appuie sur la
lecture du CSS et la correspondance très précise entre le motif observé
(toujours la FIN du texte conservée, jamais le début) et le comportement
connu du rognage en contexte RTL. Après mise à jour, merci de tester
l'impression d'un ticket en français ET un ticket en arabe, pour confirmer
que les deux s'impriment maintenant en entier.

## Fichiers modifiés

- `Views/Borne/Confirmation.cshtml`
- `Nouba.csproj` — version 2.7.45.

## À faire de votre côté

- `dotnet build`.
- Imprimer un ticket test dans chaque langue disponible sur la borne.
