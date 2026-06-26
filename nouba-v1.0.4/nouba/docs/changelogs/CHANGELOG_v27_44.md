# Nouba v2.7.44 — Le numéro de version affiché était figé depuis longtemps

Trouvé en creusant une fausse alerte : une capture d'écran de l'admin
montrait « V2.7.26 » alors que la version réellement installée était bien
plus récente, ce qui donnait l'impression qu'une mise à jour ne s'était pas
appliquée.

## Le problème

Il existait **deux numéros de version complètement indépendants** dans le
projet :
- `Nouba.csproj` → `<Version>`, mis à jour à chaque changelog (c'est lui qui
  apparaît dans les propriétés du fichier .exe sous Windows).
- `Infrastructure/LicenseInfo.cs` → une constante `Version = "2.7.26"`
  écrite en dur, utilisée pour afficher le numéro dans la barre latérale de
  l'admin (`V2.7.26 · ADMIN`) — et qui n'avait jamais été mise à jour depuis
  la v2.7.26, malgré près de 20 versions de corrections livrées depuis.

Aucun rapport avec un quelconque bug de fonctionnement : uniquement de
l'affichage. Mais ça crée une vraie confusion pour vérifier qu'une mise à
jour a bien été appliquée — exactement ce qui s'est passé.

## Le correctif

`LicenseInfo.Version` lit maintenant automatiquement la version depuis les
métadonnées de l'assembly (générées par MSBuild à partir de `<Version>`
dans `Nouba.csproj`) au lieu d'être une chaîne séparée à maintenir à la
main. Une seule source de vérité : le numéro affiché dans l'admin
correspondra toujours exactement à celui du `.csproj`, sans plus jamais
nécessiter de synchronisation manuelle à chaque changelog.

En bonus, j'ai retiré `LicenseInfo.ReleaseDate`, une constante inutilisée
ailleurs dans le code (vérifié par recherche exhaustive).

## Fichiers modifiés

- `Infrastructure/LicenseInfo.cs`
- `Nouba.csproj` — version 2.7.44.

## À faire de votre côté

- `dotnet build`, puis vérifier dans Admin que la barre latérale affiche
  bien « V2.7.44 ».
- Ceci ne change rien à la validation de licence elle-même (clé RSA liée à
  l'adresse MAC) : `LicenseInfo.Version` n'est utilisé que pour
  l'affichage, jamais dans la signature de licence — vérifié par recherche
  exhaustive avant modification.
