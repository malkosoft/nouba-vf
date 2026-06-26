# Nouba Pro 1.0.0 — Première version commerciale

Date : 2026-06-25
Lignée : issue directe de la série 2.7.x (dernier point : 2.7.58). Le passage en
**1.0.0** marque la première version *vendable* stabilisée, prête à l'exécutable
autonome et au prototype matériel.

## Voix (outillage de réglage arabe)
- Nouvelle page interne **`/tts-tuning.html`** : réglage de la voix à l'oreille
  (langue, genre, **vitesse en direct**, et bascule **avec/sans tashkeel** pour
  l'arabe), avec comparaison A/B. Permet de finaliser la prononciation sans
  recompiler.
- `/Tts/Speak` accepte désormais deux paramètres **optionnels** : `lengthScale`
  (débit forcé, borné 0.5–2.0) et `strip` (retire les voyelles arabes pour
  laisser libtashkeel décider). Aucun impact sur les appels existants.
- Helper `AnnouncementTextBuilder.StripArabicDiacritics` (retrait tashkeel +
  tatweel, sans toucher aux lettres ni au texte non-arabe).
- Diagnostic identifié : le texte d'annonce arabe est *à moitié* vocalisé, ce
  qui perturbe souvent le diacritiseur de Piper. À trancher à l'oreille via la
  page de réglage, puis report dans `ResolveLengthScale` / le texte d'annonce.

## Stabilisation
- Version produit alignée partout en **1.0.0** (`Version`, `FileVersion`,
  `AssemblyVersion`). Badge admin affiché : `v1.0.0`.
- Lecture de version durcie (`LicenseInfo`) : ne garde que le jeton `X.Y.Z`,
  insensible à un éventuel suffixe `+hash`/libellé.
- Réglages TTS hérités (`TtsModelFr/En/Ar`, `TtsLengthScale`, `TtsTimeoutMs`…)
  documentés comme **inertes** : non lus par le service, non exposés à l'admin.
  Conservés tels quels (pas de migration SQLite testable hors runtime).

## Exécutable
- Script **`publish-windows.bat`** : produit un `.exe` autonome (self-contained,
  single-file, win-x64, ReadyToRun, DLL natives auto-extraites).
- Guide **`docs/BUILD_EXE.md`** : pré-requis, build, premier lancement, et points
  vente (signature de code, SmartScreen, démarrage auto, perfs clé USB).

## Notes
- Aucune modification du flux d'annonce en production : tout l'ajout vocal est
  optionnel et piloté par la page de réglage tant que les valeurs définitives ne
  sont pas figées.
