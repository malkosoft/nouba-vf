# Nouba v2.7.43 — Correctif probable du « ça marche un coup sur deux » (voix)

Deuxième correctif du chantier stabilité voix, et le plus important : un
vrai bug de fond dans le pré-chauffage TTS, identifié en comparant le texte
généré côté serveur et celui réellement demandé par l'écran Display.

## Le problème (confirmé par lecture de code, pas une supposition)

Quand un agent appelle un ticket, le serveur lance en arrière-plan une
synthèse Piper « anticipée » pour que le fichier audio soit déjà en cache
disque au moment où l'écran Display le demande réellement (latence
SignalR + fetch ≈ 100-300 ms) — but explicitement décrit dans le code :
*« le WAV est souvent déjà en cache disque quand le client le demande...
la voix démarre quasi instantanément »*.

Sauf que le texte généré côté serveur pour ce pré-chauffage était
complètement différent de celui que l'écran Display génère réellement en
JavaScript :

- Serveur (avant) : `"Ticket A12 guichet Guichet 3"`
- Display (réel)  : `"Ticket A douze, service Réception, veuillez vous
  présenter au Guichet trois"`

Le cache Piper est indexé sur le **texte exact**. Avec deux textes
différents, le pré-chauffage ne produisait **jamais** de cache hit. Résultat
concret : chaque annonce réelle, sans exception, repartait d'une synthèse
Piper à froid — avec une durée variable selon la charge du serveur au
moment précis de l'appel (autres synthèses en cours, modèle arabe plus
lent, etc.). Quand cette synthèse à froid prenait un peu plus de temps que
d'habitude, les filets de sécurité côté navigateur (délai de 9 s sans
lecture démarrée) pouvaient se déclencher et faire basculer — ou échouer —
l'annonce. D'où l'impression de hasard, identique sur PC et sur TV puisque
la cause est côté serveur et non liée au navigateur.

## Le correctif

Nouveau fichier `Helpers/AnnouncementTextBuilder.cs` qui reproduit
fidèlement, en C#, la même logique que le JavaScript de
`Display/Index.cshtml` (numéros épelés en toutes lettres par langue,
formatage du nom de guichet, gabarits de phrase identiques). Le
pré-chauffage dans `AgentController.CallNext` utilise désormais ce
helper : le texte généré côté serveur est maintenant **identique** à celui
que l'écran demandera quelques centaines de millisecondes plus tard → vrai
cache hit → réponse quasi instantanée, sans repasser par une synthèse à
froid sur le chemin critique.

**Vérification effectuée** : j'ai extrait les fonctions JavaScript
d'origine et le portage C# dans des scripts Node.js séparés, puis comparé
leur sortie sur 22 cas de test (nombres traversant les paliers 70/80/90 en
français — les plus piégeux —, tickets à zéros non significatifs comme
B007, noms de guichet sans chiffre, casse mixte, les 4 langues). Les 22 cas
correspondent à l'identique. Je n'ai pas pu exécuter le vrai code C#
(`dotnet` indisponible dans mon environnement), donc ce test croisé est la
meilleure vérification possible sans compilation réelle — `dotnet build`
reste la confirmation finale à faire de votre côté.

## Important

Ceci corrige une cause réelle et significative de latence/échec
intermittent, mais je ne peux pas garantir que c'est l'unique cause de
« ça marche un coup sur deux » sans pouvoir tester en conditions réelles
(matériel, charge serveur, modèle de TV). Si le problème persiste après
cette mise à jour, le panneau de diagnostic visible en bas à gauche de
l'écran Display (qui s'affiche automatiquement à la première erreur)
indique la raison précise de l'échec — utile pour cibler un éventuel
correctif suivant.

## Fichiers modifiés

- `Helpers/AnnouncementTextBuilder.cs` (nouveau)
- `Controllers/AgentController.cs` — utilise le nouveau helper pour le
  pré-chauffage, ancienne fonction `BuildAnnouncementText` retirée.
- `Nouba.csproj` — version 2.7.43.

## À faire de votre côté

- `dotnet build`.
- Tester un appel de ticket normal sur Display (PC puis Smart TV si
  possible) plusieurs fois de suite, dans différentes langues, et noter si
  la fréquence du problème diminue.
