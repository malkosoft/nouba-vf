# Activation du TTS naturel (Piper / Coqui XTTS)

Nouba Pro embarque un service IA 100 % offline. Par défaut, les annonces vocales
utilisent l'API Web Speech du navigateur (qualité standard).

Pour passer à une **voix synthétique de qualité studio** (style « Google Voice »
ou « Apple voice »), Nouba peut s'interfacer avec **Piper** ou **Coqui XTTS**,
deux moteurs TTS open-source qui tournent sur CPU sans GPU et sans internet.

## Option 1 — Piper (recommandé : léger, rapide, FR/AR/EN dispo)

### Installation Windows

1. Télécharger le binaire Piper :
   https://github.com/rhasspy/piper/releases (`piper_windows_amd64.zip`)

2. Décompresser dans `C:\Program Files\Nouba\piper\`

3. Télécharger les modèles vocaux (~60 Mo chacun) :
   - **FR** : `fr_FR-siwis-medium.onnx` (femme) ou `fr_FR-tom-medium.onnx` (homme)
   - **EN** : `en_US-amy-medium.onnx`
   - **AR** : pas de Piper officiel — utiliser Coqui XTTS (option 2)

   Source : https://huggingface.co/rhasspy/piper-voices

4. Placer les .onnx ET les .onnx.json dans `C:\Program Files\Nouba\piper\models\`

5. Configurer dans `appsettings.json` :
   ```json
   "Nouba": {
     "Ai": {
       "PiperPath": "C:\\Program Files\\Nouba\\piper\\piper.exe",
       "PiperModelsDir": "C:\\Program Files\\Nouba\\piper\\models\\",
       "PiperEnabled": true
     }
   }
   ```

6. Redémarrer Nouba. Les annonces vocales passeront automatiquement en Piper.

### Empreinte disque
- Binaire Piper : ~30 Mo
- Modèle moyen : ~60 Mo par voix

## Option 2 — Coqui XTTS (ARABE supporté)

Pour les annonces en arabe avec une qualité comparable à un humain.
Plus lourd : nécessite Python + ONNX Runtime, ~2 Go.

Documentation détaillée à venir dans une release future.

## Comportement actuel

Tant que Piper / Coqui n'est pas configuré, Nouba utilise le **TTS du
navigateur** qui fonctionne déjà sur tous les navigateurs modernes.
Aucune action requise pour démarrer.

---

© 2026 Nouba Software
