# Nouba v2.7.38 — Ticket navigateur économe en encre + QR sur ticket

## Pourquoi le ticket précédent n'avait pas changé

Il existe DEUX modes d'impression dans Nouba :
- **Mode thermique (ESC/POS)** : imprimante Epson/XPrinter connectée, noir pur.
  C'est ce mode que la v2.7.37 avait optimisé.
- **Mode navigateur** : la page de confirmation est imprimée via window.print()
  — c'est une page web, donc avec fonds colorés.

Ton ticket « avec fond couleur » venait du MODE NAVIGATEUR, que la version
précédente n'avait pas touché. D'où l'absence de changement visible. C'est
corrigé ici.

## 1. Impression navigateur : noir sur blanc, zéro aplat (économie d'encre)

Le style d'impression de la page de confirmation est revu pour ne plus
imprimer AUCUN fond coloré ou gris :
- Fonds des lignes d'info (gris), de l'instruction (vert) et du badge
  prioritaire (orange) : supprimés → remplacés par du texte noir et de fins
  filets pointillés.
- Plus d'ombres ni de dégradés à l'impression.
- Tout en noir sur blanc pur : consommation d'encre minimale.
Le numéro de ticket reste en très grand.

## 2. QR code sur le ticket

Le QR EST bien prévu sur le ticket (mode navigateur et thermique), mais il ne
s'affiche QUE si « Activer le suivi mobile par QR code » est coché dans
l'Admin (onglet Suivi / QR). S'il n'apparaît pas, c'est que cette option est
désactivée.

À l'impression navigateur, le QR est maintenant explicitement maintenu
visible et net (28 mm, modules noirs sur blanc).

Rappel déploiement : le QR pointe vers l'adresse du serveur. Pour qu'un client
le scanne en 4G, renseignez l'URL publique dans les réglages QR ; sinon il ne
fonctionne que sur le Wi-Fi local de l'établissement.

## Fichiers modifiés

- `Views/Borne/Confirmation.cshtml` — bloc @media print entièrement revu
  (noir sur blanc, QR maintenu visible).
- `Nouba.csproj` — version 2.7.38.

## À tester

- Imprime un ticket via le navigateur : il doit sortir en noir sur blanc,
  sans aplats colorés.
- Active « le suivi mobile par QR code » dans l'Admin, puis réimprime : le QR
  doit apparaître.
