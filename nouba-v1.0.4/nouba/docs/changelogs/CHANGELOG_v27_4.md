# Nouba v2.7.4 — Fix voix navigateur en exploitation + 3 nouveaux thèmes

## Bug critique corrigé : Piper « prêt » mais voix navigateur en exploitation

### Diagnostic
L'admin affichait bien « Voix IA prête » pour FR/EN/AR, mais en production
(page `/Display`), c'était la voix du navigateur qui sortait. Trois causes
identifiées :

1. **Instance Audio non débloquée.** `unlockAudio()` débloquait une instance
   Audio() temporaire, puis `playWithPiper` créait une **nouvelle** instance
   à chaque appel. Sur certains navigateurs (Chrome kiosque notamment), la
   politique autoplay refuse `.play()` sur des instances Audio() différentes
   même après un geste utilisateur. Résultat : `audio.play()` était rejeté
   silencieusement → fallback navigateur.

2. **Aucun log clair.** Quand Piper échouait pour une raison ou une autre,
   on retombait sur la voix navigateur sans aucun message console.

3. **Re-tentative en boucle.** Si une langue échouait en permanence (modèle
   corrompu, par exemple), on continuait à appeler /Tts/Speak à chaque
   ticket, ajoutant 1-2 s de latence avant la voix navigateur.

### Correction (Views/Display/Index.cshtml)

- **Instance Audio unique** : `piperAudioPlayer` est créée UNE fois et
  réutilisée partout. `unlockAudio()` la débloque explicitement avec un WAV
  silencieux. `playWithPiper` réutilise cette même instance.
- **Logs détaillés** : ouvrir la console F12 sur `/Display` montre maintenant
  pour chaque appel TTS :
    - `[Nouba TTS] Lang=fr gender=female → 204, fallback navigateur (modèle absent)`
    - `[Nouba TTS] HTTP 500 sur /Tts/Speak (lang=ar). Échec 1/3.`
    - `[Nouba TTS] audio.play() refusé par le navigateur (autoplay ?). Erreur: NotAllowedError. Échec 1/3. Touchez l'écran pour autoriser.`
    - `[Nouba TTS] audio.onerror — blob WAV invalide ou décodage refusé. Échec 1/3.`
- **Compteur d'échecs par langue** : 3 KO consécutifs → Piper désactivé pour
  cette langue jusqu'au prochain F5. Évite de re-tenter en vain.
- **Réveil correct de l'overlay** : si l'autoplay refuse `.play()`, l'overlay
  « Touchez l'écran » réapparaît automatiquement.
- **Déclaration `let` remontée** pour éviter Temporal Dead Zone : `unlockAudio`
  utilisait `piperAudioPlayer` avant sa déclaration `let` (ReferenceError).

### Comment vérifier
Ouvre `/Display`, F12, onglet Console. Demande un ticket. Tu dois voir soit :
- aucune ligne `[Nouba TTS]` → Piper a marché ✓
- une ligne `→ 204, fallback navigateur (modèle absent ou genre incompatible)` → Piper indispo pour cette langue, c'est normal
- une ligne `audio.play() refusé` → l'overlay devrait réapparaître

## 3 nouveaux thèmes premium

Ajout de 3 cartes preset au tab Apparence :
- ✈️ **Aéroport** — cyan, débit voix 1.05× (trafic dense)
- 📮 **Poste** — jaune signal sur fond sombre, débit 0.95×
- ⚖️ **Tribunal** — terracotta sobre, débit 0.85× et pitch 0.9 (ton posé, autoritaire)

Total : 8 thèmes au lieu de 5.

## Pas inclus dans cette session

Tu m'as aussi demandé d'enrichir les **mises en page**. Je n'ai pas ajouté de
nouvelle option dans le `<select name="DisplayLayout">` parce que cela
nécessite d'implémenter le rendu correspondant côté `Views/Display/Index.cshtml`
(refonte du CSS et du grid HTML). Si je n'ajoute que les options sans le
rendu, choisir « Triple écran » donnerait visuellement la même chose que
« Standard », ce serait une régression UX.

À la prochaine session, on peut faire ça proprement : Triple, Vidéo dominante,
Multi-guichets, avec le CSS réel pour chaque.

## Conservé tel quel
Tickets, impression, CSV, langues, SMS, IA admin, agents, guichets, services,
monitoring imprimante, licence, base de données, backend Piper, fallback navigateur.
