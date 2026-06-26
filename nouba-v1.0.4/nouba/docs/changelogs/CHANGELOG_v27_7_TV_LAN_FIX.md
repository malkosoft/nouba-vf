# Nouba v2.7.7 — Fix TV / LAN / son affichage

Corrections appliquées :

1. **Accès réseau TV / téléphone / postes agents**
   - Nouba n'écoute plus par défaut uniquement sur `127.0.0.1`.
   - L'adresse par défaut devient `http://0.0.0.0:5000` pour permettre l'accès depuis la TV sur le même réseau.
   - Les arguments `--urls`, `DOTNET_URLS` et `ASPNETCORE_URLS` sont maintenant prioritaires.
   - `appsettings.json`, `UiSettings`, migration DB et seed ont été alignés sur `0.0.0.0:5000`.

2. **Overlay “Activer le son” sur Smart TV**
   - Ajout de `touchstart`, `touchend`, `pointerup`, `pointerdown`, `click`, `keydown`.
   - Support télécommande TV avec focus automatique et touches OK / Entrée / Espace.
   - L'overlay ne se réaffiche plus en boucle après un geste utilisateur si l'audio HTML est refusé.
   - Passage en plein écran tenté automatiquement après le geste utilisateur.

3. **TTS sur navigateurs TV**
   - Piper peut être utilisé même si le navigateur ne supporte pas Web Speech API.
   - Le fallback navigateur reste disponible quand Web Speech existe.

Tests recommandés après publication :
- Lancer Nouba installé et vérifier `Now listening on: http://0.0.0.0:5000` ou équivalent.
- Tester sur le PC : `http://192.168.1.46:5000`.
- Tester sur téléphone même Wi-Fi.
- Tester sur TV : `/Display` puis valider “Activer le son”.
