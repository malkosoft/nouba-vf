# Nouba Pro — v2.7.58

Date : 2026-06-24
Type : corrections de finition (icônes, voix, rôle client, ergonomie agent)

## 1. Icônes affichées en points / carrés — CORRIGÉ (cause racine)

Le jeu d'icônes offline avait un bug d'encodage : les couleurs étaient
doublement encodées (`%2523` au lieu de `%23`), ce qui rendait `stroke="%23000"`
invalide. Résultat : toutes les icônes au trait étaient invisibles ou
réduites à un point, et les icônes non définies apparaissaient en carré plein.
Correction : `%2523` → `%23` sur les 86 icônes + ajout de `bi-pause-circle`.
Toutes les icônes s'affichent désormais correctement, partout dans le logiciel.

## 2. Voix masculine / féminine pour toutes les langues — CLARIFIÉ

Le moteur Piper gère déjà une voix **masculine ET féminine** pour le français,
l'anglais et l'arabe (le français via deux locuteurs dans un seul modèle). Le
message d'aide était faux et laissait croire que FR/EN n'avaient qu'une voix
féminine. Texte corrigé :

- Le choix Masculin / Féminin s'applique à **FR, EN et AR**.
- Le **tamazight** a une voix unique.
- Les voix masculines nécessitent le modèle Piper correspondant ; l'onglet
  **Diagnostic** indique précisément lesquels sont installés et lesquels ajouter
  (ex. `fr_FR-upmc-medium`, `en_US-ryan-medium`, `ar_JO-kareem-medium`).

> Aucune voix n'est « inexistante » : il suffit de déposer le `.onnx` (+ `.json`)
> correspondant dans `wwwroot/tts/piper`.

## 3. Compte CLIENT : plus aucune trace de l'imprimante

La carte « statut imprimante » du tableau de bord restait visible pour le
client. Elle est désormais réservée au **fournisseur** (la grille d'indicateurs
passe proprement de 3 à 2 cartes pour le client).

## 4. Page agent : plein écran, sans ascenseur

La page agent débordait et obligeait à faire défiler pour voir les boutons ou la
file. Elle tient maintenant **dans l'écran** (en-tête + stats compacts, grille
qui occupe le reste). L'agent voit tout d'un coup d'œil — numéro en cours,
boutons (Appeler / Rappeler / Terminer / Absent) et file d'attente — et seule la
**liste d'attente** défile en interne. Validé sans scroll en 1280×720, 1366×768
et 1920×1080. Sur petit écran (< 901 px), le défilement normal est conservé.

## Fichiers modifiés

- `wwwroot/lib/bootstrap-icons/bootstrap-icons.min.css` (encodage + bi-pause-circle)
- `Views/Admin/Index.cshtml` (note voix, carte imprimante client)
- `Views/Agent/Index.cshtml` (layout plein écran)
- `Nouba.csproj` (2.7.57 → 2.7.58)

## Vérifications

- Icônes : 0 occurrence `%2523`, aucune icône utilisée non définie, rendu
  confirmé via navigateur.
- Admin : `<div>` 551/551, 7 blocs `@if (isProvider)`, JS de toutes les vues OK.
- Agent : CSS équilibré, page sans scroll confirmée par rendu réel.
