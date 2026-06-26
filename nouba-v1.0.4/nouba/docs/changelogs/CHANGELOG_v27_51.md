# Nouba v2.7.51 — Borne : délai d'attente estimé par service

Complète la file d'attente en direct (v2.7.49) : chaque service de la borne
affiche désormais, sous son nom, une **estimation du temps d'attente** pour
quelqu'un qui prendrait un ticket maintenant.

## Fonctionnement

- Un bandeau « ≈ X min d'attente » apparaît sous chaque service **dès qu'au
  moins une personne attend**, et disparaît quand le service est libre.
- Le calcul est cohérent avec celui de la création de ticket :
  `personnes en attente × 4 min ÷ guichets actifs du service`, arrondi au
  supérieur (minimum 1 min). Plus il y a de guichets ouverts, plus le délai
  estimé baisse.
- La valeur se met à jour **en direct** en même temps que le compteur :
  instantanément via SignalR (ticket pris ou appelé) et toutes les 5 s en
  filet de sécurité.
- Présenté volontairement comme une **estimation** (symbole « ≈ ») pour ne
  pas créer d'attente ferme : si un guichet ferme, le citoyen comprend que
  c'est indicatif.

Combiné au compteur « en attente » (vert/ambre), le citoyen voit d'un coup
d'œil **quel service est le plus rapide** — un argument de vente fort, rare
chez les bornes concurrentes.

## Détails techniques

- `BorneController.Index` : calcule les guichets actifs par service puis
  l'estimation par service (deux requêtes groupées `AsNoTracking`).
- `GET /Borne/Counts` renvoie désormais, par service, `{ "w": attente, "e": minutes }`
  au lieu du simple nombre. Le JavaScript reste rétro-compatible avec
  l'ancien format par sécurité.
- `Views/Borne/Index.cshtml` : bandeau d'estimation (avec icône sablier
  offline), CSS associé, et mise à jour live `applyEta()`.

## Fichiers modifiés

- `Controllers/BorneController.cs`
- `Views/Borne/Index.cshtml`
- `Nouba.csproj` — version 2.7.51.

## À faire de votre côté

1. `dotnet build`.
2. Prendre plusieurs tickets sur un même service sans les appeler : le délai
   estimé doit monter (≈ 4, 8, 12 min…).
3. Ouvrir un 2ᵉ guichet pour ce service (côté Admin) : le délai estimé doit
   être divisé par deux.
4. Appeler/terminer des tickets : compteur et délai redescendent en direct.
5. Vérifier en arabe (RTL) que le bandeau s'affiche correctement.
