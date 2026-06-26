# Nouba v2.7.19 — Refonte des 5 layouts cassés

## Diagnostic clair

5 mises en page sur 6 cachaient des éléments. Standard étant la seule
fonctionnelle, c'est elle qui sert de référence. Les autres étaient
construites avec des règles CSS trop complexes (grid-template-rows
réécrits, !important multiples, conflits de cascade).

## Approche v2.7.19 : minimum de changement vs Standard

Chaque layout cassé reprend la base Standard et n'applique que des
ajustements **minimaux** :

### Compact (tickets larges)
- **Avant** : `1.4fr / 0.6fr` → panneau droit écrasé, vidéo et historique
  superposés.
- **Après** : `1.3fr / 0.7fr` → panneau droit suffisamment large pour rester lisible.

### Plein écran
- Inchangé : `1fr` + right-col cachée. Fonctionne déjà.
  Si tu ne le voyais pas marcher, c'est probablement que la classe
  `page--large` ne s'appliquait pas (vérifier dans le panneau Admin →
  Apparence → Mise en page que tu as bien sélectionné « Plein écran »).

### TV Wall
- **Avant** : right-col en 2 lignes (média + historique) → débordement,
  l'historique en bas écrasait la vidéo sur Smart TV.
- **Après** : right-col en 1 seule zone média plein écran. L'historique
  est masqué dans ce layout (focus = ticket géant + média).

### Vidéo dominante
- **Avant** : right-col grid-row 1, left-col grid-row 2, mais sans
  hauteurs explicites → le bloc ticket débordait par le bas.
- **Après** : `height:100%` et `min-height:0` explicites sur tous les
  conteneurs concernés. Ratio 1.3fr / 1fr (vidéo plus grande que le ticket).

### Minimal (un seul ticket)
- **Avant** : tentative de réécrire `grid-template-rows` du `.table-grid` →
  conflit avec la classe `.tg-rows-3` du HTML, et les cells masquées
  par `display:none` laissaient quand même des lignes implicites vides.
- **Après** : on garde `.tg-rows-3` intact, on masque l'entête et les
  lignes secondaires, et on force la ligne primary à occuper tout
  l'espace via `grid-row:2 / span 3`. Le ticket primary remplit
  vraiment plein écran maintenant.

## Si quelque chose reste cassé

Précise-moi **par layout** :
- Quel élément est invisible (le ticket ? l'historique ? la vidéo ? le titre du haut ?)
- Sur quel écran (PC ? TV ? résolution ?)
- Photo si possible

C'est plus rapide d'itérer avec un cas précis que d'essayer de tout
deviner.

## Vérifications passées
- 60 fichiers, 0 déséquilibre syntaxique.
- Aucun input serveur cassé.
- Aucune logique JS touchée — uniquement du CSS.
