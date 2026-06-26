# Nouba v2.7.48 — Audit ingénieur + indépendance Internet totale

Objectif : rendre la version réellement « prête prototype / démo de vente »
en supprimant les dépendances qui contredisaient la promesse **100 % offline**
et qui dégradaient l'interface professionnelle.

## Problèmes corrigés (défauts bloquants pour une démo sans Internet)

### 1. Icônes absentes sur l'Admin et l'Affichage TV (défaut majeur)
Les écrans Admin et Affichage utilisent **86 icônes différentes** (171 au
total) — mais la police d'icônes n'était chargée **nulle part** côté Admin/TV.
La seule balise de chargement pointait vers un **CDN Internet** (jsdelivr) et
se trouvait sur la borne, qui n'utilise aucune icône. Résultat : sur les deux
écrans les plus regardés (le tableau de bord que configure l'acheteur, et la
TV vue par tout le public), **toutes les icônes étaient invisibles**, en ligne
comme hors-ligne.

**Correctif :** un jeu d'icônes vectoriel **autonome et 100 % offline** est
désormais intégré dans `wwwroot/lib/bootstrap-icons/bootstrap-icons.min.css`.
Technique de masque CSS : chaque icône hérite automatiquement de la couleur et
de la taille du texte, comme une police, mais sans aucune dépendance réseau.
Le balisage existant `<i class="bi bi-...">` fonctionne sans modification.
(Voir `wwwroot/lib/bootstrap-icons/README.md` pour, en option, basculer vers la
police officielle Bootstrap Icons pixel-perfect.)

### 2. Polices Google chargées depuis Internet (7 emplacements)
`_Layout`, `Admin/Login`, `Agent/Login`, `License/Activate`, `Suivi/Track` et
`nouba-premium.css` chargeaient Inter / Manrope / Sora depuis
`fonts.googleapis.com`. Sur une machine offline : texte qui clignote, polices
incohérentes d'un écran à l'autre, et légère latence au chargement.

**Correctif :** suppression de **toutes** les références distantes. Typographie
unifiée sur une pile **système native** (Segoe UI sous Windows), avec prise en
charge de l'arabe (Tahoma / Noto Sans Arabic en repli). Rendu identique et
instantané sans Internet, et cohérent sur l'ensemble du produit.

## Vérifications effectuées (audit statique, niveau ingénieur)

- Accolades équilibrées sur les **42 fichiers C#** : OK.
- Toutes les propriétés `UiSettings` référencées par les contrôleurs existent : OK.
- Les 86 classes d'icônes utilisées dans les vues sont toutes définies : OK.
- Chaque icône est un SVG syntaxiquement valide (rendu vérifié visuellement) : OK.
- Plus **aucune** référence à fonts.googleapis / gstatic / jsdelivr / cdnjs / unpkg.

## Fichiers modifiés

- `Views/Shared/_Layout.cshtml`, `Views/Admin/Login.cshtml`,
  `Views/Agent/Login.cshtml`, `Views/License/Activate.cshtml`,
  `Views/Suivi/Track.cshtml`, `wwwroot/css/nouba-premium.css` (polices).
- `Views/Admin/Index.cshtml`, `Views/Display/Index.cshtml`,
  `Views/Borne/Index.cshtml` (lien icônes local).
- `wwwroot/lib/bootstrap-icons/bootstrap-icons.min.css` (+ `README.md`) : nouveau jeu d'icônes offline.
- `Nouba.csproj` — version 2.7.48.

## À faire de votre côté

1. `dotnet build` (la base de code C# n'a pas été modifiée — aucun risque de régression backend).
2. Couper le Wi-Fi/Ethernet de la machine, puis ouvrir Borne, Affichage, Agent,
   Admin et Login : vérifier que **les icônes et les polices s'affichent
   parfaitement sans Internet**.
3. Vérifier l'arabe (RTL) sur la borne et l'affichage.
