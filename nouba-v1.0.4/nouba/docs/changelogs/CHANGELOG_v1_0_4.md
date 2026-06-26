# Nouba Pro 1.0.4 — Lot 2 (partie 2) : son TV + fluidité

Date : 2026-06-25

## #4 — Son TV (Hisense et autres) : fiabilité + diagnostic clair
- Réveil explicite de l'AudioContext (Web Audio) sur le geste de déblocage.
  Cause probable Hisense : le carillon « bi-bong » (Web Audio) restait muet car
  l'AudioContext n'était jamais repris sur un geste. Désormais on le resume +
  amorce sa sortie. (Additif : ne change rien là où ça marchait déjà.)
- Bip de déblocage : repli automatique sur un bip embarqué (base64, sans réseau)
  si la lecture via l'URL réseau échoue.
- Message d'échec précis (3 canaux) : « lecteur / voix navigateur / AudioContext ».
- Diagnostic VISIBLE à l'écran après un geste utilisateur (avant : seulement dans
  la console F12, invisible sur TV). Auto-masquage après 12 s. Au repos : rien.

## #2 — Fluidité de l'affichage
- Retrait du `backdrop-filter:blur` plein écran sur l'effet « wow » (joué à chaque
  appel) : très coûteux sur GPU de TV. Le dégradé radial couvrait déjà l'écran,
  l'effet reste quasi identique mais le rendu est nettement plus fluide.
- Reste déjà optimisé : polling adaptatif (1,2 s sans SignalR / 3 s avec), saut
  d'état via ETag, push SignalR, animations en transform/opacity (GPU).

## À tester sur la TV Hisense
1. Ouvrir l'affichage, appuyer sur « Son TV » (ou OK télécommande).
2. Si muet : un encadré « Diagnostic son » apparaît avec la cause exacte
   (autoplay refusé / AudioContext suspended / bip ko). Me communiquer ce texte.
3. Vérifier que l'effet « wow » est plus fluide à l'appel d'un ticket.
