# Nouba v2.7.31 — 6 voix : français, anglais, arabe × féminin & masculin

## Objectif

Tu veux que les TROIS langues (français, anglais, arabe) disposent CHACUNE
d'une voix féminine ET masculine. Le code précédent forçait le français et
l'anglais sur la voix féminine uniquement ; seul l'arabe gérait les deux
genres. Cette version corrige ça.

## Ce qui change dans le code

### Français : 2 voix dans un seul fichier
Le modèle `fr_FR-upmc-medium` contient deux locuteurs : jessica (féminin) et
pierre (masculin). Le service sélectionne désormais le bon locuteur via
l'argument Piper `--speaker` selon le genre demandé. Aucun fichier
supplémentaire nécessaire pour le français.

### Anglais : ajout d'un modèle masculin
L'anglais lessac n'a qu'une voix (féminine). Le code accepte maintenant un
modèle masculin séparé (`en_US-ryan-medium` recommandé, plus plusieurs
alternatives). Féminin = lessac, masculin = ryan.

### Arabe : inchangé (déjà 2 genres)
Féminin = arabic-emirati-female-model, masculin = ar_JO-kareem-medium
recommandé.

### Détails techniques importants
- Nouvelle résolution unifiée `ResolveVoice(langue, genre)` qui renvoie le
  fichier, le locuteur et le genre réellement servi.
- **La clé de cache inclut désormais le genre/locuteur.** Sans ça, la voix
  féminine et la voix masculine d'une même langue (surtout le FR, même
  fichier) auraient partagé le même audio en cache → mauvaise voix servie.
  C'est corrigé.
- `--speaker` n'est ajouté que pour les modèles multi-locuteurs (lecture de
  `num_speakers` dans le `.onnx.json`, avec mise en cache).
- Pré-chauffe étendue aux 6 voix au démarrage.
- `/Tts/Status` affiche maintenant l'état réel des 6 voix (female/male par
  langue) pour faciliter le diagnostic.

Le genre est piloté par le réglage « Voix » existant de l'Admin : un seul
choix féminin/masculin s'applique désormais aux trois langues.

## CE QUE TU DOIS FAIRE (important)

Les modèles de voix (.onnx) ne sont PAS dans le zip (trop lourds). Pour les
6 voix, télécharge ces fichiers dans `wwwroot/tts/piper/` (chaque .onnx AVEC
son .onnx.json) :

- `fr_FR-upmc-medium.onnx`            → FR féminin + masculin (1 fichier)
- `en_US-lessac-medium.onnx`          → EN féminin
- `en_US-ryan-medium.onnx`            → EN masculin (NOUVEAU)
- `arabic-emirati-female-model.onnx`  → AR féminin
- `ar_JO-kareem-medium.onnx`          → AR masculin

Liens et instructions détaillés : `wwwroot/tts/piper/README.txt` (mis à jour).
Vérification : ouvre `http://<serveur>:5000/Tts/Status` — tu dois voir
`gender:"female+male"` pour fr, en et ar.

## Limite de cette livraison

Le code C# a été écrit et relu avec soin (équilibre, signatures, logique),
MAIS il n'a pas pu être compilé ni testé dans l'environnement de préparation
(pas de SDK .NET, pas d'accès aux vrais fichiers .onnx volumineux).
**Fais impérativement `dotnet build` et un test des 3 langues × 2 genres**
après avoir déposé les modèles.

## Fichiers modifiés

- `Services/PiperTtsService.cs` — résolution des 6 voix, `--speaker`, cache
  par genre, statut détaillé, pré-chauffe.
- `wwwroot/tts/piper/README.txt` — guide des 6 voix + liens.
- `Nouba.csproj` — version 2.7.31.
