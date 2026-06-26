# Nouba Pro 1.0.1 — Correctifs admin (voix arabe UI, diagnostic, points)

Date : 2026-06-25

## Voix arabe — même logique que FR/EN
- Un **seul** bouton « Tester AR » (au lieu de « AR choisi » + « AR féminin » +
  « AR masculin »). Le genre vient désormais du **sélecteur Féminine/Masculine**
  de droite, exactement comme le français et l'anglais.
- Cartes d'état : une seule carte « Arabe » (fusion des anciennes « AR masculin »
  et « AR féminin »).

## Voix Piper joue la voix du navigateur — diagnostic
- La fonction `testVoice` en doublon (qui basculait silencieusement vers la voix
  du navigateur) a été supprimée : elle entretenait la confusion.
- Le bouton « Tester » indique maintenant la **cause exacte** : modèle absent,
  synthèse Piper échouée, WAV vide, ou **lecture auto bloquée par le navigateur**
  (cas fréquent du « c'est la voix du navigateur qui se lit » : Piper génère bien
  le son, mais le navigateur refuse de le jouer tant qu'on n'a pas cliqué sur la
  page). Un succès affiche « Voix Piper OK ».
- Côté serveur, l'en-tête `X-Piper` (ok / model-missing / synth-failed) est
  remonté dans ce message.

## Nettoyage visuel — « points »
- Les petites icônes décoratives en tête de titres, libellés, notes et onglets
  de réglages s'affichaient comme des points/taches (jeu d'icônes simplifié).
  Elles sont masquées par une règle CSS ciblée (`.bi.me-1`, `.bi.me-2`,
  `.settings-tab .bi`). Les icônes des **boutons** ne sont pas touchées.
  Réversible : retirer le bloc en bas de `wwwroot/css/site.css`.
