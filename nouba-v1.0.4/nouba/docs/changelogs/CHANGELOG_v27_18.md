# Nouba v2.7.18 — Diagnostic son TV précis

## Ton retour de v2.7.17

Tu m'as envoyé : `Piper KO (fr) : audio.onerror / WAV non lisible ou endpoint 204 (1/3)`

Ce message regroupait DEUX causes très différentes :
- **204 No Content** côté serveur (Piper n'a rien produit) → problème serveur
- **audio.onerror** côté client (TV ne décode pas) → problème navigateur TV

Impossible de les distinguer dans la même ligne. Avec v2.7.18, le diagnostic
sépare clairement les deux.

## Nouveau diagnostic côté client

`playWithPiper` fait maintenant un **fetch préalable** avant de lancer
`audio.play()`. Selon ce que renvoie le serveur, le panneau debug affiche :

| Affichage panneau | Cause | À faire |
|---|---|---|
| `Piper indisponible (lang=fr) — vérifier modèles ONNX` | Serveur répond 204 | Vérifier `wwwroot/tts/piper/fr_FR-upmc-medium.onnx` présent |
| `Erreur Piper HTTP 500` | Serveur a planté | Vérifier les logs serveur |
| `WAV vide/invalide (0 octets)` | Piper a renvoyé un fichier corrompu | Tester `/Tts/Status` |
| `audio.onerror code=4 (TV ne décode pas WAV)` | TV ne supporte pas le format WAV PCM | Bug TV — possible workaround MP3 |
| `timeout 2.5s (TV bloquée ?)` | TV ne déclenche aucun event | Recharger la page TV |
| `NotAllowedError (autoplay bloqué ?)` | Navigateur exige un geste | Cliquer le bouton flottant |

Le panneau garde les 3 dernières erreurs avec horodatage, ce qui te donne
la séquence exacte.

## Diagnostic enrichi côté serveur

L'endpoint `/Tts/Status` retourne maintenant **les chemins exacts** que Piper
cherche :

```json
{
  "available": true,
  "binary": { "path": "C:\\nouba\\wwwroot\\tts\\piper\\piper.exe", "exists": true },
  "fr": {
    "ready": true,
    "path": "C:\\nouba\\wwwroot\\tts\\piper\\fr_FR-upmc-medium.onnx",
    "onnxExists": true,
    "jsonExists": true
  },
  "en": { ... },
  "ar": { ... }
}
```

Pour diagnostiquer côté serveur, ouvre simplement
`http://localhost:5000/Tts/Status` dans un navigateur PC : tu vois
**immédiatement** quel fichier manque.

## Logs serveur passés en Warning

Avant : `_logger.LogDebug` → invisible en prod.
Maintenant : `_logger.LogWarning` avec le **chemin attendu** :

> `Piper TTS : modèle absent ou invalide pour lang=fr gender=female (path attendu : C:\\nouba\\wwwroot\\tts\\piper\\fr_FR-upmc-medium.onnx)`

Ces logs apparaissent dans la console serveur (sortie standard) au démarrage
de Nouba, dès le premier appel TTS.

## Correctif côté décodage TV

Avant : `audio.src = '/Tts/Speak?...'` directe → certaines Smart TV anciennes
ne décodent pas le streaming WAV via HTTP.

Maintenant : on fetch le WAV en blob, on vérifie qu'il fait au moins 200 octets,
puis on crée un `URL.createObjectURL(blob)` que la TV lit comme un fichier
local. Plus stable sur Tizen / WebOS.

## À faire chez toi maintenant

1. Démarrer Nouba.
2. Ouvrir `http://localhost:5000/Tts/Status` dans Chrome PC.
3. Si `binary.exists` ou un des `onnxExists` est `false` → c'est ça la cause :
   le fichier est absent. Vérifier `wwwroot/tts/piper/`.
4. Sinon, ouvrir la TV, faire appeler un ticket. Le panneau diagnostic
   donnera la vraie raison (autoplay bloqué, code décodeur, etc.).
5. Me renvoyer ce que tu vois.

## Vérifications passées
- 60 fichiers, 0 déséquilibre syntaxique.
- Doublons code éliminés dans playWithPiper (un bloc orphelin dupliqué
  qui traînait après la fermeture de la fonction).
- Aucun input serveur cassé.
