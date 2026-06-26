# Nouba v2.7 — Piper natif & Admin simplifié

## 🎙️ Piper TTS intégré nativement (out-of-the-box)
- **Plus de configuration dans l'admin** : tous les chemins Piper sont codés en dur dans `PiperTtsService.cs`.
- Le binaire et les modèles sont attendus dans `wwwroot/tts/piper/` :
  - `piper.exe` (ou `piper` sous Linux)
  - `fr_FR-upmc-medium.onnx` + `.json`
  - `en_US-lessac-high.onnx` + `.json`
  - `ar_JO-kareem-medium.onnx` + `.json`
- Si un modèle manque, la langue retombe **silencieusement** sur la voix navigateur.

## 🔊 Correction audio
- Piper écrit désormais un **fichier .wav temporaire** (`--output_file`).
  Plus jamais de lecture stdout-as-audio → fin du **bruit fort** historique.
- Validation du **header RIFF/WAVE** avant envoi au navigateur.
- **Cache-buster** côté client (`?v=…`) + **`Cache-Control: no-store`** côté serveur :
  plus de blocage sur la première voix après changement de langue.
- À chaque appel : **nouvelle instance `Audio()`**, l'ancienne est stoppée et libérée.
- Le blob est explicitement re-typé `audio/wav` (jamais mp3).
- À chaque changement de langue, `announceTicket()` coupe aussi le WAV en cours.

## 🧹 Admin réorganisé
La sidebar passe à **6 sections** simples :
- **A · Général** (tableau de bord)
- **B · Affichage borne** (écran d'affichage + borne kiosque)
- **C · Tickets** (services + guichets + agents)
- **D · Impression**
- **E · Langues** (textes & traductions)
- **F · Sécurité / maintenance** (SMS + système)

Suppression complète :
- du panneau « Voix IA réaliste (Piper) » et de tous ses champs (chemin binaire, modèles, vitesse, timeout, statut détaillé)
- du widget « Tester le chatbot d'orientation » dans le tableau de bord

## 🤖 Chatbot retiré de la borne
- Suppression du bouton flottant 💬, de la modale, du JS et de l'appel `/Ai/Chat`.
- L'action `AiController.Chat` a été retirée (l'endpoint n'est plus exposé).
- Aucun appel API mort, aucune erreur console.

## 🧱 Qualité code
- `PiperTtsService` ne dépend plus de `UiSettings` ; chemins via `IWebHostEnvironment.WebRootPath`.
- `TtsController` allégé : plus de `SaveSettings`, juste `Speak` + `Status` minimal.
- Les colonnes `TtsXxx` de la DB sont conservées (rétro-compat migration) mais ne sont plus jamais lues ni écrites.

## ✅ Conservé tel quel
Tickets, impression, CSV, affichage, langues, SMS, IA admin (résumé/rapport),
agents, guichets, services, monitoring imprimante, licence — non modifiés.
