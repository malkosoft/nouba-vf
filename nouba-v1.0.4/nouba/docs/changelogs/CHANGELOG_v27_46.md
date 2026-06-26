# Nouba v2.7.46 — Débit vocal arabe ajustable par genre

## Changement

Le ralentissement appliqué à la voix arabe (`--length-scale` Piper) était
jusqu'ici le même (1.15, soit 15 % plus lent) pour le masculin et le
féminin. Or les deux modèles ont un débit natif différent : le féminin en
avait besoin pour rester intelligible, le masculin (déjà plus lent et
moins articulé nativement) en sortait pénible à écouter.

Le masculin arabe repasse maintenant à son débit natif (1.0), le féminin
garde le ralentissement (1.15). Voir `ResolveLengthScale` dans
`Services/PiperTtsService.cs`.

## Important — je n'ai pas pu écouter le résultat

Ce changement est une hypothèse raisonnable, pas une certitude : je n'ai
aucun moyen d'écouter de l'audio dans mon environnement. Si le masculin
reste désagréable à l'oreille même à débit normal, le souci n'est plus la
vitesse mais l'articulation elle-même — un défaut du modèle, pas un
paramètre que je peux régler par du code (voir ma réponse dans le chat
pour le contexte complet et la question sur les pistes possibles).

## Fichiers modifiés

- `Services/PiperTtsService.cs`
- `Nouba.csproj` — version 2.7.46.

## À faire de votre côté

- `dotnet build`.
- Réécouter la voix masculine arabe et comparer avec avant.
