# Nouba v2.7.9 — Correctif Smart TV / téléphone / médias / audio

## Corrections principales

1. **Médias invisibles sur Smart TV**
   - Suppression du comportement qui passait automatiquement l'affichage en une seule colonne à 1280px.
   - Les Smart TV 1280x720 / 1366x768 gardent maintenant une vraie mise en page paysage avec tickets + médias visibles.
   - Le layout `tv-wall` ne masque plus la carte média.

2. **Son des tickets sur TV et smartphone**
   - Le TTS Piper n'est plus désactivé quand l'admin choisit une voix masculine alors que seul un modèle Piper féminin est disponible en FR/EN.
   - Lecture Piper plus compatible Smart TV : utilisation directe de l'URL WAV `/Tts/Speak` dans un vrai élément `<audio>` au lieu d'un Blob URL.
   - Ajout d'un bouton flottant `Son` si la Smart TV refuse les événements du gros overlay.
   - Le système ne déclare plus l'audio comme activé sans vrai geste utilisateur, pour éviter les annonces silencieuses.

3. **Fallback terrain**
   - Si la voix arabe féminine n'est pas présente, fallback vers `ar_JO-kareem-medium`.
   - Si Piper échoue, fallback navigateur quand disponible, sans bruit fort.

## À tester

- PC : `http://192.168.x.x:5000/display`
- Téléphone : affichage + son après bouton Son
- Smart TV : médias visibles + bouton Son / OK télécommande
- Appel ticket depuis poste agent : annonce vocale sur écran affichage
