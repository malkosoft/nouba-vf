# Nouba v2.7.37 — Affichage (appels précédents + QR), retrait SMS, ticket éco

Quatre corrections demandées.

## 1. Appels précédents enfin visibles sur l'affichage

Le dernier appel (le plus récent) avait un fond CLAIR hérité de l'ancien
thème : sur le nouveau fond sombre, le texte blanc dessus devenait invisible.
Corrigé : le dernier appel a maintenant un fond doré translucide avec un
numéro de ticket doré bien lisible, cohérent avec le thème premium.

## 2. QR code sur l'affichage

- Le badge QR a été adapté au thème sombre (fond clair scannable + cadre
  doré, ombre nette) pour bien ressortir.
- Surtout : ajout d'un avertissement clair dans l'Admin. Le QR n'apparaît sur
  l'écran QUE si les DEUX cases sont cochées : « Activer le suivi mobile par
  QR code » ET « Afficher un QR général sur l'écran TV ». C'était la source
  de confusion (une seule case ne suffit pas). Pensez aussi à recharger
  l'écran d'affichage après changement.

## 3. Notifications SMS retirées

La fonctionnalité SMS est retirée de l'interface :
- Onglet « SMS notifications » supprimé de la navigation Admin.
- Modale « recevoir un SMS » retirée de la borne.

Le code backend SMS reste présent (non utilisé) pour ne rien casser, mais
plus rien n'est visible ni proposé à l'utilisateur.

## 4. Ticket imprimé optimisé (économie d'encre/chaleur et de papier)

Sur imprimante thermique, « moins d'encre » = moins de points noirs chauffés.
Optimisations :
- Suppression des grosses lignes de séparation pleines (`======`, `------`)
  très consommatrices de noir.
- Gras retiré partout où il n'est pas essentiel (entête, détails, pied).
- Entête du site en taille normale (plus en double hauteur/largeur).
- QR allégé : taille de module 5 (au lieu de 6) et correction d'erreur L (au
  lieu de M) → moins de modules noirs, toujours parfaitement scannable.
- Moins de sauts de ligne et avance papier réduite avant la coupe.

Le numéro de ticket reste imprimé en GRAND (information vitale).

## Fichiers modifiés

- `Views/Display/Index.cshtml` — fond du dernier appel + badge QR thème sombre.
- `Views/Admin/Index.cshtml` — onglet SMS retiré, avertissement QR.
- `Views/Borne/Index.cshtml` — modale SMS désactivée.
- `Services/EscPosPrinter.cs` — ticket optimisé (encre + papier).
- `Nouba.csproj` — version 2.7.37.

## Vérifications

- Équilibre syntaxique C# contrôlé, JS Admin/Display revalidés, Razor OK.
- À faire de ton côté : `dotnet build`, puis tester l'affichage (appels
  précédents + QR), imprimer un ticket de test, et vérifier que le SMS a
  bien disparu.
