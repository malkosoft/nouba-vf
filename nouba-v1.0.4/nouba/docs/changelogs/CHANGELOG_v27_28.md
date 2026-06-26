# Nouba v2.7.28 — Identité visuelle unifiée & écran Agent premium

## Objectif

Tu veux un produit qui respire le professionnel et la cohérence sur TOUTES
les vues, pas seulement l'écran TV. Cette version pose les fondations d'une
identité visuelle unique et l'applique à l'écran Agent, qui était le plus
incohérent du produit.

## Le vrai problème : chaque vue avait son propre style

Diagnostic objectif avant travail :

- **Polices différentes selon les écrans** : Borne en Segoe, Agent en
  monospace, Admin en system-ui, Login en Inter, Suivi en apple-system…
  C'est exactement ce qui fait qu'un produit paraît « assemblé » plutôt que
  conçu d'un bloc.
- **Couleurs d'accent dispersées** : or sur la Borne, jaune-vert sur l'Admin,
  violet sur la licence. Aucune couleur de marque unique.
- **L'écran Agent était en thème CLAIR** (fond blanc) alors que tout le reste
  du produit est en thème sombre bleu nuit. L'incohérence la plus visible.

## Ce qui change

### 1. Système de design unifié activé

Le fichier `wwwroot/css/nouba-premium.css` (identité OR `#e8b84b` sur BLEU
NUIT) existait mais n'était branché nulle part. Il est maintenant :

- Doté de ses vraies polices de marque **Sora** (titres) + **Manrope**
  (corps), chargées automatiquement, avec repli propre sur Segoe UI
  hors-ligne.
- Branché sur le layout partagé (`_Layout.cshtml`), donc disponible pour
  toutes les vues qui l'utilisent.

Une seule source de vérité : couleurs, ombres, rayons, boutons, cartes,
badges, animations. Tout part de variables `--np-*`.

### 2. Écran Agent entièrement repensé en thème premium

L'interface du guichetier — celle qui tourne toute la journée — passe du
thème clair quelconque à un **thème sombre or/bleu nuit** cohérent avec la
Borne, l'Affichage TV et l'Admin :

- Avatar agent doré, barre supérieure avec filet doré.
- KPIs élégants (en attente, traités, absents, attente moyenne, total) avec
  couleurs sémantiques lisibles sur fond sombre.
- Ticket en cours mis en valeur dans un cadre doré.
- Boutons d'action premium (« Appeler le suivant » en dégradé doré).
- File d'attente claire, prochain ticket surligné en or.

**Aucune modification du HTML ni du JavaScript** de l'Agent : seuls les
styles ont changé. Le fonctionnement (appeler, rappeler, terminer, absent,
transfert, multilingue) est strictement identique.

## Ce qui n'a PAS changé

- Aucune logique métier, aucun contrôleur, aucune base de données touchés.
- Les corrections du son (v2.7.27) et l'effet wow TV restent en place.
- Borne, Affichage TV et Login admin étaient déjà au niveau premium : ils
  n'ont pas été modifiés (hors bénéfice automatique des polices via le
  design system).

## Suite prévue (prochaines étapes)

L'harmonisation se poursuivra vue par vue, à fond et vérifiée à chaque fois :
Admin, suivi mobile, et les pages secondaires (erreur, licence, confirmation).
Travailler une vue à la fois évite les régressions et garantit un résultat
réellement fini plutôt qu'un survol de tout.

## Fichiers modifiés

- `wwwroot/css/nouba-premium.css` — chargement des polices Sora + Manrope.
- `Views/Shared/_Layout.cshtml` — branchement du design system.
- `Views/Agent/Index.cshtml` — passage complet en thème sombre premium.

## Vérification

- Rendu de l'écran Agent vérifié visuellement dans un navigateur (capture).
- Échappement Razor des `@@media` / `@@keyframes` contrôlé.
- Aucune modification de structure HTML ni de script.
