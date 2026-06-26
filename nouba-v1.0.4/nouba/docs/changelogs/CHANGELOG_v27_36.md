# Nouba v2.7.36 — Correctif boutons agent bloquants + attaque voix arabe

Deux corrections importantes.

## 1. Boutons Terminer / Absent / Transférer invisibles → corrigé

Symptôme : l'agent ne pouvait pas appeler le suivant (« clôturez d'abord »),
mais les boutons pour clôturer (Terminer, Absent, Rappeler, Transférer)
n'étaient pas affichés → agent bloqué.

Cause : deux façons incohérentes de détecter le « ticket en cours ».
- Le garde-fou « appeler le suivant » regardait les tickets au statut
  « Called » (table Tickets).
- L'affichage des boutons regardait le dernier appel dans l'historique
  (table CallHistories), par nom de service.
Ces deux sources pouvaient se désynchroniser : un ticket était bien en cours
(garde-fou actif) mais l'historique ne le reflétait pas → boutons masqués.

Correction : le « ticket en cours » est désormais dérivé directement du
ticket réellement au statut « Called » (même source de vérité que le
garde-fou). Les boutons de clôture apparaissent donc toujours quand un ticket
est en cours. L'historique n'est utilisé qu'en complément (nom du guichet).

## 2. Début de la voix arabe « sonnait anglais »

Symptôme : l'attaque de la phrase arabe (surtout voix féminine) démarrait mal,
comme si elle parlait anglais sur le premier mot.

Cause : le moteur de phonèmes (espeak-ng) peut mal « attaquer » un tout
premier mot ambigu en arabe.

Correction : l'annonce arabe commence maintenant par un mot d'appel 100%
arabe non ambigu (« نِداء » = appel) suivi d'une courte pause, ce qui force
un démarrage clairement arabe. Le texte est aussi mieux voyellé
(« التذكرةُ رقم … »). Combiné au carillon doux qui précède la voix,
l'attaque est nettement plus propre.

Note : la voix arabe reste un modèle libre, moins abouti que le FR/EN ; cette
correction vise spécifiquement le démarrage de phrase.

## Important

- Pense à vider `wwwroot/tts/cache/` après mise à jour : le texte arabe ayant
  changé, les anciens enregistrements en cache ne correspondent plus (ils se
  régénèrent automatiquement, mais vider le dossier garantit le nouveau texte).
- Sauvegarde `nouba.db` avant déploiement (par précaution habituelle).

## Fichiers modifiés

- `Controllers/AgentController.cs` — « ticket en cours » fiabilisé (source =
  ticket au statut Called, cohérent avec le garde-fou).
- `Views/Display/Index.cshtml` — annonce arabe avec mot d'amorce + voyellisation.
- `Nouba.csproj` — version 2.7.36.

## Vérifications

- Boutons d'action agent : rendu vérifié visuellement (bien visibles et
  lisibles en thème sombre).
- Équilibre syntaxique C# contrôlé, JavaScript Display revalidé.
- À faire de ton côté : `dotnet build`, vider le cache TTS, puis tester
  l'enchaînement appeler → terminer, et écouter une annonce arabe.
