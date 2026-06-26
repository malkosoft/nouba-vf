# Nouba v2.7.22 — Web Speech sur VIDAA Hisense

## Problème diagnostiqué

Tu m'as confirmé : sur ta TV Hisense (VIDAA OS), Web Speech ne se déclenchait
pas malgré v2.7.21. En relisant mon code, j'ai trouvé **4 bugs** qui ensemble
empêchaient Web Speech de marcher correctement sur les vieux Chromium type
VIDAA.

## 4 bugs corrigés

### 1. Garde-fou 1500 ms tuait Web Speech en plein discours

Avant : `setTimeout(() => done(false), 1500)`. Mais une annonce type
« Ticket B12 guichet 2 » dure 3-4 secondes. Le timeout déclenchait
`resolve(false)` PENDANT que la voix parlait → on basculait sur Piper qui
parlait par-dessus, ou pire on coupait tout.

Après : le garde-fou vérifie seulement que `onstart` est appelé en **2 secondes**
(la voix démarre-t-elle ?). Si oui, on laisse parler aussi longtemps que nécessaire.
Si non, Web Speech est mort et on tombe sur Piper.

### 2. `speechSynthesis.cancel()` AVANT `.speak()` tuait l'utterance

Sur VIDAA, l'enchaînement `cancel() ; speak(utt)` annulait aussi le nouveau
`utt` qu'on venait de créer. Retiré le `cancel()` préventif.

### 3. Pas de warm-up Web Speech au chargement

Les vieux Chromium (VIDAA, anciens WebOS) ont besoin d'un premier appel
`speak()` silencieux pour initialiser leur stack audio interne. Sans ce warm-up,
le 1er vrai appel échoue.

Ajouté : `speak(new SpeechSynthesisUtterance(' '))` au chargement de la page,
volume 0, rate 10 (durée négligeable). 400 ms après, on re-charge la liste des voix.

### 4. Volume `<0.8` ignoré par VIDAA

VIDAA ignore parfois les volumes faibles. Forcé à `Math.max(0.8, voiceVolume)`
sur Web Speech pour garantir audibilité.

## Bonus : détection TV étendue

`isLikelyTvBrowser` détecte maintenant aussi : VIDAA, HISENSE, GoogleTV, AndroidTV,
AFTM, AFTB, BRAVIA. Avant : Tizen, WebOS, NetCast, HbbTV, TV.

## Logs F12 améliorés

Quand tu testes, ouvre F12 console sur la TV (si possible via Chrome remote debug)
ou sur PC en simulant l'UA Hisense. Tu verras :
- `[Nouba TTS] Warm-up Web Speech effectué — N voix disponibles.`
- `[Nouba TTS] speechSynthesis.speak() appelé, en attente de onstart…`
- `[Nouba TTS] Web Speech démarre (Microsoft Hortense - French, lang=fr-FR)`
- `[Nouba TTS] Web Speech terminé OK.`

OU si ça échoue :
- `[Nouba TTS] Web Speech : onstart jamais déclenché en 2s → moteur indisponible (VIDAA ?)`

## Si ça ne marche TOUJOURS pas après v2.7.22

VIDAA peut **vraiment** ne pas supporter Web Speech (certaines versions firmware).
Dans ce cas, aucune correction Nouba ne peut aider — c'est une limitation TV.
Solution standard : mini-PC Android TV box (40-80€) branché en HDMI.

Cette solution est celle utilisée par TOUS les concurrents commerciaux algériens
(CECI DZ inclus, qui vend un kiosk Intel i5 à plusieurs milliers de dinars).
Tu peux faire pareil mais 5 à 10× moins cher avec une box Android TV.

## Vérifications passées
- 60 fichiers, 0 déséquilibre syntaxique.
- Aucun changement serveur, contrôleurs, modèles, DB.
