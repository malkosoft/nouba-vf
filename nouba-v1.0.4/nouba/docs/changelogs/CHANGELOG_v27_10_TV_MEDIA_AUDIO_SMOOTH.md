# Nouba v2.7.10 — Correctif TV / médias / audio / responsive

## Problèmes ciblés

- Sur Smart TV, la vidéo média pouvait s'arrêter au moment de l'annonce vocale d'un ticket.
- Le son d'appel ticket pouvait arriver avec une latence perceptible sur TV/téléphone.
- Le logo et le texte défilant en bas de l'affichage pouvaient être trop bas ou hachurés sur certains écrans TV.
- Les pages devaient mieux s'adapter aux différentes résolutions : TV 720p/1080p, téléphone, tablette, affichage portrait/paysage.

## Corrections appliquées

1. **Vidéo média plus robuste pendant les annonces**
   - Ajout d'un système de surveillance/reprise automatique de la vidéo média.
   - Si la Smart TV met la vidéo en pause pendant la lecture audio, Nouba tente de la relancer automatiquement.
   - Ajout des attributs compatibles TV : `playsinline`, `webkit-playsinline`, `muted`, `loop`, `preload`.

2. **Audio TTS plus fluide**
   - Le polling de secours de la page affichage passe de 3 s à 1,2 s.
   - Si SignalR fonctionne, le polling reste présent en sécurité mais moins agressif.
   - L'annonce TTS relance aussi la vérification de lecture média pour éviter l'arrêt vidéo.

3. **Footer / bandeau amélioré**
   - Bandeau bas remonté légèrement.
   - Hauteur adaptée à la résolution.
   - Animation du texte défilant rendue plus douce avec `translate3d` et `will-change`.
   - Logo du bandeau mieux centré et moins coupé par l'overscan TV.

4. **Responsive renforcé**
   - Ajout de règles pour grands écrans, TV 1280px, petits écrans, portrait et tablettes.
   - Meilleure adaptation des tailles de tickets, guichets, médias, bandeau et entête.

## À tester

- `http://IP_DU_PC:5000/display` sur TV.
- Vidéo média pendant appel d'un ticket.
- Son d'annonce ticket après activation audio.
- Footer/logo/bandeau sur TV.
- Affichage sur téléphone vertical et horizontal.
