═══════════════════════════════════════════════════════════════════
  Piper TTS — Voix IA Nouba — 6 VOIX (FR/EN/AR × féminin/masculin)
═══════════════════════════════════════════════════════════════════

À partir de la v2.7.31, Nouba gère 6 voix : français, anglais et arabe,
chacune en voix FÉMININE et MASCULINE. Le genre est piloté par le réglage
« Voix » de l'Admin et s'applique aux trois langues.

IMPORTANT : ce dossier doit contenir les fichiers .onnx (les modèles de
voix). Ils ne sont PAS livrés dans le zip (trop volumineux). Téléchargez
CHAQUE .onnx AVEC son .onnx.json (les deux sont obligatoires).

───────────────────────────────────────────────────────────────────
LES 6 VOIX ET LES FICHIERS REQUIS
───────────────────────────────────────────────────────────────────

FRANÇAIS (féminin + masculin) — UN SEUL FICHIER (2 voix incluses) :
  • fr_FR-upmc-medium.onnx        (+ .onnx.json déjà présent)
    → contient jessica (féminin, speaker 0) ET pierre (masculin, speaker 1)
    → Nouba sélectionne automatiquement la bonne voix selon le réglage.

ANGLAIS féminin :
  • en_US-lessac-medium.onnx      (+ .onnx.json déjà présent)
ANGLAIS masculin (NOUVEAU fichier à ajouter) :
  • en_US-ryan-medium.onnx        (+ en_US-ryan-medium.onnx.json)

ARABE féminin :
  • arabic-emirati-female-model.onnx   (+ .onnx.json déjà présent)
ARABE masculin :
  • ar_JO-kareem-medium.onnx           (+ ar_JO-kareem-medium.onnx.json)
    (recommandé : modèle stable. Évitez ar_JO-SA_miro-high qui est un
     modèle d'entraînement « training », parfois instable.)

───────────────────────────────────────────────────────────────────
OÙ TÉLÉCHARGER
───────────────────────────────────────────────────────────────────

Binaire Piper :
  https://github.com/rhasspy/piper/releases
  (zip de votre OS, à extraire ici : piper.exe + espeak-ng-data/)

Modèles de voix (.onnx + .onnx.json) :
  https://huggingface.co/rhasspy/piper-voices/tree/main
  Arborescence du site : <langue>/<code>/<dataset>/<qualité>/<fichier>
  Exemples de chemins :
    fr/fr_FR/upmc/medium/fr_FR-upmc-medium.onnx
    en/en_US/lessac/medium/en_US-lessac-medium.onnx
    en/en_US/ryan/medium/en_US-ryan-medium.onnx
    ar/ar_JO/kareem/medium/ar_JO-kareem-medium.onnx
  (Téléchargez le .onnx ET le .onnx.json de chaque dossier.)

  Voix arabe féminine emirati : voir la collection de modèles arabes
  Piper (le .onnx.json fourni indique le nom attendu).

───────────────────────────────────────────────────────────────────
NOMS DE FICHIERS ALTERNATIFS ACCEPTÉS (si vous changez de voix)
───────────────────────────────────────────────────────────────────
Anglais masculin : en_US-ryan-high/low, en_US-joe-medium,
                   en_GB-alan-medium, en_GB-northern_english_male-medium
Anglais féminin  : en_US-lessac-high, en_US-amy-medium,
                   en_US-hfc_female-medium, en_GB-jenny_dioco-medium
Arabe masculin   : ar_JO-kareem-low, ar_SA-miro-*, arabic-male-model
Arabe féminin    : voix-arabe-feminine, ar_SA-dii-*, ar_AR-female-medium

───────────────────────────────────────────────────────────────────
VÉRIFICATION APRÈS INSTALLATION
───────────────────────────────────────────────────────────────────
Ouvrez dans un navigateur :  http://<serveur>:5000/Tts/Status
Vous verrez l'état des 6 voix, par exemple :
  fr: { ready:true, female:true, male:true, gender:"female+male" }
  en: { ready:true, female:true, male:true, gender:"female+male" }
  ar: { ready:true, female:true, male:true, gender:"female+male" }
Si "male:false" pour FR → le .onnx FR n'est pas le modèle 2-voix upmc.
Si "male:false" pour EN → ajoutez en_US-ryan-medium.onnx.

ARBORESCENCE FINALE ATTENDUE :
  wwwroot/tts/piper/
    piper.exe
    espeak-ng-data/
    fr_FR-upmc-medium.onnx               + .onnx.json
    en_US-lessac-medium.onnx             + .onnx.json
    en_US-ryan-medium.onnx               + .onnx.json
    arabic-emirati-female-model.onnx     + .onnx.json
    ar_JO-kareem-medium.onnx             + .onnx.json
  wwwroot/tts/cache/   (auto, ne pas toucher)
═══════════════════════════════════════════════════════════════════
