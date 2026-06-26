# Nouba v2.7.8 — Correctif TV / LAN / responsive

## Corrections incluses

1. **Adresse réseau LAN**
   - Neutralisation robuste des anciennes configurations qui forçaient `127.0.0.1:5000`.
   - Réapplication de l'URL finale après `Build()` via `app.Urls`.
   - Par défaut : `http://0.0.0.0:5000`.
   - La TV doit accéder via `http://IP_DU_PC:5000/display`.

2. **Smart TV — bouton Activer le son**
   - Suppression de syntaxes JavaScript trop récentes dans le bloc critique (`?.`, `.finally`).
   - Support télécommande : `Enter`, `OK`, `Select`, keyCode `13`, `23`, `32`, `keydown` et `keyup`.
   - Si la Smart TV ne déclenche aucun événement compatible, l'overlay se ferme automatiquement après 12 secondes pour ne jamais bloquer l'affichage.

3. **Affichage TV / téléphone**
   - CSS responsive renforcé pour `/display`.
   - CSS responsive renforcé pour `/Borne`.
   - Les pages évitent maintenant le recouvrement et le cropping sur téléphones, tablettes et navigateurs TV à faible hauteur.

4. **Offline / LAN**
   - SignalR de `/display` charge maintenant la librairie locale `/lib/signalr/signalr.min.js`, sans CDN Internet.

## À faire après installation

- Désinstaller l'ancienne version.
- Installer cette nouvelle version.
- Vérifier dans la console : `Now listening on: http://0.0.0.0:5000`.
- Tester depuis le PC : `http://IP_DU_PC:5000/display`.
- Tester depuis téléphone puis Smart TV.
