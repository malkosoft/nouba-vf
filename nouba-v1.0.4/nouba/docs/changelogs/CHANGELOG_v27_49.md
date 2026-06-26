# Nouba v2.7.49 — Borne : file d'attente en direct + finitions tactiles

Cette version inclut tout le travail de la v2.7.48 (indépendance Internet
totale : icônes et polices 100 % hors-ligne) et y ajoute une refonte ciblée
de la **borne**, le premier écran que voit le citoyen — celui qui crée
l'effet « waw » en démonstration commerciale.

## Nouveautés borne

### 1. Nombre de personnes en attente affiché EN DIRECT sur chaque service
Chaque tuile de service affiche désormais, en temps réel, combien de
personnes attendent pour ce service. Le badge change de couleur selon
l'affluence :
- **vert « libre »** quand personne n'attend,
- neutre quand la file est modérée,
- **ambre « chargé »** à partir de 6 personnes en attente.

Le citoyen voit ainsi d'un coup d'œil quel guichet est le moins chargé.
C'est un vrai différenciateur : la plupart des bornes concurrentes
n'affichent rien de tel.

Détails techniques :
- `BorneController.Index` calcule les compteurs par service (une seule
  requête groupée `AsNoTracking` — coût négligeable).
- Nouveau point d'accès JSON `GET /Borne/Counts` qui renvoie
  `{ "1": 0, "2": 3, "3": 8 }`.
- La borne rafraîchit les badges :
  - instantanément via SignalR (`RefreshQueue`) dès qu'un ticket est pris
    ou appelé ;
  - et toutes les 5 s en filet de sécurité si le temps réel est coupé.
- Petite pulsation du badge quand le nombre change (retour visuel de vie).

### 2. Effet d'onde au toucher (ripple)
Au toucher/clic d'une tuile, une onde lumineuse part du point de contact —
le retour tactile premium attendu sur une borne moderne.

## Compatibilité / sécurité

- Aucune logique existante modifiée : priorité (enceinte/handicap),
  multilingue FR/AR/Tz/EN, RTL arabe, overlay « création du ticket »,
  reconnexion SignalR, responsive (smartphone → écran sur pied) — tout est
  conservé tel quel.
- Le nouveau point d'accès `/Borne/Counts` est en lecture seule et sans
  suivi (aucun impact sur les performances ni la base).

## Fichiers modifiés

- `Controllers/BorneController.cs` — compteurs par service + endpoint `Counts`.
- `Views/Borne/Index.cshtml` — badge « en attente » par tuile, CSS associé,
  rafraîchissement JS en direct, effet d'onde, branchement SignalR.
- `Nouba.csproj` — version 2.7.49.

## À faire de votre côté

1. `dotnet build`.
2. Ouvrir la borne, prendre quelques tickets, en appeler depuis un poste
   agent, et vérifier que les badges « en attente » montent/descendent en
   direct et changent de couleur (vert ↔ ambre).
3. Sur écran tactile : vérifier l'effet d'onde au toucher.
4. Tester en arabe (RTL) que le badge se place bien à gauche.
