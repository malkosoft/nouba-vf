# Nouba v2.7.2 Premium — Correctifs produit

Cette version transforme Nouba vers une expérience plus premium et moins technique, sans setup/installateur.

## Inclus

- Admin simplifié : les détails Piper/ONNX/chemins ne sont plus exposés comme réglages client.
- Barre d'action sticky en haut de la page affichage.
- Onglet Diagnostic guidé : système, voix IA, imprimante, borne.
- Piper natif offline via `wwwroot/tts/piper/`.
- Modèles codés en dur :
  - `fr_FR-upmc-medium.onnx`
  - `en_US-lessac-medium.onnx`
  - `ar_JO-kareem-medium.onnx`
- Support prévu pour une voix arabe féminine compatible Piper si un modèle est déposé dans le dossier.
- Fallback automatique : si voix IA absente, Nouba utilise la voix système/navigateur sans crash et sans bruit.
- Audio Piper sécurisé : génération en vrai fichier WAV temporaire puis lecture en `audio/wav`.
- Cache-buster et no-store pour éviter que la première voix reste bloquée après changement de langue.
- Presets premium : Clinique, Administration, Banque, Mairie, Entreprise.
- Chatbot borne non présent dans l'interface borne.

## Non inclus volontairement

- Aucun installateur Windows.
- Aucun PowerShell.
- Aucun setup Inno/WiX/MSIX.

## À faire côté machine

Déposer manuellement les binaires/voix dans `wwwroot/tts/piper/` si absents.
