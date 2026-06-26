# Nouba Pro 1.0.2 — Lot 1 : visuel affichage + sécurité borne

Date : 2026-06-25
Premier lot du chantier « retours d'usage » (15 points). Sans migration BD,
sans rien retirer de l'existant.

## Corrigé
- **#5 Logo affichage** : suppression du liseré vertical (`border-right`) qui
  créait l'artefact en bas à gauche ; le logo client est désormais présenté sur
  un « chip » blanc arrondi propre et uniforme (net quel que soit le logo).
- **#8 Ticket prioritaire** : plus aucun service présélectionné. Le client doit
  choisir explicitement (placeholder « Choisir un service »). Sans choix, la
  soumission est bloquée, le sélecteur clignote en rouge et un message clair
  s'affiche (FR/AR/EN/TZ). Évite les tickets prioritaires sur le mauvais service.
- **#11 Code service** : retiré du bouton borne (le `serviceId` reste transmis,
  aucune régression). Le bouton n'affiche plus que l'icône + le nom.
- **#13 Signe Tamazight** : le yaz ⵣ est rendu en SVG inline → toujours affiché,
  même si la police de la TV/tablette n'a pas le glyphe Tifinagh (fini le carré
  vide).

## À tester sur matériel
- Affichage TV : rendu du logo client (fond blanc ou transparent).
- Borne tablette : tenter un ticket prioritaire sans choisir de service → message ;
  bouton tamazight → symbole net.
