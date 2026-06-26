# Nouba v2.7.15 — Fix layout Minimal/Vidéo dominante + bouton son TV visible

## Layout « Minimal » : rendu correct

**Cause** : la grille `grid-template-rows:88px 1fr` était figée en pixels
et l'entête « TICKET / GUICHET » restait visible alors qu'on n'a qu'un seul
ticket à montrer. Sur TV, ça donnait une bande inutile en haut et le ticket
était décalé.

**Correctif** :
- L'entête est maintenant masquée en mode minimal (`display:none`).
- Une seule ligne plein écran (`grid-template-rows:1fr`).
- Tailles de police agrandies : ticket `clamp(80px, 14vw, 320px)`, compteur
  `clamp(60px, 9vw, 200px)`. Sur TV 65", le ticket prend vraiment toute
  la place comme prévu.
- Padding adaptatif `clamp(20px, 3vh, 60px)`.

## Layout « Vidéo dominante » : aussi corrigé

**Cause** similaire : `grid-template-columns:1fr 0` mettait la 2ᵉ colonne
à largeur 0, mais l'historique se chevauchait avec le média.

**Correctif** :
- Right-col devient un simple bloc plein hauteur en row 1.
- Historique caché (`display:none !important`).
- Left-col en row 2 avec ticket grand : `clamp(70px, 9vw, 200px)`.

## Son TV : bouton flottant gros et impossible à manquer

Plutôt que de continuer à parier sur l'auto-unlock silencieux (qui ne marche
pas dans tous les contextes Smart TV), j'ai radicalement amélioré la stratégie
de fallback :

1. **Si l'auto-unlock échoue** : le bouton flottant 🔊 « Activer le son »
   apparaît au bout de 800 ms, **en parallèle de l'overlay**.
2. **Timer overlay 12 s → 4 s** : si rien n'a été cliqué, l'overlay couvrant
   se masque vite, mais le bouton flottant reste pour permettre l'activation
   plus tard sans bloquer l'écran.
3. **Bouton flottant beaucoup plus visible** :
   - Taille adaptative `clamp()` (gros sur TV, normal sur PC).
   - Animation pulse (qui attire l'œil).
   - Position bas-droite avec ombre dorée.
   - `tabIndex=0` + autofocus → la télécommande peut le « voir » directement.
4. **Au clic/tap/touche/keypress, l'audio est débloqué et le bouton disparaît.**

## Résumé des comportements possibles côté TV

| Situation | Avant | Après |
|---|---|---|
| Kiosque Chrome avec autoplay-policy | Auto-unlock OK | Auto-unlock OK |
| Smart TV qui autorise spontanément | Auto-unlock OK | Auto-unlock OK |
| Smart TV strict | Overlay 12 s puis bouton | Overlay 4 s puis bouton GROS pulsant |
| PC navigateur classique | Overlay → clic = OK | Overlay + bouton dispo en parallèle |

## Diagnostic toujours utile
Si après cette mise à jour le son ne marche toujours pas, ouvre F12 (Console)
sur le navigateur de la TV et copie-moi les lignes `[Nouba TTS] …` qui
apparaissent quand un ticket est appelé. Sans ce log, je devine.

## Conservé tel quel
Tickets, impression, langues, agents, services, presets thème, 6 mises en
page (avec minimal et video-hero corrigés ici), QR de suivi mobile, voix
Piper, fallback navigateur.
