# Nouba v2.7.29 — Console Admin harmonisée (identité or / bleu nuit)

## Objectif

Poursuite de l'unification visuelle commencée en v2.7.28. Après l'écran Agent,
c'est au tour de la **console d'administration** — la vue la plus riche du
produit, et celle que tu manipules pour configurer le système.

## Ce qui change

### Console Admin alignée sur l'identité unique

L'Admin était déjà en thème sombre, mais avec un accent **violet** et une
palette de fonds différente du reste du produit, ce qui le faisait jurer avec
la Borne, l'Affichage et l'Agent.

Désormais l'Admin reprend exactement l'identité **OR (#e8b84b) sur BLEU NUIT** :

- Accent violet → or, sur toute l'interface (navigation active, boutons
  principaux, badges, curseurs, interrupteurs, barres de progression, points
  de timeline…).
- Fonds alignés sur la palette `--np-*` du design system (bleu nuit profond).
- Police passée sur Sora (titres) + Manrope (corps), comme le reste.
- Lisibilité corrigée : partout où du texte blanc se trouvait sur l'ancien
  accent foncé, il passe en texte foncé sur l'or clair (badges, boutons
  « Export CSV », « Enregistrer », tags actifs…).

L'harmonisation s'appuie sur les variables CSS déjà centralisées de l'Admin :
il a suffi de réaffecter `--accent`, `--bg`, `--surface`… vers la palette de
marque, puis de remplacer les rares couleurs violettes codées en dur.

**Aucune modification de la structure HTML ni du JavaScript de l'Admin.**
Tous les onglets, réglages, formulaires, le tableau de bord, les widgets et
le test voix fonctionnent à l'identique. Les couleurs sémantiques des
indicateurs (servis = vert, attente = jaune, appelés = bleu, absents = rouge)
sont conservées car elles portent du sens.

## Ce qui n'a PAS changé

- Aucune logique métier, aucun contrôleur, aucune base de données touchés.
- Les corrections du son (v2.7.27), l'effet wow TV et l'écran Agent premium
  (v2.7.28) restent en place.

## État de l'harmonisation premium

- ✅ Affichage TV — effet wow (v2.7.27)
- ✅ Écran Agent — thème premium (v2.7.28)
- ✅ Console Admin — identité unifiée (v2.7.29)
- ⏳ Suivi mobile (prochaine étape)
- ⏳ Pages secondaires : login agent, erreur, licence, confirmation

## Fichiers modifiés

- `Views/Admin/Index.cshtml` — palette réaffectée vers or/bleu nuit,
  lisibilité des textes sur accent, violets résiduels remplacés.
- `Nouba.csproj` — version 2.7.29.

## Vérification

- Rendu du tableau de bord Admin vérifié visuellement dans un navigateur.
- Échappement Razor (`@@media`, `@@keyframes`) contrôlé : intact.
- JavaScript de l'Admin validé syntaxiquement.
- Aucun texte blanc résiduel sur fond or (lisibilité contrôlée).
