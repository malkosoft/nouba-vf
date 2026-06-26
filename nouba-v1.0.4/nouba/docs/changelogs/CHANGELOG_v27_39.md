# Nouba v2.7.39 — QR enfin imprimé + impression navigateur économe (confirmé)

Suite à tes précisions : tu imprimes via le navigateur Windows, et le suivi
QR est activé. Cette version cible exactement ce cas.

## 1. QR code manquant sur le ticket → corrigé

Cause trouvée : l'impression navigateur se déclenchait 600 ms après
l'affichage de la page, soit AVANT que l'image du QR (générée à la volée
côté serveur) ait fini de se charger. Le navigateur imprimait donc la page
sans le QR.

Correction : l'impression attend désormais que l'image QR soit complètement
chargée avant de lancer l'impression (avec un filet de sécurité : si le QR
tarde plus de 2,5 s, on imprime quand même). Le QR sera présent sur le ticket.

## 2. Encre : ticket navigateur en noir sur blanc (rappel v2.7.38)

Le style d'impression de la page de confirmation n'imprime plus aucun fond
coloré ou gris (lignes d'info, instruction, badge prioritaire) : tout en noir
sur blanc pur. Consommation d'encre fortement réduite. Le numéro de ticket
reste en grand.

## Rappels utiles

- Le QR n'apparaît que si « Activer le suivi mobile par QR code » est coché
  (c'est ton cas). 
- Le QR pointe vers l'adresse du serveur : pour un scan en 4G par le client,
  renseignez l'URL publique dans les réglages QR ; sinon il ne fonctionne que
  sur le Wi-Fi local.

## Fichiers modifiés

- `Views/Borne/Confirmation.cshtml` — attente du chargement du QR avant
  impression (+ impression noir sur blanc de la v2.7.38).
- `Nouba.csproj` — version 2.7.39.

## À tester

- Prends un ticket → la page de confirmation s'affiche → l'impression doit
  partir une fois le QR visible, et le ticket imprimé (noir sur blanc) doit
  comporter le QR.
