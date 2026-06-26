# Nouba v2.7.1 — Correctifs voix

## 🔓 Bug 1 : voix qui ne marchait qu'après clic / plein écran

**Cause** : les navigateurs (Chrome, Edge, Safari) bloquent toute lecture
audio (Web Speech API et HTMLAudio) tant qu'il n'y a pas eu d'interaction
utilisateur sur la page. Avant ce correctif, les annonces étaient
**silencieusement abandonnées** quand l'audio était verrouillé.

**Correctif** :
- **Overlay « Touchez l'écran pour activer le son »** s'affiche au démarrage
  tant que l'audio n'a pas été déverrouillé.
- Au premier clic / touche / pointer : audio débloqué + plein écran tenté +
  réveil de `speechSynthesis` + réveil du canal `Audio()` via un WAV silencieux.
- Les annonces qui arrivent pendant le verrouillage **ne sont plus jetées** :
  elles sont mises en file (max 5) et la dernière est rejouée dès que l'audio
  est débloqué.
- L'état déverrouillé est mémorisé dans `sessionStorage` → après un
  rafraîchissement de la page, plus besoin de re-cliquer.

## 🎙️ Bug 2 : choix masculin/féminin ignoré

**Cause** : depuis la v2.7, Piper (voix réaliste) est utilisé en priorité
quand un modèle est dispo. Or les modèles fournis ont un **genre fixe** :
FR (UPMC) féminin, EN (Lessac) féminin, AR (Kareem) masculin. La fonction
`pickVoice` qui respecte le choix admin n'est appelée que dans le fallback
Web Speech API — qui n'était jamais déclenché quand Piper était installé.

**Correctif** :
- Le client envoie `&gender=male` ou `&gender=female` dans l'URL `/Tts/Speak`.
- Si le genre demandé **ne correspond pas** au modèle Piper de la langue,
  le serveur renvoie 204 → le client retombe sur Web Speech API qui sait
  choisir une voix du bon genre.
- Matrice :
    - genre `female` demandé : FR Piper ✓, EN Piper ✓, AR → fallback navigateur (voix arabe féminine type Hoda/Salma)
    - genre `male` demandé   : FR → fallback navigateur (Paul/Henri/…), EN → fallback (David/Mark/…), AR Piper ✓

## ✅ Conservé tel quel
Tickets, impression, CSV, langues, SMS, IA admin (résumé/rapport), agents,
guichets, services, monitoring imprimante, licence, base de données.

## 🧪 Test rapide après mise à jour
1. Ouvrir `/Display` dans un onglet **fraîchement ouvert** (pas un onglet
   déjà débloqué de session précédente).
2. L'overlay vert avec 🔊 doit apparaître.
3. Cliquer dessus → l'overlay disparaît, le plein écran est tenté.
4. Appeler un ticket depuis l'agent : la voix s'active immédiatement.
5. Aller dans Admin → Écran d'affichage → basculer Genre = Masculin.
   Réappeler un ticket en français : la voix doit devenir masculine
   (voix navigateur Paul/Henri/… au lieu de la voix Piper UPMC féminine).
