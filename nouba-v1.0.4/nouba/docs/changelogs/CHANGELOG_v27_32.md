# Nouba v2.7.32 — Carillon doux avant l'annonce

## Nouvelle fonctionnalité : carillon d'appel

À chaque appel de ticket, un carillon doux est désormais joué juste avant
l'annonce vocale, en même temps que l'effet wow visuel. Objectif : attirer
l'attention des clients sans les déranger.

Caractéristiques :

- Deux notes douces qui montent (type sonnette d'aéroport : sol5 → do6),
  en ondes sinusoïdales (son rond, jamais strident).
- Enveloppe progressive (montée 60 ms, extinction douce) : aucun « clic ».
- Durée ~0,75 s, puis la voix enchaîne sans trou.
- Volume modéré, dérivé du volume voix réglé en admin mais plafonné bas
  pour rester discret.
- Généré en Web Audio (oscillateurs), totalement indépendant de l'élément
  audio de la voix : aucun conflit avec l'annonce.

Réglages :

- Activé par défaut.
- Pour le désactiver ponctuellement (ex. test) : ouvrir l'écran avec
  `?chime=0`, par exemple `/Display?wow=1&chime=0`.

## À propos de la voix arabe

La voix arabe Piper (kareem masculin, emirati féminin) reste moins naturelle
que le français/anglais : c'est une limite des modèles arabes libres
disponibles, pas un défaut de Nouba. Des pistes d'amélioration sont à
l'étude (réglages de prononciation, autres modèles, voix premium en ligne) ;
elles feront l'objet d'une prochaine version selon le choix retenu.

## Fichiers modifiés

- `Views/Display/Index.cshtml` — fonction `playChime()` + déclenchement
  avant l'annonce dans `speakNext`, option `?chime=0`.
- `Nouba.csproj` — version 2.7.32.

## Vérification

- Forme d'onde du carillon simulée et contrôlée : démarrage à zéro (pas de
  clic), deux notes bien présentes, pic modéré.
- JavaScript de l'écran d'affichage revalidé.
- Un échantillon `exemple-carillon.wav` est fourni pour écoute.
