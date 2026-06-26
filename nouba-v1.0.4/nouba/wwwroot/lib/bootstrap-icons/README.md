# Icônes Nouba — 100 % hors-ligne

Le fichier `bootstrap-icons.min.css` de ce dossier est un **jeu d'icônes
autonome** intégré directement à Nouba :

- aucune connexion Internet requise (la promesse centrale du produit) ;
- aucune police ni CDN externe à charger ;
- chaque icône est une image vectorielle (SVG) appliquée en *masque*, donc
  elle prend automatiquement la **couleur** et la **taille** du texte autour
  d'elle, exactement comme une police d'icônes classique ;
- couvre les 86 icônes réellement utilisées par l'Admin et l'Affichage TV.

Le balisage habituel fonctionne sans aucun changement :

    <i class="bi bi-printer"></i>
    <i class="bi bi-gear"></i>

## (Optionnel) Passer aux icônes officielles Bootstrap Icons

Si vous préférez un rendu pixel-perfect identique à Bootstrap Icons, vous
pouvez remplacer le jeu intégré par la police officielle. À faire **une seule
fois, sur une machine connectée à Internet** (par ex. votre PC de
développement) — ensuite tout reste 100 % hors-ligne :

1. Télécharger la release : https://github.com/twbs/icons/releases
   (fichier `bootstrap-icons-1.11.3.zip`).
2. Dans ce dossier `wwwroot/lib/bootstrap-icons/`, remplacer :
   - `bootstrap-icons.min.css` par le `font/bootstrap-icons.min.css` de la release ;
   - copier le dossier `font/fonts/` de la release dans `wwwroot/lib/bootstrap-icons/fonts/`.
3. `dotnet build`. Les `<i class="bi ...">` continueront de fonctionner.

Tant que vous ne faites pas cette étape, le jeu intégré ci-dessus suffit
et fonctionne partout, sans Internet.
