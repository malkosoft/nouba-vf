# Nouba v2.7.52 — Borne tablette : plus de boîte d'impression PDF parasite

## Le problème (vu sur votre capture)

Avec le mode d'impression « all », le ticket partait bien vers l'imprimante
thermique (badge « Impression du ticket… ») **mais la page ouvrait EN PLUS
la boîte d'impression du navigateur** (« Microsoft Print to PDF »). Sur une
borne tablette, c'est inacceptable : double impression + une boîte de dialogue
que le citoyen ne doit jamais voir.

## Le correctif

La boîte d'impression du navigateur ne s'ouvre désormais **jamais** quand le
ticket a déjà été pris en charge par l'imprimante thermique (ESC/POS) :

- `escposOk` = « ok » ou « queued » → l'imprimante thermique imprime →
  **aucune impression navigateur**, aucune boîte de dialogue.
- mode « all » → l'ESC/POS reste prioritaire ; le navigateur n'imprime QUE
  si l'ESC/POS n'a pas pris le relais (imprimante désactivée, ou échec).
- mode « escpos » → jamais d'impression navigateur (sauf échec, en secours).

Résultat sur la tablette : le ticket sort tout seul de l'imprimante
thermique, la borne revient à l'accueil, et **aucune fenêtre PDF n'apparaît**.

## Comment configurer la tablette pour une impression 100 % automatique

Deux cas selon votre matériel :

### A. Imprimante thermique (recommandé pour une borne)
1. Admin → Impression → mode **« escpos »** (ou « all »), imprimante activée,
   IP/port de l'imprimante renseignés.
2. Le serveur envoie le ticket directement à l'imprimante (TCP) : impression
   immédiate, silencieuse, sans aucune boîte de dialogue.

### B. Pas d'imprimante thermique (impression via le navigateur)
Pour imprimer sans la boîte de dialogue, lancez le navigateur en **mode
kiosque avec impression silencieuse** :

- **Edge** : `msedge.exe --kiosk "http://localhost:5000/Borne" --edge-kiosk-type=fullscreen --kiosk-printing`
- **Chrome** : `chrome.exe --kiosk "http://localhost:5000/Borne" --kiosk-printing`

Le flag `--kiosk-printing` fait que `window.print()` imprime directement sur
l'imprimante par défaut, **sans afficher de boîte de dialogue**. Pensez à
définir la bonne imprimante par défaut dans Windows (pas « Print to PDF »).

> Astuce démo : le mode kiosque masque aussi la barre d'adresse et les
> onglets — l'écran ressemble alors à une vraie borne, pas à un navigateur.

## Fichiers modifiés

- `Views/Borne/Confirmation.cshtml` — logique d'impression navigateur revue.
- `Nouba.csproj` — version 2.7.52.
