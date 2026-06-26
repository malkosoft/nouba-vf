# Nouba v2.7.16 — Champ Titre TV bien visible dans l'admin

## Le bug que tu rencontrais

Tu cherchais où changer le texte « Nouba - Gestion de file d'attente » qui
s'affiche en haut de l'écran TV, et tu ne le trouvais pas dans l'admin.

## Diagnostic

Le champ existait déjà — mais il était :
1. Planqué dans une sous-section nommée « Texte d'en-tête (écran TV) »,
   placée tout en bas du sous-onglet **Bandeau & Langue**, après les
   couleurs et le bandeau défilant.
2. **Dupliqué** dans le sous-onglet Apparence (sans label clair) ET dans
   l'onglet Borne (avec un label différent).
3. Avec un label peu explicite : « Texte d'en-tête (écran TV) » — pas évident
   que c'est ICI qu'on change « Nouba - Gestion de file d'attente ».

Donc le mécanisme fonctionnait déjà côté serveur, mais tu ne pouvais pas
trouver le champ.

## Correctif

1. **Bloc bien visible en haut du sous-onglet Apparence**, avec :
   - Cadre coloré (fond accent bleu) qui attire l'œil.
   - Titre clair : « Titre principal de l'écran TV ».
   - Note explicite : *« Par défaut : Nouba - Gestion de file d'attente.
     C'est ICI que vous le changez. »*
   - 4 champs FR/AR/TZ/EN avec drapeaux et placeholder explicite
     (« Ex : Bienvenue à l'agence Mobilis Tizi-Ouzou »).

2. **Doublon supprimé** : la sous-section « Texte d'en-tête (écran TV) »
   du sous-onglet Bandeau a été retirée. Avant, deux champs avec le même
   `name="HeaderTextFr"` étaient envoyés en POST → le serveur prenait le
   dernier, ce qui pouvait écraser tes modifications du haut.

3. **Note clarifiée** sur le champ « Nom du site » juste au-dessus :
   *« Affiché à côté du logo en haut à gauche de l'écran TV et de la borne. »*
   → Tu sais maintenant que c'est différent du Titre principal.

## Comportement après changement

Une fois que tu modifies un des 4 champs Titre principal et que tu cliques
Enregistrer, ASCII tu verras instantanément (via SignalR) le nouveau texte
en haut de l'écran TV — pas besoin de F5.

Si un champ est laissé vide, c'est le « Nom du site » qui sera utilisé
à la place (fallback).

## Vérifications passées
- 60 fichiers, 0 déséquilibre syntaxique.
- Une seule occurrence de `name="HeaderTextFr"` par formulaire (admin →
  tab Display, et admin → tab Borne séparément). Plus de conflit.
- Aucun changement côté serveur — le binding `StrN(...)` de
  `AdminController.UpdateSettings` était déjà en place.
