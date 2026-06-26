# Nouba v2.7.23 — Restaurer Piper sur PC + logs clairs

## Bug PC corrigé : régression Piper

En v2.7.22, j'avais introduit un warm-up Web Speech **au chargement de la page**.
Ce warm-up jouait silencieusement une utterance vide. Sur PC, cette utterance
restait dans la queue de `speechSynthesis` et perturbait le routage audio :
au moment où le 1er ticket était appelé, Web Speech répondait avant Piper.

**Correctif** : warm-up restreint aux Smart TV (`isLikelyTvBrowser` uniquement).
Sur PC, on retombe sur le comportement v2.7.21 : Piper en priorité, Web Speech
en secours.

## Regex TV restreint (faux positifs PC)

Avant : `TV` simple matchait des UA PC légitimes contenant « TV » comme sous-chaîne.
Si jamais ton UA Chrome PC contenait quelque chose de bizarre, tu basculais sur la
logique TV.

Maintenant : `TV` simple retiré du regex. Seuls les marqueurs explicites comptent :
Tizen, WebOS, VIDAA, HISENSE, SmartTV, GoogleTV, AndroidTV, BRAVIA, CrKey (Chromecast).

## Logs F12 explicites du moteur audio choisi

Quand tu testes maintenant, F12 console affiche en clair :
- `[Nouba TTS] PC détecté → Piper d'abord, Web Speech en fallback.`
- `[Nouba TTS] ✓ Piper a parlé sur PC.`

Ou sur TV :
- `[Nouba TTS] TV détectée → Web Speech d'abord, Piper en bonus.`
- `[Nouba TTS] Web Speech KO → fallback Piper.`
- `[Nouba TTS] ✓ Piper a parlé sur TV (bonus).` (si VIDAA décode le WAV)

Et si rien ne marche :
- `[Nouba TTS] Aucun moteur audio disponible pour cette annonce.`

## Comment tester

1. PC : ouvre /Display, F12 console, fais appeler un ticket. Tu devrais
   voir « ✓ Piper a parlé sur PC. » et entendre la voix IA neurale.
2. TV Hisense : ouvre /Display, fais appeler un ticket. Tu devrais entendre
   Web Speech ou Piper selon ce qui marche sur VIDAA. Si rien, F12 (via
   Chrome remote debug) te dira quel moteur a échoué et pourquoi.

## Sur le problème VIDAA si tout reste muet

VIDAA Hisense est connu pour bloquer les deux : `speechSynthesis` (Web Speech)
ET `<audio>` PCM WAV (Piper). Si après v2.7.23 ta TV reste muette, c'est que
VIDAA ne supporte aucun moteur audio web. Aucune correction Nouba n'est possible.

Trois solutions à ce stade :
1. **Mini-PC Android TV box** (40-80€, branché HDMI à la TV) → marche à 100%
2. **Mini-PC Windows fanless** (~150€) → marche à 100% et peut héberger Nouba lui-même
3. Accepter d'afficher sans son sur Hisense, compenser avec animation visuelle forte
   (déjà en place : `tickPop` au passage du ticket primary)

## Vérifications passées
- 17 fichiers HTML, 0 déséquilibre
- Aucun changement serveur, modèles, DB
