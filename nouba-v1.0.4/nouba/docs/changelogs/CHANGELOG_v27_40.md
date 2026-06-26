# Nouba v2.7.40 — QR dans « Borne kiosque » + fini les pop-ups du navigateur

Deux améliorations pour un outil plus professionnel.

## 1. Suivi mobile par QR déplacé dans « Borne kiosque »

Le réglage du suivi mobile par QR code se trouvait dans l'onglet
« Affichage ». Il est désormais dans l'onglet « Borne kiosque », ce qui est
plus logique (le QR est imprimé sur le ticket de la borne).

- Section retirée de l'onglet Affichage (et son sous-onglet).
- Réintégrée dans l'onglet Borne kiosque, visible directement.
- L'enregistrement fonctionne à l'identique (même formulaire de réglages).

## 2. Fini les pop-ups grises du navigateur → modales Nouba

Tu avais raison : les fenêtres « localhost dit… » avec OK/Annuler ne font pas
professionnel. C'étaient les pop-ups natives du navigateur (confirm/alert).

Elles sont toutes remplacées par une **modale au thème de l'application**
(or / bleu nuit, icône, titre, message, boutons) :
- Suppression d'un service, d'un guichet, d'un agent → modale rouge « danger ».
- Réinitialiser la journée → modale de confirmation.
- Restaurer le thème par défaut → modale de confirmation.
- Messages d'information (rapport copié, URL valide/invalide) → modale info.

Plus aucune pop-up native dans l'administration. Le comportement (Entrée =
confirmer, Échap = annuler, clic en dehors = annuler) est géré proprement.

## Fichiers modifiés

- `Views/Admin/Index.cshtml` — modale Nouba (HTML/CSS/JS) + remplacement de
  tous les confirm()/alert() natifs ; déplacement du bloc QR vers l'onglet
  Borne kiosque.
- `Nouba.csproj` — version 2.7.40.

## Vérifications

- Rendu de la modale vérifié visuellement (thème sombre, lisible).
- Équilibre HTML (587 div ouvrants / 587 fermants) et JavaScript validés.
- Plus aucun confirm()/alert() natif dans l'admin.
- À faire de ton côté : `dotnet build`, puis vérifier le déplacement du QR
  dans « Borne kiosque » et tester une suppression (la modale doit apparaître).
