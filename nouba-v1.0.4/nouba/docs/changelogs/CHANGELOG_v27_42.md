# Nouba v2.7.42 — Reconnexion temps réel auto-réparante (Display / Agent / Borne)

Premier correctif de stabilité dans le cadre du chantier « zéro bug » :
une vraie panne de fiabilité à long terme sur le canal temps réel (SignalR),
qui alimente notamment les annonces vocales de l'écran TV.

## Le problème

Les 3 écrans qui écoutent SignalR pour être notifiés en direct (Display,
Agent, Borne) utilisaient tous `withAutomaticReconnect([0,1000,3000,5000,10000])`.
Ce mécanisme intégré retente 5 fois sur ~19 secondes, puis **abandonne
définitivement** si la coupure dure plus longtemps (redémarrage serveur un
peu long, coupure Wi-Fi, redémarrage de box internet...). Aucun code ne
relançait la connexion après cet abandon : il fallait recharger la page
manuellement pour retrouver le temps réel.

Impact concret par écran :
- **Affichage TV** : les annonces continuaient d'être dites (le polling de
  secours prend le relais), mais avec jusqu'à 3 secondes de retard au lieu
  d'être instantanées — et ce, silencieusement, pour le reste de la session.
- **Agent** : un watchdog de 3 s prenait le relais (donc pas de blocage),
  mais avec la même perte de réactivité immédiate.
- **Borne** : impact minime, un rechargement automatique a lieu toutes les
  60 s de toute façon.

Ce n'est pas une panne totale (chaque écran a un filet de secours), mais
c'est exactement le genre de dégradation silencieuse qui donne l'impression
d'un outil « qui marche un coup sur deux » avec le temps.

## Le correctif

Sur les 3 écrans, un gestionnaire `conn.onclose()` relance désormais une
nouvelle tentative de connexion toutes les 15 secondes, indéfiniment,
jusqu'à ce que le réseau ou le serveur soit de nouveau disponible — sans
jamais nécessiter de rechargement de page.

## Fichiers modifiés

- `Views/Display/Index.cshtml`
- `Views/Agent/Index.cshtml`
- `Views/Borne/Index.cshtml`
- `Nouba.csproj` — version 2.7.42.

## Non modifié (volontairement)

- La connexion SignalR du widget « statut imprimante » dans Admin a sa
  propre sécurité (polling toutes les 20 s indépendant de SignalR) :
  impact déjà nul, pas de changement nécessaire.

## À faire de votre côté

- `dotnet build` pour confirmer la compilation.
- Pas de test simple pour vérifier une coupure réseau prolongée ; à défaut,
  surveiller la console navigateur (F12) sur `/Display` : un message
  `SignalR Display deconnecte definitivement, nouvelle tentative dans 15s.`
  doit apparaître si la connexion tombe, suivi d'une reconnexion.
