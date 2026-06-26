# Nouba v2.7.47 — La vraie cause du ticket incomplet à l'impression

Les correctifs précédents (v2.7.45, taille de page + sens d'écriture)
n'étaient qu'une coïncidence partielle. La vraie cause, confirmée cette
fois par lecture exhaustive de tout le CSS du fichier (pas seulement le
bloc d'impression) :

## Le problème

Le ticket a des animations d'entrée sur l'écran de confirmation : le
numéro qui apparaît en grossissant, les lignes (service, date, attente,
délai estimé) qui montent une par une avec un délai échelonné (0,45s,
0,55s, 0,65s, 0,75s...), l'instruction qui apparaît encore après. Un
commentaire dans le code disait explicitement que ces animations étaient
*« écran uniquement, jamais à l'impression »* — mais ce n'était qu'une
intention, jamais réellement appliquée. La seule règle qui désactivait
ces animations concernait les visiteurs ayant coché « réduire les
animations » dans les paramètres d'accessibilité de leur système — rien
n'était prévu pour l'impression elle-même.

Résultat concret : `window.print()` se déclenche entre 600 ms et 2,5
secondes après l'affichage du ticket (le temps que le QR code charge).
Les animations des lignes durent jusqu'à 1,25 seconde, celle de
l'instruction jusqu'à 1,35 seconde. Si l'impression se déclenche avant
qu'une ligne ait commencé son animation, cette ligne est encore à son
état de départ : invisible (opacité à 0). Le navigateur imprime alors la
page strictement comme elle apparaît à cet instant précis — avec des
blocs entiers invisibles. Comme le moment exact dépend de la vitesse de
la machine et du temps de chargement du QR code, le résultat variait
d'une impression à l'autre : parfois tout y était, parfois des lignes
entières manquaient. C'est aussi très probablement ce qui expliquait le
texte qui semblait « tronqué » sur les captures précédentes (un élément
encore à mi-chemin de son animation d'agrandissement/déplacement).

## Le correctif

Le bloc d'impression force maintenant explicitement tous les éléments
animés à leur état final (visibles, sans transformation), quel que soit
le moment où l'impression se déclenche : plus de dépendance au timing.
Les décorations purement visuelles (halo doré, anneau autour du numéro,
étincelles) sont en plus explicitement masquées à l'impression — elles
n'ont jamais eu leur place sur un ticket imprimé en noir et blanc.

## Fichiers modifiés

- `Views/Borne/Confirmation.cshtml`
- `Nouba.csproj` — version 2.7.47.

## À faire de votre côté

- `dotnet build`.
- Imprimer plusieurs tickets de suite (français et arabe) et vérifier que
  toutes les lignes (service, date, attente, délai estimé, instruction)
  sont systématiquement présentes, à chaque essai.
