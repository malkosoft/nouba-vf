# Installation de la voix réaliste Piper TTS

Nouba peut annoncer les tickets avec une **voix neuronale naturelle** au lieu de la voix robotique du navigateur, grâce au moteur **Piper TTS** open-source. Ce système est **100 % offline** : aucun envoi de données vers Internet.

## Vue d'ensemble

| Langue | Modèle recommandé | Qualité | Taille |
|---|---|---|---|
| Français | `fr_FR-siwis-medium` | ⭐⭐⭐⭐⭐ Excellente | ~63 Mo |
| Anglais  | `en_US-amy-medium`   | ⭐⭐⭐⭐⭐ Excellente | ~63 Mo |
| Arabe    | `ar_JO-kareem-low`   | ⭐⭐⭐ Moyenne (arabe standard, accent jordanien) | ~30 Mo |
| Tamazight | _(non disponible)_  | — | Utilise la voix navigateur |

**Total disque** : environ **160 Mo** pour les 3 modèles installés + ~15 Mo pour le binaire Piper.

---

## Étape 1 : Télécharger Piper

1. Aller sur **https://github.com/rhasspy/piper/releases**
2. Télécharger la dernière version pour votre OS :
   - Windows 64-bit : `piper_windows_amd64.zip`
   - Linux 64-bit : `piper_linux_x86_64.tar.gz`
3. Extraire l'archive
4. Copier **tout le contenu** (piper.exe + DLLs `.dll` ou bibliothèques `.so`) dans :

```
wwwroot/tts/piper/
```

Le résultat doit ressembler à :
```
wwwroot/tts/piper/
  piper.exe          (sous Windows)
  espeak-ng-data/    (dossier requis)
  *.dll              (les DLLs accompagnant piper.exe)
```

---

## Étape 2 : Télécharger les modèles vocaux

Aller sur **https://huggingface.co/rhasspy/piper-voices/tree/main**

Pour chaque modèle, télécharger **les deux fichiers** : le `.onnx` ET le `.onnx.json` qui l'accompagne (le JSON contient la phonétique).

### Modèle français (recommandé)
- Chemin sur HuggingFace : `fr/fr_FR/siwis/medium/`
- Fichiers à télécharger :
  - `fr_FR-siwis-medium.onnx` (~63 Mo)
  - `fr_FR-siwis-medium.onnx.json` (~5 Ko)

### Modèle anglais
- Chemin : `en/en_US/amy/medium/`
- Fichiers :
  - `en_US-amy-medium.onnx`
  - `en_US-amy-medium.onnx.json`

### Modèle arabe
- Chemin : `ar/ar_JO/kareem/low/`
- Fichiers :
  - `ar_JO-kareem-low.onnx` (~30 Mo)
  - `ar_JO-kareem-low.onnx.json`

Placer les **6 fichiers** dans :

```
wwwroot/tts/models/
```

---

## Étape 3 : Activer dans l'admin Nouba

1. Ouvrir l'admin → **Affichage & Voix**
2. Descendre jusqu'au panneau violet **« Voix IA réaliste (Piper) »**
3. Cocher **« Activer Piper TTS »**
4. Vérifier l'état des langues — chaque langue doit afficher **✓ Voix Piper active** en vert
5. Cliquer sur les boutons **FR / EN / AR** pour tester chaque langue
6. Cliquer **« Enregistrer la configuration TTS »**

Si une langue affiche **✗ Modèle absent** ou **✗ Binaire absent**, vérifiez les chemins dans la section précédente.

---

## Comment ça fonctionne

Quand un agent appelle un ticket :

1. L'écran d'affichage demande la phrase au serveur via `/Tts/Speak?text=...&lang=fr`
2. Si Piper est configuré et a un modèle pour cette langue → le serveur génère le WAV via Piper et le retourne
3. Si Piper n'est **pas** configuré ou a planté → le serveur renvoie **204 No Content**
4. Dans ce cas, le navigateur utilise automatiquement sa **Web Speech API** comme fallback

**Cache** : chaque WAV généré est mis en cache dans `wwwroot/tts/cache/` pour ne pas re-synthétiser les mêmes phrases. Le cache se nettoie automatiquement quand il dépasse 100 fichiers.

---

## Dépannage

### « Binaire absent »
Vérifiez que `wwwroot/tts/piper/piper.exe` existe et est exécutable.
Sous Windows, il faut **toutes les DLLs** présentes dans l'archive Piper d'origine.

### « Modèle absent »
Vérifiez que **les deux fichiers** (`.onnx` ET `.onnx.json`) sont bien présents avec les noms exacts dans `wwwroot/tts/models/`.

### Le test fonctionne mais l'écran d'affichage utilise toujours la voix navigateur
- Vérifier que la case « Activer Piper TTS » est cochée
- Recharger la page Display (F5)
- Vérifier la console navigateur (F12) — pas d'erreur réseau sur `/Tts/Speak` ?

### Piper plante / timeout
- Augmenter le timeout dans l'admin (par défaut 5000 ms, mettre 8000 ou 10000)
- Vérifier que le PC a suffisamment de RAM (recommandé : 4 Go libres)
- Premier lancement = plus lent (chargement du modèle ONNX)

### Voix arabe inintelligible
C'est normal et attendu : aucun modèle TTS open-source ne gère parfaitement les dialectes maghrébins. Le modèle `ar_JO-kareem-low` parle un arabe **standard littéraire** avec un accent jordanien. Pour vos clients algériens, la voix arabe peut sonner étrange. Vous pouvez désactiver le modèle AR pour utiliser la voix navigateur arabe à la place (souvent meilleure car liée à la voix Windows installée).

---

## Désactivation

Pour revenir à la voix navigateur partout :
1. Décocher **« Activer Piper TTS »** dans l'admin
2. Enregistrer

Aucun fichier n'a besoin d'être supprimé — les modèles peuvent rester en place pour une réactivation future.

---

## Licences

- **Piper** : MIT License — usage commercial libre
- **Modèles fr_FR-siwis-medium / en_US-amy-medium** : CC-BY-4.0 — usage commercial libre avec mention
- **Modèle ar_JO-kareem-low** : CC-BY-4.0 — usage commercial libre avec mention

Pour les usages commerciaux de Nouba, mentionner dans la documentation client :
> Synthèse vocale réalisée avec Piper TTS et les voix de Sasha Smith / Amy Lawson / Kareem (rhasspy/piper-voices, CC-BY-4.0)
